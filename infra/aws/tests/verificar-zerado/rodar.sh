#!/usr/bin/env bash
# Testa a verificação de zerado (camada 4) com um shim do `aws`: nenhuma chamada sai da máquina.
# O shim devolve "conta vazia" para toda operação e cada caso troca só as respostas que importam
# (arquivos <servico>_<operacao>.json ou .erro no diretório do caso). Precisa de bash e jq.
# Uso: tests/verificar-zerado/rodar.sh
#
# Cada caso declara: código de saída esperado, trecho que precisa estar na saída, trecho que NÃO pode
# estar e, opcionalmente, a operação que precisa (+) ou não pode (-) ter sido chamada.
set -uo pipefail

dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
script="${VERIFICAR_ZERADO:-$dir/../../scripts/verificar-zerado.sh}"
raiz="$(mktemp -d)"
trap 'rm -rf "$raiz"' EXIT

mkdir -p "$raiz/bin"
cat > "$raiz/bin/aws" << 'SHIM'
#!/usr/bin/env bash
# Shim do aws para os testes. Lê a resposta de $SHIM_DIR/<servico>_<operacao>.json; sem arquivo,
# devolve a resposta vazia da operação. Um arquivo .erro faz a chamada falhar como a CLI falha.
servico="$1"
operacao="$2"
echo "$servico $operacao $*" >> "$SHIM_DIR/chamadas.log"
if [[ -f "$SHIM_DIR/${servico}_${operacao}.erro" ]]; then
  echo "An error occurred ($(cat "$SHIM_DIR/${servico}_${operacao}.erro")) when calling the ${operacao} operation: shim" >&2
  exit 254
fi
if [[ -f "$SHIM_DIR/${servico}_${operacao}.json" ]]; then
  cat "$SHIM_DIR/${servico}_${operacao}.json"
  exit 0
fi
case "$servico $operacao" in
  "sts get-caller-identity") echo '{"Account":"111111111111"}' ;;
  "ecs list-clusters") echo '{"clusterArns":[]}' ;;
  "ecs describe-clusters") echo '{"clusters":[],"failures":[]}' ;;
  "ecs list-services") echo '{"serviceArns":[]}' ;;
  "ecs list-tasks") echo '{"taskArns":[]}' ;;
  "ecs describe-tasks") echo '{"tasks":[],"failures":[]}' ;;
  "elbv2 describe-load-balancers") echo '{"LoadBalancers":[]}' ;;
  "elbv2 describe-target-groups") echo '{"TargetGroups":[]}' ;;
  "rds describe-db-instances") echo '{"DBInstances":[]}' ;;
  "rds describe-db-snapshots") echo '{"DBSnapshots":[]}' ;;
  "rds describe-db-instance-automated-backups") echo '{"DBInstanceAutomatedBackups":[]}' ;;
  "ec2 describe-nat-gateways") echo '{"NatGateways":[]}' ;;
  "ec2 describe-addresses") echo '{"Addresses":[]}' ;;
  "ec2 describe-network-interfaces") echo '{"NetworkInterfaces":[]}' ;;
  "ec2 describe-instances") echo '{"Reservations":[]}' ;;
  "ec2 describe-vpc-endpoints") echo '{"VpcEndpoints":[]}' ;;
  "secretsmanager list-secrets") echo '{"SecretList":[]}' ;;
  "logs describe-log-groups") echo '{"logGroups":[]}' ;;
  "kms list-keys") echo '{"Keys":[]}' ;;
  "resourcegroupstaggingapi get-resources") echo '{"ResourceTagMappingList":[]}' ;;
  *) echo "shim: operação sem resposta padrão: $servico $operacao" >&2; exit 99 ;;
esac
SHIM
chmod +x "$raiz/bin/aws"

REGIAO_TESTE="us-east-1"
CONTA="111111111111"
ARN_CLUSTER="arn:aws:ecs:$REGIAO_TESTE:$CONTA:cluster/renda-fixa-api"
ARN_TAREFA="arn:aws:ecs:$REGIAO_TESTE:$CONTA:task/renda-fixa-api/0123456789abcdef"
ARN_TASKDEF="arn:aws:ecs:$REGIAO_TESTE:$CONTA:task-definition/renda-fixa-api:3"
ARN_LB="arn:aws:elasticloadbalancing:$REGIAO_TESTE:$CONTA:loadbalancer/app/renda-fixa-api/abc"

falhas=0
total=0

