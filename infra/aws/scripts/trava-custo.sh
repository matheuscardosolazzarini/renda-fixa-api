#!/usr/bin/env bash
# Trava de custo, camada 6a da F11 (docs/SPEC-F11-EXECUCAO.md).
#
# Lê pela entrada padrão o JSON do plano (terraform show -json <plano>) e reprova (exit 1) se:
#  - houver recurso gerenciado fora da lista positiva abaixo;
#  - algum atributo estiver fora do limite de custo (classe do RDS, Multi-AZ, armazenamento, tipo de disco,
#    IOPS, backup, tipo do ALB, replicação do secret,
#    acesso público, desired_count, CPU e memória das tarefas, retenção dos logs); valor ainda desconhecido
#    no plano (after_unknown) nos atributos de valor exato também reprova;
#  - uma proteção que impede o destroy ou deixa resíduo estiver ligada, ou um recurso de custo contínuo estiver
#    ligado: RDS com deletion_protection, sem skip_final_snapshot, sem delete_automated_backups ou com
#    enabled_cloudwatch_logs_exports; ALB com enable_deletion_protection; secret com recovery_window_in_days
#    diferente de 0; cluster ECS sem a setting containerInsights = disabled (ausente reprova: a conta pode ter
#    Container Insights ligado por padrão). Valor desconhecido (after_unknown) nesses atributos também reprova;
#  - o plano tiver mais itens que o ambiente comporta: 1 aws_db_instance, 1 aws_lb, 1 aws_ecs_service,
#    1 aws_default_security_group e 2 aws_ecs_task_definition (itens com qualquer ação diferente de um "delete" isolado);
#  - a entrada for vazia, não for JSON, não tiver resource_changes ou tiver item malformado
#    (fail closed: o que não dá para verificar não é aprovado).
#
# Uso: terraform show -json plano.tfplan | scripts/trava-custo.sh
#
# O JSON do plano tem variáveis sensíveis: o script nunca o imprime nem o salva. As mensagens
# trazem só tipo, endereço e nome do atributo, nunca o valor. Os erros do jq vão para /dev/null
# porque podem citar um trecho do dado.
#
# Decisões de escopo (registradas no relatório do P5):
#  - Só o modo "managed" passa pela lista; data sources e efêmeros não criam nada.
#  - Fica de fora da verificação o item cuja única ação é "delete": apagar não custa e o plano de
#    limpeza precisa passar. Qualquer outra ação (create, update, replace, no-op, forget) é verificada,
#    inclusive no-op: um recurso caro já existente no state não pode passar despercebido.
#  - resource_changes vazio reprova: um plano sem nenhum item não permite concluir que está dentro
#    do limite, e o plano real do ambiente sempre lista os recursos (ao menos como no-op).
set -euo pipefail

if ! command -v jq > /dev/null 2>&1; then
  echo "REPROVADO trava de custo sem jq: não há como verificar o plano" >&2
  exit 1
fi

# Lista positiva: tipo -> motivo. Fechada no P5 a partir do que infra/aws declara (resource).
# Tudo o que não está aqui custa por hora ou foge do desenho (NAT, Elastic IP, EC2, VPC endpoint,
# KMS de cliente, Route 53, Transit Gateway...) e é barrado, inclusive recurso novo que alguém
# adicione sem passar por esta lista.
# O programa jq usa $ (variáveis do jq): as aspas simples são intencionais.
# shellcheck disable=SC2016
programa='
def lista: {
  "aws_vpc": "rede do ambiente; sem custo por hora",
  "aws_subnet": "duas subnets publicas (ALB e RDS exigem duas AZs); sem custo",
  "aws_internet_gateway": "saida das tarefas para o GHCR sem NAT; sem custo",
  "aws_route_table": "rota publica para o internet gateway; sem custo",
  "aws_route_table_association": "liga as subnets a rota publica; sem custo",
  "aws_security_group": "cadeia ALB, tarefa e RDS; sem custo",
  "aws_default_security_group": "adota o SG padrao da VPC sem nenhuma regra; sem custo",
  "aws_vpc_security_group_ingress_rule": "regra de entrada separada do SG; sem custo",
  "aws_vpc_security_group_egress_rule": "regra de saida separada do SG; sem custo",
  "aws_db_subnet_group": "exigido pelo RDS; sem custo",
  "aws_db_instance": "PostgreSQL db.t4g.micro; custo limitado por atributo",
  "aws_secretsmanager_secret": "connection string (D6); custo por hora do secret, destruido junto",
  "aws_secretsmanager_secret_version": "valor do secret; sem custo adicional",
  "aws_cloudwatch_log_group": "logs com retencao de 3 dias",
  "aws_ecs_cluster": "cluster Fargate sem Container Insights; vazio nao custa",
  "aws_ecs_task_definition": "tarefas Fargate 0,25 vCPU e 0,5 GB; custo limitado por atributo",
  "aws_ecs_service": "servico da API com desired_count 0 ou 1",
  "aws_lb": "ALB (custo por hora, destruido junto)",
  "aws_lb_target_group": "alvo do ALB; sem custo",
  "aws_lb_listener": "listener HTTP 80; sem custo"
};

