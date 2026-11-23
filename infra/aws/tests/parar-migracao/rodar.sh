#!/usr/bin/env bash
# Testa scripts/parar-migracao.sh com um shim do `aws`: nenhuma chamada sai da máquina.
# O shim lê a resposta de $SHIM_DIR (ecs_list-tasks.txt, ecs_stop-task.txt) e, para fazer a chamada falhar,
# um arquivo .erro (código da AWS) ou .erro-cru (texto da CLI sem código, como rede e timeout).
# Uso: tests/parar-migracao/rodar.sh
#
# Cada caso declara: código de saída esperado, trecho que precisa estar na saída, trecho que NÃO pode estar
# e, opcionalmente, a operação que precisa (+) ou não pode (-) ter sido chamada.
set -uo pipefail

dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
script="${PARAR_MIGRACAO:-$dir/../../scripts/parar-migracao.sh}"
raiz="$(mktemp -d)"
trap 'rm -rf "$raiz"' EXIT

mkdir -p "$raiz/bin"
cat > "$raiz/bin/aws" << 'SHIM'
#!/usr/bin/env bash
servico="$1"
operacao="$2"
echo "$servico $operacao $*" >> "$SHIM_DIR/chamadas.log"
if [[ -f "$SHIM_DIR/${servico}_${operacao}.erro" ]]; then
  echo "An error occurred ($(cat "$SHIM_DIR/${servico}_${operacao}.erro")) when calling the ${operacao} operation: shim" >&2
  exit 254
fi
if [[ -f "$SHIM_DIR/${servico}_${operacao}.erro-cru" ]]; then
  cat "$SHIM_DIR/${servico}_${operacao}.erro-cru" >&2
  exit 255
fi
# stop-task falha só para a tarefa listada em stop-task.falha (as outras seguem normalmente).
if [[ "$operacao" == "stop-task" && -f "$SHIM_DIR/stop-task.falha" ]] && [[ "$*" == *"$(cat "$SHIM_DIR/stop-task.falha")"* ]]; then
  echo "An error occurred (AccessDeniedException) when calling the StopTask operation: shim" >&2
  exit 254
fi
if [[ -f "$SHIM_DIR/${servico}_${operacao}.txt" ]]; then
  cat "$SHIM_DIR/${servico}_${operacao}.txt"
  exit 0
fi
case "$servico $operacao" in
  "ecs list-tasks") echo "" ;;
  "ecs stop-task") echo "{}" ;;
  *) echo "shim: operação sem resposta padrão: $servico $operacao" >&2; exit 99 ;;
esac
SHIM
chmod +x "$raiz/bin/aws"

ARN_A="arn:aws:ecs:us-east-1:111111111111:task/renda-fixa-api/aaaa1111"
ARN_B="arn:aws:ecs:us-east-1:111111111111:task/renda-fixa-api/bbbb2222"

falhas=0
total=0