# caso NOME SAIDA_ESPERADA DEVE_CONTER NAO_DEVE_CONTER [+operacao|-operacao ...]
# As respostas do caso são gravadas em "$d" pela função preparar_<nome> (definida antes da chamada).
caso() {
  local nome="$1" saida_esperada="$2" contem="$3" nao_contem="$4"
  shift 4
  local d="$raiz/caso-$nome" saida codigo motivo="" exigencia operacao
  # FILTRO (regex) roda só os casos cujo nome casa: cada caso chama o jq dezenas de vezes.
  if [[ -n "${FILTRO:-}" && ! "$nome" =~ $FILTRO ]]; then
    return 0
  fi
  total=$((total + 1))
  mkdir -p "$d"
  : > "$d/chamadas.log"
  "preparar_$nome" "$d"

  saida="$(SHIM_DIR="$d" PATH="$raiz/bin:$PATH" AWS_REGION="$REGIAO_TESTE" bash "$script" 2>&1)"
  codigo=$?

  if [[ $codigo -ne $saida_esperada ]]; then
    motivo="esperava saída $saida_esperada, saiu $codigo"
  elif [[ -n "$contem" && "$saida" != *"$contem"* ]]; then
    motivo="a saída não traz '$contem'"
  elif [[ -n "$nao_contem" && "$saida" == *"$nao_contem"* ]]; then
    motivo="a saída não deveria trazer '$nao_contem'"
  else
    for exigencia in "$@"; do
      operacao="${exigencia:1}"
      if [[ "${exigencia:0:1}" == "+" ]] && ! grep -q "^$operacao " "$d/chamadas.log"; then
        motivo="a operação '$operacao' deveria ter sido chamada"
      elif [[ "${exigencia:0:1}" == "-" ]] && grep -q "^$operacao " "$d/chamadas.log"; then
        motivo="a operação '$operacao' não deveria ter sido chamada"
      fi
    done
  fi

  if [[ -n "$motivo" ]]; then
    printf '%-58s FALHOU: %s\n' "$nome" "$motivo"
    printf '%s\n' "$saida" | sed 's/^/    | /'
    falhas=$((falhas + 1))
  else
    printf '%-58s ok\n' "$nome"
  fi
}

etiquetados() { # dir arn...: resposta da tagging API com os ARNs dados
  local d="$1" arn lista=""
  shift
  for arn in "$@"; do
    lista+="{\"ResourceARN\":\"$arn\",\"Tags\":[{\"Key\":\"project\",\"Value\":\"renda-fixa-api\"}]},"
  done
  echo "{\"ResourceTagMappingList\":[${lista%,}]}" > "$d/resourcegroupstaggingapi_get-resources.json"
}

cluster_listado() { # dir status
  echo "{\"clusterArns\":[\"$ARN_CLUSTER\"]}" > "$1/ecs_list-clusters.json"
  echo "{\"clusters\":[{\"clusterArn\":\"$ARN_CLUSTER\",\"status\":\"$2\"}],\"failures\":[]}" > "$1/ecs_describe-clusters.json"
}

# ---- Base ----
preparar_conta_vazia_aprova() { :; }
caso conta_vazia_aprova 0 "APROVADA" "RESIDUO"

# ---- I1: varredura por tag ----
preparar_tag_task_definition_vira_aviso() {
  etiquetados "$1" "$ARN_TASKDEF"
}
caso tag_task_definition_vira_aviso 0 "AVISO" "RESIDUO" "-ecs describe-tasks"

preparar_tag_tarefa_stopped_vira_aviso() {
  etiquetados "$1" "$ARN_TAREFA"
  echo '{"tasks":[{"lastStatus":"STOPPED"}],"failures":[]}' > "$1/ecs_describe-tasks.json"
}
caso tag_tarefa_stopped_vira_aviso 0 "AVISO" "RESIDUO" "+ecs describe-tasks"

preparar_tag_tarefa_inexistente_vira_aviso() {
  etiquetados "$1" "$ARN_TAREFA"
  echo '{"tasks":[],"failures":[{"arn":"x","reason":"MISSING"}]}' > "$1/ecs_describe-tasks.json"
}
caso tag_tarefa_inexistente_vira_aviso 0 "AVISO" "RESIDUO"

preparar_tag_tarefa_de_cluster_apagado_vira_aviso() {
  etiquetados "$1" "$ARN_TAREFA"
  echo "ClusterNotFoundException" > "$1/ecs_describe-tasks.erro"
}
caso tag_tarefa_de_cluster_apagado_vira_aviso 0 "AVISO" "RESIDUO"

preparar_tag_tarefa_running_reprova() {
  etiquetados "$1" "$ARN_TAREFA"
  echo '{"tasks":[{"lastStatus":"RUNNING"}],"failures":[]}' > "$1/ecs_describe-tasks.json"
}
caso tag_tarefa_running_reprova 1 "RESIDUO recurso-com-tag $ARN_TAREFA" ""

preparar_tag_tarefa_pending_reprova() {
  etiquetados "$1" "$ARN_TAREFA"
  echo '{"tasks":[{"lastStatus":"PENDING"}],"failures":[]}' > "$1/ecs_describe-tasks.json"
}
caso tag_tarefa_pending_reprova 1 "RESIDUO recurso-com-tag" ""

