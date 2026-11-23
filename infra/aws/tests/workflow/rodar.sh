#!/usr/bin/env bash
# Checagens estruturais do .github/workflows/infra-aws.yml, só com bash e awk (sem ferramenta nova).
# Cada regra vira uma função que recebe o caminho do workflow; os casos mutantes copiam o arquivo real,
# estragam um ponto e exigem que a regra reprove.
# Uso: tests/workflow/rodar.sh
set -uo pipefail

dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
workflow="${WORKFLOW:-$dir/../../../../.github/workflows/infra-aws.yml}"
raiz="$(mktemp -d)"
trap 'rm -rf "$raiz"' EXIT

# Imprime "job|passo|continue-on-error" para cada passo cujo `run` chama o script informado.
# Um passo começa em "      - name:" (6 espaços) e um job em "  nome:" (2 espaços) debaixo de `jobs:`.
passos_que_chamam() { # arquivo script
  awk -v alvo="$2" '
    function fecha() {
      if (passo != "" && chama) { print job "|" passo "|" (coe == "" ? "ausente" : coe) }
      passo = ""; chama = 0; coe = ""
    }
    /^jobs:/ { em_jobs = 1; next }
    em_jobs && /^  [a-z][a-z0-9_-]*:[[:space:]]*$/ { fecha(); job = $1; sub(/:$/, "", job); next }
    /^      - name:/ { fecha(); passo = $0; sub(/^      - name:[[:space:]]*/, "", passo); gsub(/"/, "", passo); next }
    passo != "" && /^        continue-on-error:/ { coe = $2; next }
    passo != "" && /^        run:/ && index($0, alvo) { chama = 1; next }
    passo != "" && /^          / && index($0, alvo) { chama = 1 }
    END { fecha() }
  ' "$1"
}

# Regra: nos jobs destruir, limpar e agendado, o passo que chama parar-migracao.sh existe e tem
# `continue-on-error: true`. Sem isso, uma falha dele pularia o `terraform destroy`.
regra_parar_migracao_nao_bloqueia_o_destroy() {
  local arquivo="$1" job achados linha ok=0
  achados="$(passos_que_chamam "$arquivo" "parar-migracao.sh")"
  for job in destruir limpar agendado; do
    linha="$(grep "^$job|" <<< "$achados" || true)"
    if [[ -z "$linha" ]]; then
      echo "  o job '$job' não tem passo que chame parar-migracao.sh"
      ok=1
    elif [[ "$linha" != *"|true" ]]; then
      echo "  job '$job', passo '$(cut -d'|' -f2 <<< "$linha")': falta continue-on-error: true"
      ok=1
    fi
  done
  return $ok
}

# Regra: o agendado destrói só pelo script (plano salvo e checagem do TTL no mesmo snapshot), nunca por um
# `terraform destroy` direto, e só confere a conta se o script confirmou que destruiu.
regra_agendado_destroi_pelo_script() {
  local arquivo="$1" ok=0
  if [[ -z "$(passos_que_chamam "$arquivo" "destruir-se-vencido.sh" | grep '^agendado|' || true)" ]]; then
    echo "  o job 'agendado' não chama destruir-se-vencido.sh"
    ok=1
  fi
  if awk '/^  agendado:/ { dentro = 1; next } dentro && /^  [a-z]/ { dentro = 0 } dentro && /terraform destroy/ { achou = 1 } END { exit !achou }' "$arquivo"; then
    echo "  o job 'agendado' ainda chama 'terraform destroy' direto"
    ok=1
  fi
  if ! awk '/^  agendado:/ { dentro = 1; next } dentro && /^  [a-z]/ { dentro = 0 }
    dentro && /^      - name:/ { condicao = "" }
    dentro && /^        if:/ { condicao = $0 }
    dentro && /verificar-zerado-apos-destroy.sh/ { visto = 1; if (condicao !~ /destruiu/) ruim = 1 }
    END { exit !(visto && !ruim) }' "$arquivo"; then
    echo "  a verificação de zerado do agendado depois do destroy não depende do output 'destruiu'"
    ok=1
  fi
  return $ok
}

# Mutante: apaga as linhas `continue-on-error` do job indicado (ou de todos, se JOB = "*").
sem_continue_on_error() { # arquivo job
  awk -v alvo="$2" '
    /^  [a-z][a-z0-9_-]*:[[:space:]]*$/ { job = $1; sub(/:$/, "", job) }
    /^        continue-on-error:/ && (alvo == "*" || job == alvo) { next }
    { print }
  ' "$1"
}

falhas=0
total=0

# caso NOME ESPERADO(0 aprova, 1 reprova) FUNCAO ARQUIVO [TRECHO que deve aparecer na saída]
caso() {
  local nome="$1" esperado="$2" regra="$3" arquivo="$4" trecho="${5:-}" saida codigo motivo=""
  if [[ -n "${FILTRO:-}" && ! "$nome" =~ $FILTRO ]]; then
    return 0
  fi
  total=$((total + 1))
  saida="$("$regra" "$arquivo" 2>&1)"
  codigo=$?
  if [[ $codigo -ne $esperado ]]; then
    motivo="esperava saída $esperado, saiu $codigo"
  elif [[ -n "$trecho" && "$saida" != *"$trecho"* ]]; then
    motivo="a saída não traz '$trecho'"
  fi
  if [[ -n "$motivo" ]]; then
    printf '%-62s FALHOU: %s\n' "$nome" "$motivo"
    printf '%s\n' "$saida" | sed 's/^/    | /'
    falhas=$((falhas + 1))
  else
    printf '%-62s ok\n' "$nome"
  fi
}

# ---- parar-migracao.sh não pode pular o destroy ----
caso workflow_atual_parar_migracao_tem_continue_on_error 0 regra_parar_migracao_nao_bloqueia_o_destroy "$workflow"

for job in destruir limpar agendado; do
  sem_continue_on_error "$workflow" "$job" > "$raiz/sem-coe-$job.yml"
  caso "sem_continue_on_error_no_$job" 1 regra_parar_migracao_nao_bloqueia_o_destroy "$raiz/sem-coe-$job.yml" "job '$job'"
done

sed 's/^\( *\)continue-on-error: true/\1continue-on-error: false/' "$workflow" > "$raiz/coe-false.yml"
caso continue_on_error_false_reprova 1 regra_parar_migracao_nao_bloqueia_o_destroy "$raiz/coe-false.yml" "falta continue-on-error: true"

sed 's#bash scripts/parar-migracao.sh#true#' "$workflow" > "$raiz/sem-passo.yml"
caso job_sem_o_passo_de_parar_migracao_reprova 1 regra_parar_migracao_nao_bloqueia_o_destroy "$raiz/sem-passo.yml" "não tem passo que chame parar-migracao.sh"

# ---- agendado destrói pelo script ----
caso workflow_atual_agendado_destroi_pelo_script 0 regra_agendado_destroi_pelo_script "$workflow"

sed 's#bash scripts/destruir-se-vencido.sh#terraform destroy -auto-approve#' "$workflow" > "$raiz/destroy-direto.yml"
caso agendado_com_terraform_destroy_direto_reprova 1 regra_agendado_destroi_pelo_script "$raiz/destroy-direto.yml" "terraform destroy"

sed 's/ && steps.destruir.outputs.destruiu == .true.//' "$workflow" > "$raiz/zerado-sempre.yml"
caso agendado_com_zerado_sem_depender_do_destroy_reprova 1 regra_agendado_destroi_pelo_script "$raiz/zerado-sempre.yml" "destruiu"

echo
if [[ $falhas -gt 0 ]]; then
  echo "$falhas de $total casos falharam."
  exit 1
fi
echo "$total casos ok."