# caso NOME SAIDA_ESPERADA DEVE_CONTER NAO_DEVE_CONTER [+operacao|-operacao ...]
# As respostas do caso são gravadas em "$d" pela função preparar_<nome> (definida antes da chamada).
# ARGS_SCRIPT (opcional) são os argumentos passados ao script naquele caso.
caso() {
  local nome="$1" saida_esperada="$2" contem="$3" nao_contem="$4"
  shift 4
  local d="$raiz/caso-$nome" saida codigo motivo="" exigencia operacao
  if [[ -n "${FILTRO:-}" && ! "$nome" =~ $FILTRO ]]; then
    return 0
  fi
  total=$((total + 1))
  mkdir -p "$d"
  : > "$d/chamadas.log"
  ARGS_SCRIPT=""
  "preparar_$nome" "$d"

  # shellcheck disable=SC2086 # ARGS_SCRIPT é uma lista simples de palavras, definida pelo caso
  saida="$(SHIM_DIR="$d" PATH="$raiz/bin:$PATH" AWS_REGION="us-east-1" bash "$script" $ARGS_SCRIPT 2>&1)"
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

# ---- Sem tarefa ----
preparar_sem_tarefas_sai_0() { :; }
caso sem_tarefas_sai_0 0 "Nenhuma tarefa de migração ativa" "ERRO" "-ecs stop-task"

preparar_resposta_none_da_cli_sai_0() { echo "None" > "$1/ecs_list-tasks.txt"; }
caso resposta_none_da_cli_sai_0 0 "Nenhuma tarefa de migração ativa" "ERRO" "-ecs stop-task"

preparar_cluster_inexistente_sai_0() { echo "ClusterNotFoundException" > "$1/ecs_list-tasks.erro"; }
caso cluster_inexistente_sai_0 0 "não existe" "ERRO" "-ecs stop-task"

# ---- Tarefas em execução ----
preparar_tarefa_em_execucao_e_parada() { echo "$ARN_A" > "$1/ecs_list-tasks.txt"; }
caso tarefa_em_execucao_e_parada 0 "Tarefa de migração parada: $ARN_A" "ERRO" "+ecs stop-task"

preparar_duas_tarefas_em_execucao_sao_paradas() { printf '%s\t%s\n' "$ARN_A" "$ARN_B" > "$1/ecs_list-tasks.txt"; }
caso duas_tarefas_em_execucao_sao_paradas 0 "Tarefa de migração parada: $ARN_B" "ERRO"

preparar_consulta_usa_a_familia_e_o_cluster_informado() { ARGS_SCRIPT="outro-cluster"; }
caso consulta_usa_a_familia_e_o_cluster_informado 0 "cluster outro-cluster" "ERRO" "+ecs list-tasks"

# ---- Falha ao listar ----
preparar_falha_com_codigo_da_aws_mostra_o_codigo() { echo "AccessDeniedException" > "$1/ecs_list-tasks.erro"; }
caso falha_com_codigo_da_aws_mostra_o_codigo 1 "ERRO ecs list-tasks: AccessDeniedException" "" "-ecs stop-task"

preparar_falha_sem_codigo_cita_a_causa() {
  echo 'Could not connect to the endpoint URL: "https://ecs.us-east-1.amazonaws.com/"' > "$1/ecs_list-tasks.erro-cru"
}
caso falha_sem_codigo_cita_a_causa 1 "ERRO ecs list-tasks: falha sem código da AWS (rede, credencial, timeout ou região)" "" "-ecs stop-task"

preparar_regiao_ausente_cita_a_regiao() {
  echo 'aws: [ERROR]: An error occurred (NoRegion): You must specify a region. You can also configure your region by running "aws configure".' > "$1/ecs_list-tasks.erro-cru"
}
caso regiao_ausente_cita_a_regiao 1 "região" "falha sem código" "-ecs stop-task"

# ---- Falha ao parar ----
preparar_falha_ao_parar_sem_codigo_cita_a_causa() {
  echo "$ARN_A" > "$1/ecs_list-tasks.txt"
  echo 'Read timeout on endpoint URL: "https://ecs.us-east-1.amazonaws.com/"' > "$1/ecs_stop-task.erro-cru"
}
caso falha_ao_parar_sem_codigo_cita_a_causa 1 "ERRO ecs stop-task $ARN_A: falha sem código da AWS (rede, credencial, timeout ou região)" ""

preparar_falha_em_uma_tarefa_nao_impede_parar_a_outra() {
  printf '%s\t%s\n' "$ARN_A" "$ARN_B" > "$1/ecs_list-tasks.txt"
  echo "aaaa1111" > "$1/stop-task.falha"
}
caso falha_em_uma_tarefa_nao_impede_parar_a_outra 1 "Tarefa de migração parada: $ARN_B" "" "+ecs stop-task"

echo
if [[ $falhas -gt 0 ]]; then
  echo "$falhas de $total casos falharam."
  exit 1
fi
echo "$total casos ok."
