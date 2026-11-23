#!/usr/bin/env bash
# Verificação de zerado, camada 4 da F11 (docs/SPEC-F11-EXECUCAO.md).
#
# Lista na CONTA (não no state do Terraform) tudo o que custa por hora e deveria ter sumido depois do
# destroy, e sai com código diferente de zero se achar qualquer coisa. Imprime só tipo e identificador.
#
# Uso: scripts/verificar-zerado.sh [REGIAO]      (padrão: variável AWS_REGION; sem nenhuma, falha)
#   Contra o Floci: AWS_ENDPOINT_URL=http://localhost:4566 (a CLI v2 usa essa variável sozinha).
# Saída: 0 = conta zerada; 1 = achou resíduo; 2 = não conseguiu verificar (erro de chamada,
#   credencial, permissão, API inexistente, timeout ou resposta inesperada).
#
# Fail closed: erro de chamada nunca vale como "vazio". Todo erro é registrado, a verificação continua
# para mostrar o quadro completo e o resultado final é reprovação (código 2).
#
# Contrato com o bootstrap (P6): o bootstrap NÃO aplica a tag project=renda-fixa-api nos recursos dele
# (bucket do state, roles, provedor OIDC, budget); usa stack=bootstrap e app=renda-fixa-api. Esta
# verificação também não lista S3, IAM nem Budgets, e na busca por tag descarta (com aviso) ARNs de
# s3, iam e budgets, que o Terraform do ambiente nunca declara.
#
# Permissões de leitura que a role do CI precisa para este script (para o P6):
#   sts:GetCallerIdentity (não exige permissão), ecs:ListClusters/ListServices/ListTasks,
#   elasticloadbalancing:DescribeLoadBalancers/DescribeTargetGroups,
#   rds:DescribeDBInstances/DescribeDBSnapshots/DescribeDBInstanceAutomatedBackups,
#   ec2:DescribeNatGateways/DescribeAddresses/DescribeNetworkInterfaces/DescribeInstances/DescribeVpcEndpoints,
#   secretsmanager:ListSecrets, logs:DescribeLogGroups, kms:ListKeys/DescribeKey, tag:GetResources.
set -euo pipefail

export AWS_PAGER=""
PROJETO="renda-fixa-api"
REGIAO="${1:-${AWS_REGION:-}}"

if [[ -z "$REGIAO" || ! "$REGIAO" =~ ^[a-z]{2}(-[a-z]+)+-[0-9]+$ ]]; then
  echo "ERRO região ausente ou inválida: informe o argumento ou AWS_REGION" >&2
  exit 2
fi
for ferramenta in aws jq; do
  if ! command -v "$ferramenta" > /dev/null 2>&1; then
    echo "ERRO ferramenta ausente: $ferramenta" >&2
    exit 2
  fi
done

ERR_TMP="$(mktemp)"
ERROS_TMP="$(mktemp)" # erro() roda também em subshell (dentro de $(chamar ...)): o contador vai para arquivo
trap 'rm -f "$ERR_TMP" "$ERROS_TMP"' EXIT

residuos=0

residuo() { # tipo identificador
  echo "RESIDUO $1 $2"
  residuos=$((residuos + 1))
}

erro() { # mensagem
  echo "ERRO $1" >&2
  echo x >> "$ERROS_TMP"
}

aviso() { # mensagem
  echo "AVISO $1"
}

# Chama a CLI e devolve o JSON em stdout. Em qualquer falha registra o erro (só o código da AWS, nunca
# a mensagem inteira) e devolve 1.
chamar() { # servico operacao [args...]
  local saida codigo
  if saida="$(aws "$@" --region "$REGIAO" --output json --cli-connect-timeout 10 --cli-read-timeout 60 2> "$ERR_TMP")"; then
    if [[ -z "$saida" ]] || ! jq -e 'type == "object"' <<< "$saida" > /dev/null 2>&1; then
      erro "$1 $2: resposta vazia ou em formato inesperado"
      return 1
    fi
    printf '%s' "$saida"
    return 0
  fi
  codigo="$(sed -n 's/.*An error occurred (\([A-Za-z0-9._]*\)).*/\1/p' "$ERR_TMP" | head -n 1)"
  erro "$1 $2: ${codigo:-falha sem código da AWS (rede, credencial, timeout ou API inexistente)}"
  return 1
}