preparar_tag_tarefa_describe_com_erro_sai_2() {
  etiquetados "$1" "$ARN_TAREFA"
  echo "AccessDeniedException" > "$1/ecs_describe-tasks.erro"
}
caso tag_tarefa_describe_com_erro_sai_2 2 "AccessDeniedException" "APROVADA"

preparar_tag_tarefa_resposta_ambigua_sai_2() {
  etiquetados "$1" "$ARN_TAREFA"
  echo '{"tasks":[],"failures":[]}' > "$1/ecs_describe-tasks.json"
}
caso tag_tarefa_resposta_ambigua_sai_2 2 "ERRO" "APROVADA"

preparar_tag_tarefa_sem_status_sai_2() {
  etiquetados "$1" "$ARN_TAREFA"
  echo '{"tasks":[{"taskArn":"x"}],"failures":[]}' > "$1/ecs_describe-tasks.json"
}
caso tag_tarefa_sem_status_sai_2 2 "ERRO" "APROVADA"

preparar_tag_task_definition_e_load_balancer_reprova_so_o_lb() {
  etiquetados "$1" "$ARN_TASKDEF" "$ARN_LB"
}
caso tag_task_definition_e_load_balancer_reprova_so_o_lb 1 "RESIDUO recurso-com-tag $ARN_LB" "RESIDUO recurso-com-tag $ARN_TASKDEF"

preparar_tag_tarefa_stopped_e_cluster_reprova_so_o_cluster() {
  etiquetados "$1" "$ARN_TAREFA" "$ARN_CLUSTER"
  echo '{"tasks":[{"lastStatus":"STOPPED"}],"failures":[]}' > "$1/ecs_describe-tasks.json"
}
caso tag_tarefa_stopped_e_cluster_reprova_so_o_cluster 1 "RESIDUO recurso-com-tag $ARN_CLUSTER" "RESIDUO recurso-com-tag $ARN_TAREFA"

# ---- S4: cluster INACTIVE listado por list-clusters ----
preparar_cluster_inactive_nao_vira_residuo() {
  cluster_listado "$1" INACTIVE
  # Se o script listasse serviços mesmo assim, isto viraria erro (saída 2).
  echo "ClusterNotFoundException" > "$1/ecs_list-services.erro"
}
caso cluster_inactive_nao_vira_residuo 0 "AVISO" "RESIDUO" "+ecs describe-clusters" "-ecs list-services" "-ecs list-tasks"

preparar_cluster_ausente_no_describe_vira_aviso() {
  echo "{\"clusterArns\":[\"$ARN_CLUSTER\"]}" > "$1/ecs_list-clusters.json"
  echo '{"clusters":[],"failures":[{"arn":"x","reason":"MISSING"}]}' > "$1/ecs_describe-clusters.json"
}
caso cluster_ausente_no_describe_vira_aviso 0 "AVISO" "RESIDUO" "-ecs list-services"

preparar_cluster_active_vazio_e_residuo() {
  cluster_listado "$1" ACTIVE
}
caso cluster_active_vazio_e_residuo 1 "RESIDUO ecs-cluster $ARN_CLUSTER" "" "+ecs list-services" "+ecs list-tasks"

preparar_cluster_active_com_servico_e_tarefa_reprova() {
  cluster_listado "$1" ACTIVE
  echo "{\"serviceArns\":[\"arn:aws:ecs:$REGIAO_TESTE:$CONTA:service/renda-fixa-api/renda-fixa-api\"]}" > "$1/ecs_list-services.json"
  echo "{\"taskArns\":[\"$ARN_TAREFA\"]}" > "$1/ecs_list-tasks.json"
}
caso cluster_active_com_servico_e_tarefa_reprova 1 "RESIDUO ecs-servico" ""

preparar_cluster_deprovisioning_ainda_e_residuo() {
  cluster_listado "$1" DEPROVISIONING
}
caso cluster_deprovisioning_ainda_e_residuo 1 "RESIDUO ecs-cluster" ""

preparar_cluster_describe_com_erro_sai_2() {
  echo "{\"clusterArns\":[\"$ARN_CLUSTER\"]}" > "$1/ecs_list-clusters.json"
  echo "AccessDeniedException" > "$1/ecs_describe-clusters.erro"
}
caso cluster_describe_com_erro_sai_2 2 "AccessDeniedException" "APROVADA"

preparar_cluster_describe_sem_o_cluster_sai_2() {
  echo "{\"clusterArns\":[\"$ARN_CLUSTER\"]}" > "$1/ecs_list-clusters.json"
  echo '{"clusters":[],"failures":[]}' > "$1/ecs_describe-clusters.json"
}
caso cluster_describe_sem_o_cluster_sai_2 2 "ERRO" "APROVADA"

echo
if [[ $falhas -gt 0 ]]; then
  echo "$falhas de $total casos falharam."
  exit 1
fi
echo "$total casos ok."