def num_le($v; $max): ($v | type) == "number" and $v <= $max;

# Atributo de valor exato: reprova se o valor ainda é desconhecido no plano (after_unknown) ou se difere.
# Sem isto, um valor desconhecido que viesse com um "after" aparente passaria.
def exato($a; $u; $campo; $esperado): ($u[$campo] != true) and ($a[$campo] == $esperado);

# Valor ainda desconhecido no plano: true, ou uma coleção com algum elemento desconhecido.
def desconhecido($u; $campo): ($u[$campo] // false) | if type == "boolean" then . else ([.. | booleans] | any) end;

# Máximo de itens por tipo (o ambiente tem um só banco, um só ALB, um só serviço, duas task definitions e uma só VPC,
# portanto um só security group padrão).
# Conta os itens com qualquer ação diferente de um "delete" isolado; uma substituição conta uma vez.
def maximos: {"aws_db_instance": 1, "aws_lb": 1, "aws_ecs_service": 1, "aws_ecs_task_definition": 2, "aws_default_security_group": 1};

def limites:
  . as $rc
  | ($rc.change.after // {}) as $a
  | ($rc.change.after_unknown | if type == "object" then . else {} end) as $u
  | if $rc.type == "aws_db_instance" then
      [
        (if (exato($a; $u; "instance_class"; "db.t4g.micro") | not) then "instance_class deve ser db.t4g.micro" else empty end),
        (if (exato($a; $u; "multi_az"; false) | not) then "multi_az deve ser false" else empty end),
        (if (num_le($a.allocated_storage; 20) | not) then "allocated_storage deve ser no maximo 20" else empty end),
        (if (exato($a; $u; "storage_type"; "gp3") | not) then "storage_type deve ser gp3" else empty end),
        (if $a.iops != null and (num_le($a.iops; 3000) | not)
          then "iops provisionado acima do incluso: deve ficar vazio ou ser no maximo 3000" else empty end),
        (if (exato($a; $u; "backup_retention_period"; 0) | not) then "backup_retention_period deve ser 0" else empty end),
        (if (exato($a; $u; "publicly_accessible"; false) | not) then "publicly_accessible deve ser false" else empty end),
        (if ($u.max_allocated_storage == true) or (num_le($a.max_allocated_storage // 0; 20) | not)
          then "max_allocated_storage deve ficar vazio ou ser no maximo 20" else empty end),
        (if ($u.performance_insights_enabled == true) or ($a.performance_insights_enabled // false) != false
          then "performance_insights_enabled deve ser false" else empty end),
        (if ($u.monitoring_interval == true) or ($a.monitoring_interval // 0) != 0
          then "monitoring_interval deve ser 0" else empty end),
        (if (exato($a; $u; "deletion_protection"; false) | not) then "deletion_protection deve ser false" else empty end),
        (if (exato($a; $u; "skip_final_snapshot"; true) | not) then "skip_final_snapshot deve ser true" else empty end),
        (if (exato($a; $u; "delete_automated_backups"; true) | not) then "delete_automated_backups deve ser true" else empty end),
        (if desconhecido($u; "enabled_cloudwatch_logs_exports") or (($a.enabled_cloudwatch_logs_exports // []) | length) > 0
          then "enabled_cloudwatch_logs_exports deve ficar vazio (sem exportação de logs)" else empty end)
      ]
    elif $rc.type == "aws_lb" then
      [
        (if (exato($a; $u; "load_balancer_type"; "application") | not) then "load_balancer_type deve ser application" else empty end),
        (if (exato($a; $u; "enable_deletion_protection"; false) | not) then "enable_deletion_protection deve ser false" else empty end)
      ]
    elif $rc.type == "aws_secretsmanager_secret" then
      [
        (if ($u.replica == true) or ((($a.replica // []) | length) > 0) then "replica deve ficar vazio (sem replicação entre regiões)" else empty end),
        (if (exato($a; $u; "recovery_window_in_days"; 0) | not) then "recovery_window_in_days deve ser 0 (sem janela de recuperação o secret não sobra depois do destroy)" else empty end)
      ]
    elif $rc.type == "aws_ecs_cluster" then
      [
        (((($a.setting // []) | map(select(.name == "containerInsights"))) as $ci
          | (desconhecido($u; "setting")) or ($ci | length) != 1 or $ci[0].value != "disabled")
          | if . then "setting containerInsights deve ser disabled (Container Insights ligado cobra por métrica)" else empty end)
      ]
    elif $rc.type == "aws_ecs_service" then
      [ (if (num_le($a.desired_count; 1) | not) then "desired_count deve ser no maximo 1" else empty end) ]
    elif $rc.type == "aws_ecs_task_definition" then
      [
        (if ($a.cpu | tostring) != "256" then "cpu deve ser 256" else empty end),
        (if ($a.memory | tostring) != "512" then "memory deve ser 512" else empty end)
      ]
    elif $rc.type == "aws_cloudwatch_log_group" then
      [ (if $a.retention_in_days != 3 then "retention_in_days deve ser 3" else empty end) ]
    else [] end;

def bem_formado:
  type == "object"
  and ((.address | type) == "string")
  and ((.type | type) == "string")
  and ((.mode | type) == "string")
  and ((.change | type) == "object")
  and ((.change.actions | type) == "array")
  and all(.change.actions[]; type == "string")
  and (((.change.after // {}) | type) == "object");

def reprovacoes:
  to_entries[]
  | .key as $i
  | .value as $rc
  | if ($rc | bem_formado | not) then
      "REPROVADO resource_changes[\($i)]: item malformado"
    elif $rc.mode == "data" or $rc.mode == "ephemeral" then
      empty
    elif $rc.mode != "managed" then
      "REPROVADO \($rc.type) \($rc.address): modo desconhecido no plano"
    elif $rc.change.actions == ["delete"] then
      empty
    else
      (if (lista | has($rc.type)) then empty
       else "REPROVADO \($rc.type) \($rc.address): tipo fora da lista positiva" end),
      ($rc | limites[] | "REPROVADO \($rc.type) \($rc.address): \(.)")
    end;

# Só sobre itens bem formados (os malformados já reprovam em reprovacoes).
def cardinalidade:
  [ .[] | select(bem_formado and .mode == "managed" and .change.actions != ["delete"]) | .type ]
  | group_by(.)
  | .[]
  | select(maximos[.[0]] != null and length > maximos[.[0]])
  | "REPROVADO \(.[0]): \(length) recursos no plano, no máximo \(maximos[.[0]])";

if length != 1 then
  "REPROVADO entrada inválida: esperado exatamente um documento JSON"
else
  .[0] as $p
  | if ($p | type) != "object" then
      "REPROVADO entrada inválida: o plano deve ser um objeto JSON"
    elif ($p.resource_changes | type) != "array" then
      "REPROVADO resource_changes ausente ou inválido: sem ele o plano não pode ser verificado"
    elif ($p.resource_changes | length) == 0 then
      "REPROVADO resource_changes vazio: um plano sem itens não pode ser verificado"
    else
      [ $p.resource_changes | reprovacoes, cardinalidade ] as $r
      | if ($r | length) > 0 then $r[]
        else "APROVADO \($p.resource_changes | length) itens do plano dentro da lista positiva e dos limites" end
    end
end
'

if ! saida="$(jq -rs "$programa" 2> /dev/null)"; then
  echo "REPROVADO entrada inválida: o plano não é um JSON legível" >&2
  exit 1
fi

if [[ "$saida" == REPROVADO* || "$saida" == *$'\n'REPROVADO* ]]; then
  echo "$saida" >&2
  echo "Trava de custo: plano reprovado." >&2
  exit 1
fi

if [[ "$saida" == APROVADO* ]]; then
  echo "Trava de custo: $saida"
  exit 0
fi

# Qualquer outra saída (inclusive vazia) é inesperada: fail closed.
echo "REPROVADO resultado inesperado da análise do plano" >&2
exit 1