# Lista identificadores com um filtro jq e registra cada um como resíduo do tipo dado.
achar() { # tipo filtro-jq servico operacao [args...]
  local tipo="$1" filtro="$2" resposta ids id
  shift 2
  if ! resposta="$(chamar "$@")"; then
    return 0
  fi
  if ! ids="$(jq -r "$filtro" <<< "$resposta" 2> /dev/null)"; then
    erro "$1 $2: resposta em formato inesperado"
    return 0
  fi
  while IFS= read -r id; do
    if [[ -n "$id" ]]; then
      residuo "$tipo" "$id"
    fi
  done <<< "$ids"
}

# Credencial primeiro: sem ela nada do resto vale.
if ! aws sts get-caller-identity --region "$REGIAO" --output json --cli-connect-timeout 10 --cli-read-timeout 60 > /dev/null 2> "$ERR_TMP"; then
  codigo="$(sed -n 's/.*An error occurred (\([A-Za-z0-9._]*\)).*/\1/p' "$ERR_TMP" | head -n 1)"
  echo "ERRO sts get-caller-identity: ${codigo:-sem credencial válida ou sem rede}" >&2
  echo "Verificação de zerado: NAO CONCLUIDA (0 resíduos, 1 erro)" >&2
  exit 2
fi

# ---- ECS: clusters, serviços e tarefas em execução, em qualquer cluster ----
# Cluster vazio não custa, mas conta como resíduo: o destroy deveria tê-lo removido, e sobra com a tag
# do projeto. Tarefas paradas (STOPPED) não custam e ficam de fora.
# O list-clusters pode devolver, por um tempo, um cluster recém-apagado (INACTIVE): ele não existe mais e
# listar serviços ou tarefas nele daria erro. Por isso cada cluster passa por describe-clusters antes. Só o
# INACTIVE (ou o MISSING, apagado entre as duas chamadas) vira aviso; ACTIVE e os estados de transição
# (PROVISIONING, DEPROVISIONING, FAILED) ainda existem e são resíduo. Erro no describe é erro de chamada.
if clusters="$(chamar ecs list-clusters)"; then
  if arns="$(jq -r '.clusterArns[]' <<< "$clusters" 2> /dev/null)"; then
    while IFS= read -r cluster; do
      if [[ -z "$cluster" ]]; then
        continue
      fi
      if ! descricao="$(chamar ecs describe-clusters --clusters "$cluster")"; then
        continue
      fi
      if ! situacao="$(jq -r '[(.clusters[0].status // "-"), ((.failures // [])[0].reason // "-")] | @tsv' <<< "$descricao" 2> /dev/null)"; then
        erro "ecs describe-clusters: resposta em formato inesperado"
        continue
      fi
      status="${situacao%%$'\t'*}"
      motivo="${situacao#*$'\t'}"
      if [[ "$status" == "INACTIVE" || ( "$status" == "-" && "$motivo" == "MISSING" ) ]]; then
        aviso "ecs-cluster-inexistente $cluster (status ${status/#-/MISSING}: apagado, não custa)"
        continue
      fi
      if [[ "$status" == "-" ]]; then
        erro "ecs describe-clusters: o cluster não veio na resposta nem em failures"
        continue
      fi
      residuo ecs-cluster "$cluster"
      achar ecs-servico '.serviceArns[]' ecs list-services --cluster "$cluster"
      achar ecs-tarefa '.taskArns[]' ecs list-tasks --cluster "$cluster"
    done <<< "$arns"
  else
    erro "ecs list-clusters: resposta em formato inesperado"
  fi
fi

# ---- Load balancers e target groups ----
achar load-balancer '.LoadBalancers[].LoadBalancerArn' elbv2 describe-load-balancers
achar target-group '.TargetGroups[].TargetGroupArn' elbv2 describe-target-groups

# ---- RDS: instâncias, snapshots manuais e backups automáticos retidos (depois do delete) ----
achar rds-instancia '.DBInstances[].DBInstanceIdentifier' rds describe-db-instances
achar rds-snapshot-manual '.DBSnapshots[].DBSnapshotIdentifier' rds describe-db-snapshots --snapshot-type manual
achar rds-backup-automatico '.DBInstanceAutomatedBackups[].DBInstanceAutomatedBackupsArn' rds describe-db-instance-automated-backups

