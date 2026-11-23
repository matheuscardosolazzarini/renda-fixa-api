#!/usr/bin/env bash
# Destroy do agendado, sem a corrida com um `subir` que esteja prorrogando o ambiente.
#
# A corrida: o agendado lê expires_at (vencido) e, antes de o destroy pegar o lock, um `subir` prorroga o prazo.
# O destroy lia o state de novo e destruía o ambiente recém-prorrogado. O mecanismo daqui tem três partes:
#   1. terraform plan -destroy -out=<arquivo>;
#   2. o prazo é lido da TAG `expires_at` DO CLUSTER (module.app.aws_ecs_cluster.this) no prior_state desse plano,
#      que vem do state atualizado. NÃO do output `expires_at`: num `plan -destroy` o Terraform recalcula os outputs
#      da raiz com as -var da chamada, e o output devolveria o EXPIRES_AT velho que o agendado passou. Só segue se
#      a tag estiver vencida; cluster ou tag ausente ou inválida conta como vencido, e tag além de agora + 12 h
#      (limite da validation de variables.tf, mais 10 min de folga) também, pois só pode ter sido editada fora do
#      Terraform;
#   3. repete a guarda da fila manual (mesma lógica do passo "Guarda" do agendado, no YAML) logo antes do apply e
#      aplica o plano salvo. Se um `subir` gravar o state depois do plano, o Terraform recusa ("Saved plan is
#      stale") e nada é destruído: este script sai 1 com a explicação.
# Um `subir` que prorrogue ANTES do plano é visto pela tag (passo 2); um que grave DEPOIS do plano deixa o plano
# obsoleto (passo 3).
#
# Janela que sobra (aceita, ver o README): o agendado aplica o destroy antes de o `subir` gravar qualquer coisa.
# O ambiente estava vencido e intocado; o `subir` fica vermelho com "Saved plan is stale" e basta rodá-lo de novo.
#
# Entradas (ambiente): AWS_REGION, EXPIRES_AT e SHA (valores lidos antes, só para as -var do plano),
# REPOSITORIO (dono/repo, para o gh) e GH_TOKEN (o gh); todos obrigatórios. MARCACAO_EXPIRES_AT é opcional.
# TTL_ENDERECO e TTL_CAMINHO (opcionais, ganchos de teste) trocam o recurso e o atributo do prazo; o padrão é o
# cluster real e ["tags","expires_at"]. Roda em infra/aws.
# Saída: 0 = destruiu, ou não havia o que destruir agora (ver o output `destruiu` em $GITHUB_OUTPUT);
# 1 = erro, ou plano obsoleto. Em qualquer saída diferente de `destruiu=true`, a verificação de zerado não deve rodar.
set -euo pipefail

: "${AWS_REGION:?defina AWS_REGION}"
: "${EXPIRES_AT:?defina EXPIRES_AT}"
: "${SHA:?defina SHA}"
: "${REPOSITORIO:?defina REPOSITORIO}"
: "${GH_TOKEN:?defina GH_TOKEN}"

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
plano="$tmp/destroy.tfplan"
log_apply="$tmp/apply.log"

# O output `destruiu` decide, no workflow, se a verificação de zerado roda: só depois de um destroy aplicado.
registrar() {
  if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
    echo "destruiu=$1" >> "$GITHUB_OUTPUT"
  fi
}

filtro_ttl="$(dirname "${BASH_SOURCE[0]}")/expires-at-do-cluster.jq"
ttl_endereco="${TTL_ENDERECO:-module.app.aws_ecs_cluster.this}"
ttl_caminho="${TTL_CAMINHO:-[\"tags\",\"expires_at\"]}"

# Limite da tag: o mesmo de `variable "expires_at"` em variables.tf (no máximo 12 h à frente de agora), mais uma folga
# de 10 min para o relógio do runner e a duração do ciclo. Tag acima disso só pode ter sido gravada fora do
# Terraform (a validation a recusa): conta como inválida, portanto vencida, para que editar a tag à mão não
# deixe o ambiente vivo para sempre.
limite_horas=12
folga_min=10