# ---- Rede que cobra por hora ----
# NAT e VPC endpoint já apagados ficam visíveis por um tempo com state deleted: não custam.
achar nat-gateway '.NatGateways[] | select(.State != "deleted" and .State != "failed") | .NatGatewayId' ec2 describe-nat-gateways
achar elastic-ip '.Addresses[] | (.AllocationId // .PublicIp)' ec2 describe-addresses
# ENI com IPv4 público associado (tarefas Fargate, ALB): cobrado mesmo sem ser Elastic IP.
achar eni-ipv4-publico '.NetworkInterfaces[] | select(.Association.PublicIp != null) | .NetworkInterfaceId' ec2 describe-network-interfaces
achar ec2-instancia '.Reservations[].Instances[] | select(.State.Name != "terminated") | .InstanceId' ec2 describe-instances
achar vpc-endpoint '.VpcEndpoints[] | select(.State | ascii_downcase | IN("deleted", "failed", "rejected", "expired") | not) | .VpcEndpointId' ec2 describe-vpc-endpoints

# ---- Secrets Manager: todos da conta, inclusive os agendados para exclusão ----
# O secret em janela de recuperação continua cobrado até sumir de vez. A conta não tem outro uso de
# Secrets Manager, então qualquer secret é resíduo.
achar secret '.SecretList[] | .Name + (if .DeletedDate != null then " (agendado para exclusao)" else "" end)' secretsmanager list-secrets --include-planned-deletion

# ---- CloudWatch Logs: qualquer log group com o nome do projeto ----
# Substring no nome: pega /ecs/<projeto>, /aws/rds/instance/<projeto>/..., Container Insights etc.
# O filtro é feito aqui, não por --log-group-name-pattern: o Floci ignora o padrão e devolveria tudo.
achar log-group ".logGroups[] | select(.logGroupName | contains(\"$PROJETO\")) | .logGroupName" logs describe-log-groups

# ---- KMS: chave gerenciada pelo cliente (proibida no projeto) ----
# list-keys também traz as chaves gerenciadas pela AWS (aws/rds, aws/secretsmanager...), então cada
# chave passa por describe-key e só KeyManager = CUSTOMER conta. Chave em PendingDeletion não é usável
# nem removível antes do prazo: gera AVISO e não reprova (a AWS não cobra chave agendada para exclusão,
# a confirmar no P8); qualquer outro estado (Enabled, Disabled...) é resíduo.
if chaves="$(chamar kms list-keys)"; then
  if ids="$(jq -r '.Keys[].KeyId' <<< "$chaves" 2> /dev/null)"; then
    while IFS= read -r chave; do
      if [[ -z "$chave" ]]; then
        continue
      fi
      if meta="$(chamar kms describe-key --key-id "$chave")"; then
        if linha="$(jq -r '.KeyMetadata | [.KeyManager, .KeyState] | @tsv' <<< "$meta" 2> /dev/null)"; then
          gerente="${linha%%$'\t'*}"
          estado="${linha#*$'\t'}"
          if [[ "$gerente" == "CUSTOMER" ]]; then
            if [[ "$estado" == "PendingDeletion" || "$estado" == "PendingReplicaDeletion" ]]; then
              aviso "kms-chave-agendada-para-exclusao $chave"
            else
              residuo kms-chave-cliente "$chave"
            fi
          fi
        else
          erro "kms describe-key: resposta em formato inesperado"
        fi
      else
        # Falha de describe-key costuma ser permissão ou rede, igual para todas as chaves: um erro basta.
        break
      fi
    done <<< "$ids"
  else
    erro "kms list-keys: resposta em formato inesperado"
  fi
fi