# 1. Plano de destroy salvo, com as mesmas -var do destroy direto de antes.
terraform plan -destroy -input=false -lock-timeout=5m -var-file=environments/aws.tfvars \
  -var "region=$AWS_REGION" -var "expires_at=$EXPIRES_AT" -var "image_sha=$SHA" -var "service_desired_count=0" \
  -out="$plano"

# 2. TTL da tag do cluster no snapshot que o plano vai destruir (ver o cabeçalho: o output não serve). O JSON do
# plano passa direto pelo jq: não é impresso nem guardado.
if ! expires="$(terraform show -json "$plano" | jq -r -f "$filtro_ttl" --arg endereco "$ttl_endereco" --argjson caminho "$ttl_caminho")"; then
  echo "ERRO: não foi possível ler o plano de destroy (terraform show -json): nada foi destruído." >&2
  registrar false
  exit 1
fi

vencido=true
restante=0
if [[ "$expires" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9:.]+(Z|[+-][0-9]{2}:[0-9]{2})$ ]] \
  && alvo="$(date -u -d "$expires" +%s 2> /dev/null)"; then
  agora="$(date +%s)"
  restante=$((alvo - agora))
  if ((alvo > agora + limite_horas * 3600 + folga_min * 60)); then
    echo "::warning::tag expires_at do cluster ($expires) passa do limite de ${limite_horas} h da validation de variables.tf (mais ${folga_min} min de folga) e pode ter sido editada fora do Terraform: conta como vencida."
    expires="${MARCACAO_EXPIRES_AT:-1970-01-01T00:00:00Z}"
  elif ((restante > 0)); then
    vencido=false
  fi
else
  echo "::warning::tag expires_at do cluster ausente ou inválida no state: o ambiente conta como vencido."
  expires="${MARCACAO_EXPIRES_AT:-1970-01-01T00:00:00Z}"
fi

if [[ "$vencido" == "false" ]]; then
  echo "O ambiente foi prorrogado depois da leitura (expira em $expires, restam $((restante / 60)) min): nada será destruído."
  registrar false
  exit 0
fi

# 3. Fila manual, de novo: um `subir` ou `destruir` manual entrou depois da guarda do começo do ciclo.
ativas=0
for situacao in in_progress queued waiting requested pending; do
  quantas="$(gh run list --repo "$REPOSITORIO" --workflow infra-aws.yml --event workflow_dispatch \
    --status "$situacao" --json databaseId --jq length)"
  ativas=$((ativas + quantas))
done
if ((ativas > 0)); then
  echo "::warning::Há $ativas execução(ões) manual(is) do infra-aws em andamento ou na fila: este ciclo não destrói nada."
  registrar false
  exit 0
fi

# 4. Aplica exatamente o plano salvo. O tee mostra o andamento (um destroy leva minutos) e guarda o texto
# para reconhecer a recusa do plano obsoleto; PIPESTATUS[0] é o código do terraform.
set +e
terraform apply -input=false -lock-timeout=5m "$plano" 2>&1 | tee "$log_apply"
codigo="${PIPESTATUS[0]}"
set -e

if [[ "$codigo" -ne 0 ]]; then
  registrar false
  if grep -q 'Saved plan is stale' "$log_apply"; then
    echo "ERRO: o state foi alterado depois do plano (provavelmente um 'subir' prorrogou o ambiente): nada foi destruído." >&2
    echo "Se o ambiente continuar vencido, o próximo ciclo do agendado o destrói; se foi um 'subir' que falhou, rode-o de novo." >&2
  else
    echo "ERRO: o terraform apply do plano de destroy falhou (código $codigo): confira o log acima." >&2
  fi
  exit 1
fi

registrar true