# ---- Qualquer recurso com a tag do projeto (não é a única barreira: tudo acima olha a conta) ----
# O provider só desregistra a task definition no destroy: ela fica INACTIVE, com a tag do projeto, e a tagging
# API pode continuar devolvendo-a. Task definition não custa: AVISO. A tarefa (task/) que a tagging API devolve
# pode ser uma já STOPPED, que ainda leva a tag propagada por cerca de 1 h: só vira AVISO se o describe-tasks
# confirmar STOPPED (ou que ela não existe mais). Tarefa em execução reprova, e erro na confirmação é erro de
# chamada (saída 2): nunca vale como "parada". Tarefa em execução também é pega por list-tasks, acima.
# Confirma o estado de uma tarefa. Imprime "parada" (STOPPED ou já inexistente) ou "ativa". O único erro da AWS que
# conta como "inexistente" é ClusterNotFoundException: um cluster só é apagado sem tarefas ativas.
estado_tarefa() { # arn
  local arn="$1" caminho resposta codigo estado
  local -a argumentos
  caminho="${arn#*:task/}"
  argumentos=(ecs describe-tasks --tasks "$arn")
  if [[ "$caminho" == */* ]]; then
    argumentos+=(--cluster "${caminho%%/*}") # ARN no formato novo: task/<cluster>/<id>
  fi
  if ! resposta="$(aws "${argumentos[@]}" --region "$REGIAO" --output json --cli-connect-timeout 10 --cli-read-timeout 60 2> "$ERR_TMP")"; then
    codigo="$(sed -n 's/.*An error occurred (\([A-Za-z0-9._]*\)).*/\1/p' "$ERR_TMP" | head -n 1)"
    if [[ "$codigo" == "ClusterNotFoundException" ]]; then
      echo parada
      return 0
    fi
    erro "ecs describe-tasks: ${codigo:-falha sem código da AWS (rede, credencial, timeout ou API inexistente)}"
    return 1
  fi
  if ! estado="$(jq -r 'if ((.tasks // []) | length) > 0 then (.tasks[0].lastStatus // "-")
      elif (((.failures // []) | map(.reason) | index("MISSING")) != null) then "MISSING"
      else "-" end' <<< "$resposta" 2> /dev/null)" || [[ "$estado" == "-" ]]; then
    erro "ecs describe-tasks: resposta sem o estado da tarefa"
    return 1
  fi
  if [[ "$estado" == "STOPPED" || "$estado" == "MISSING" ]]; then
    echo parada
  else
    echo ativa
  fi
}

if marcados="$(chamar resourcegroupstaggingapi get-resources --tag-filters "Key=project,Values=$PROJETO")"; then
  if arns="$(jq -r '.ResourceTagMappingList[].ResourceARN' <<< "$marcados" 2> /dev/null)"; then
    ignorados=0
    while IFS= read -r arn; do
      if [[ -z "$arn" ]]; then
        continue
      fi
      if [[ "$arn" =~ ^arn:[^:]+:(iam|s3|budgets): ]]; then
        ignorados=$((ignorados + 1))
      elif [[ "$arn" =~ ^arn:[^:]+:ecs:[^:]*:[^:]*:task-definition/ ]]; then
        aviso "ecs-task-definition-inativa $arn (o provider só a desregistra: não custa)"
      elif [[ "$arn" =~ ^arn:[^:]+:ecs:[^:]*:[^:]*:task/ ]]; then
        if estado="$(estado_tarefa "$arn")"; then
          if [[ "$estado" == "parada" ]]; then
            aviso "ecs-tarefa-parada $arn (STOPPED ou inexistente: não custa)"
          else
            residuo recurso-com-tag "$arn"
          fi
        fi
      else
        residuo recurso-com-tag "$arn"
      fi
    done <<< "$arns"
    if [[ $ignorados -gt 0 ]]; then
      aviso "$ignorados recurso(s) com a tag do projeto em s3/iam/budgets ignorado(s): são do bootstrap, que não deveria usar a tag project"
    fi
  else
    erro "resourcegroupstaggingapi get-resources: resposta em formato inesperado"
  fi
fi

erros=$(wc -l < "$ERROS_TMP" | tr -d " ")
echo
if [[ $erros -gt 0 ]]; then
  echo "Verificação de zerado: NAO CONCLUIDA ($residuos achado(s), $erros erro(s) de chamada)" >&2
  exit 2
fi
if [[ $residuos -gt 0 ]]; then
  echo "Verificação de zerado: REPROVADA ($residuos achado(s); o mesmo recurso pode aparecer por mais de uma via)" >&2
  exit 1
fi
echo "Verificação de zerado: APROVADA (nada sobrando na região $REGIAO)"
