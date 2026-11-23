#!/usr/bin/env bash
# Testa scripts/destruir-se-vencido.sh (o destroy do agendado) com `terraform` e `gh` falsos: nenhuma chamada
# sai da máquina. O terraform falso monta o JSON do plano no formato real (ver plano_json) e arquivos .erro /
# .obsoleto fazem o plan, o show ou o apply falharem; o gh falso lê gh_<situacao> (contagem) ou gh.erro.
# No fim há casos com o Terraform REAL (terraform_real_*): config mínima com terraform_data dentro de um módulo.
# Precisa de bash e jq. Uso: tests/agendado/rodar.sh
#
# Cada caso declara: código de saída esperado, trecho que precisa estar na saída, trecho que NÃO pode estar
# e, opcionalmente, o comando que precisa (+) ou não pode (-) ter sido chamado (começo da linha do
# chamadas.log: "terraform plan", "terraform apply", "gh run"). Um caso pode ter ainda uma função
# verificar_<nome> que imprime o motivo da falha, se houver.
set -uo pipefail

dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
script="${DESTRUIR_SE_VENCIDO:-$dir/../../scripts/destruir-se-vencido.sh}"
raiz="$(mktemp -d)"
trap 'rm -rf "$raiz"' EXIT

mkdir -p "$raiz/bin"
cat > "$raiz/bin/terraform" << 'SHIM'
#!/usr/bin/env bash
# Terraform falso. plan grava um arquivo no -out; show -json devolve o plano no formato real; apply exige o arquivo.
echo "terraform $*" >> "$SHIM_DIR/chamadas.log"
case "$1" in
  plan)
    if [[ -f "$SHIM_DIR/plan.erro" ]]; then echo "Error: plan falhou (shim)" >&2; exit 1; fi
    for argumento in "$@"; do
      if [[ "$argumento" == -out=* ]]; then echo "plano-falso" > "${argumento#-out=}"; fi
      if [[ "$argumento" == expires_at=* ]]; then echo "${argumento#expires_at=}" > "$SHIM_DIR/var_expires_at"; fi
    done
    ;;
  show)
    if [[ -f "$SHIM_DIR/show.erro" ]]; then echo "Error: show falhou (shim)" >&2; exit 1; fi
    [[ -f "${@: -1}" ]] || { echo "Error: arquivo de plano inexistente (shim)" >&2; exit 1; }
    if [[ -f "$SHIM_DIR/plano.cru" ]]; then cat "$SHIM_DIR/plano.cru"; exit 0; fi
    # Formato real: num `plan -destroy` o output expires_at da raiz é recalculado com a -var da chamada (nunca
    # reflete o state); o prazo gravado está na tag do cluster, que vem do state atualizado.
    var="$(cat "$SHIM_DIR/var_expires_at")"
    if [[ -f "$SHIM_DIR/sem-cluster" ]]; then
      recursos='[{"address":"module.app.aws_cloudwatch_log_group.this","values":{"retention_in_days":3}}]'
    elif [[ -f "$SHIM_DIR/cluster-sem-tag" ]]; then
      recursos='[{"address":"module.app.aws_ecs_cluster.this","values":{"tags":null,"tags_all":{"project":"renda-fixa-api"}}}]'
    else
      recursos="$(jq -nc --arg tag "$(cat "$SHIM_DIR/tag")" '[{address:"module.app.aws_ecs_cluster.this",values:{tags:{expires_at:$tag},tags_all:{expires_at:$tag,project:"renda-fixa-api"}}}]')"
    fi
    jq -nc --arg var "$var" --argjson recursos "$recursos" '{format_version:"1.2",prior_state:{values:{outputs:{expires_at:{sensitive:false,value:$var}},root_module:{child_modules:[{address:"module.app",resources:$recursos}]}}}}'
    ;;
  apply)
    [[ -f "${@: -1}" ]] || { echo "Error: arquivo de plano inexistente (shim)" >&2; exit 1; }
    if [[ -f "$SHIM_DIR/apply.obsoleto" ]]; then
      echo "Error: Saved plan is stale" >&2
      echo "The given plan file can no longer be applied because the state was changed by another operation after the plan was created." >&2
      exit 1
    fi
    if [[ -f "$SHIM_DIR/apply.erro" ]]; then echo "Error: apply falhou (shim)" >&2; exit 1; fi
    echo "Destroy complete! Resources: 27 destroyed."
    ;;
  *) echo "shim: terraform $1 não esperado" >&2; exit 99 ;;
esac
SHIM
cat > "$raiz/bin/gh" << 'SHIM'
#!/usr/bin/env bash
# gh falso: `gh run list ... --status S ...` imprime a contagem de $SHIM_DIR/gh_S (padrão 0).
echo "gh $*" >> "$SHIM_DIR/chamadas.log"
if [[ -f "$SHIM_DIR/gh.erro" ]]; then echo "gh: HTTP 502 (shim)" >&2; exit 1; fi
situacao=""
while [[ $# -gt 0 ]]; do
  if [[ "$1" == "--status" ]]; then situacao="$2"; fi
  shift
done
if [[ -f "$SHIM_DIR/gh_$situacao" ]]; then cat "$SHIM_DIR/gh_$situacao"; else echo 0; fi
SHIM
chmod +x "$raiz/bin/terraform" "$raiz/bin/gh"

# Prazos relativos a agora: a tag precisa caber no limite de 12 h da validation de variables.tf (mais a folga do
# script), então "prorrogado" é agora + 2 h, e não uma data distante.
agora_mais() { date -u -d "+$1" +%Y-%m-%dT%H:%M:%SZ; }
FUTURO="$(agora_mais "2 hours")"
PASSADO="2020-01-01T00:00:00Z"
MARCACAO="1970-01-01T00:00:00Z"

falhas=0
total=0

# plano_json DIR TAG|-|SEM-TAG: prazo gravado na tag do cluster no state ("-" = state sem o cluster; "SEM-TAG" =
# cluster sem a tag). O output do plano vem da -var (EXPIRES_ATUAL), como no Terraform real.
plano_json() {
  case "$2" in
    "-") : > "$1/sem-cluster" ;;
    "SEM-TAG") : > "$1/cluster-sem-tag" ;;
    *) echo "$2" > "$1/tag" ;;
  esac
}

# caso NOME SAIDA_ESPERADA DEVE_CONTER NAO_DEVE_CONTER [+comando|-comando ...]
# EXPIRES_ATUAL e SHA_ATUAL (opcionais, definidos pelo preparar_) são os valores que o agendado leu antes.
caso() {
  local nome="$1" saida_esperada="$2" contem="$3" nao_contem="$4"
  shift 4
  local d="$raiz/caso-$nome" saida codigo motivo="" exigencia comando
  if [[ -n "${FILTRO:-}" && ! "$nome" =~ $FILTRO ]]; then
    return 0
  fi
  total=$((total + 1))
  mkdir -p "$d"
  : > "$d/chamadas.log"
  : > "$d/github_output"
  EXPIRES_ATUAL="$PASSADO"
  SHA_ATUAL="abc"
  "preparar_$nome" "$d"

  saida="$(cd "$d" && SHIM_DIR="$d" PATH="$raiz/bin:$PATH" GITHUB_OUTPUT="$d/github_output" AWS_REGION=us-east-1 \
    EXPIRES_AT="$EXPIRES_ATUAL" SHA="$SHA_ATUAL" MARCACAO_EXPIRES_AT="$MARCACAO" \
    REPOSITORIO="dono/repo" GH_TOKEN=token-falso bash "$script" 2>&1)"
  codigo=$?

  if [[ $codigo -ne $saida_esperada ]]; then
    motivo="esperava saída $saida_esperada, saiu $codigo"
  elif [[ -n "$contem" && "$saida" != *"$contem"* ]]; then
    motivo="a saída não traz '$contem'"
  elif [[ -n "$nao_contem" && "$saida" == *"$nao_contem"* ]]; then
    motivo="a saída não deveria trazer '$nao_contem'"
  else
    for exigencia in "$@"; do
      comando="${exigencia:1}"
      if [[ "${exigencia:0:1}" == "+" ]] && ! grep -q "^$comando" "$d/chamadas.log"; then
        motivo="'$comando' deveria ter sido chamado"
      elif [[ "${exigencia:0:1}" == "-" ]] && grep -q "^$comando" "$d/chamadas.log"; then
        motivo="'$comando' não deveria ter sido chamado"
      fi
    done
    if [[ -z "$motivo" ]] && declare -F "verificar_$nome" > /dev/null; then
      motivo="$("verificar_$nome" "$d")"
    fi
  fi

  if [[ -n "$motivo" ]]; then
    printf '%-62s FALHOU: %s\n' "$nome" "$motivo"
    printf '%s\n' "$saida" | sed 's/^/    | /'
    falhas=$((falhas + 1))
  else
    printf '%-62s ok\n' "$nome"
  fi
}

# Confere o output `destruiu` gravado no GITHUB_OUTPUT: o passo da verificação de zerado só roda com `true`.
saiu_com() {
  if ! grep -qx "destruiu=$2" "$1/github_output"; then
    echo "o output 'destruiu' deveria ser $2: $(tr '\n' ' ' < "$1/github_output")"
  fi
}

# ---- TTL lido da tag do cluster no plano ----
preparar_plano_com_expires_at_futuro_nao_aplica() { plano_json "$1" "$FUTURO"; }
verificar_plano_com_expires_at_futuro_nao_aplica() { saiu_com "$1" false; }
caso plano_com_expires_at_futuro_nao_aplica 0 "prorrogado" "Destroy complete" "+terraform plan" "-terraform apply" "-gh run"

preparar_plano_vencido_aplica_o_arquivo_do_plano() { plano_json "$1" "$PASSADO"; }
verificar_plano_vencido_aplica_o_arquivo_do_plano() {
  local arquivo_do_plano arquivo_aplicado
  arquivo_do_plano="$(sed -n 's/^terraform plan .*-out=\([^ ]*\).*/\1/p' "$1/chamadas.log")"
  arquivo_aplicado="$(sed -n 's/^terraform apply .* \([^ ]*\)$/\1/p' "$1/chamadas.log")"
  if [[ -z "$arquivo_do_plano" || "$arquivo_do_plano" != "$arquivo_aplicado" ]]; then
    echo "o apply deveria usar o arquivo do plano ('$arquivo_do_plano'), usou '$arquivo_aplicado'"
    return
  fi
  saiu_com "$1" true
}
caso plano_vencido_aplica_o_arquivo_do_plano 0 "Destroy complete" "" "+terraform plan" "+terraform apply"

preparar_plano_sem_expires_at_conta_como_vencido() { plano_json "$1" "-"; }
caso plano_sem_expires_at_conta_como_vencido 0 "Destroy complete" "" "+terraform apply"

preparar_plano_com_expires_at_invalido_conta_como_vencido() { plano_json "$1" "isto-nao-e-data"; }
caso plano_com_expires_at_invalido_conta_como_vencido 0 "Destroy complete" "" "+terraform apply"

preparar_plano_com_a_marcacao_de_1970_conta_como_vencido() { plano_json "$1" "$MARCACAO"; }
caso plano_com_a_marcacao_de_1970_conta_como_vencido 0 "Destroy complete" "" "+terraform apply"

# O agendado leu vencido (EXPIRES_AT = -var, vencido) e um `subir` prorrogou antes do plano: o output do plano
# repete a -var, então só a tag do cluster mostra a prorrogação.
preparar_vale_a_tag_do_cluster_e_nao_o_output_do_plano() { plano_json "$1" "$FUTURO"; }
caso vale_a_tag_do_cluster_e_nao_o_output_do_plano 0 "prorrogado" "" "-terraform apply"

preparar_tag_vencida_vale_mesmo_com_output_do_plano_no_futuro() {
  plano_json "$1" "$PASSADO"
  EXPIRES_ATUAL="$FUTURO"
}
caso tag_vencida_vale_mesmo_com_output_do_plano_no_futuro 0 "Destroy complete" "" "+terraform apply"

preparar_cluster_sem_a_tag_conta_como_vencido() { plano_json "$1" "SEM-TAG"; }
caso cluster_sem_a_tag_conta_como_vencido 0 "Destroy complete" "" "+terraform apply"

# ---- Tag além do limite de 12 h (validation de variables.tf) conta como inválida, portanto vencida ----
# Folga do script: 10 min (relógio do runner e duração do ciclo). Uma tag acima de agora + 12 h + 10 min só pode
# ter sido gravada fora do Terraform, e uma tag assim nunca prorroga: senão, editar a tag a deixaria viva.
preparar_tag_em_2099_conta_como_vencida() { plano_json "$1" "2099-01-01T00:00:00Z"; }
verificar_tag_em_2099_conta_como_vencida() { saiu_com "$1" true; }
caso tag_em_2099_conta_como_vencida 0 "limite" "prorrogado" "+terraform apply"

preparar_tag_em_2099_avisa_que_pode_ter_sido_editada_fora_do_terraform() { plano_json "$1" "2099-01-01T00:00:00Z"; }
caso tag_em_2099_avisa_que_pode_ter_sido_editada_fora_do_terraform 0 "fora do Terraform" ""

preparar_tag_em_agora_mais_11h_nao_destroi() { plano_json "$1" "$(agora_mais "11 hours")"; }
caso tag_em_agora_mais_11h_nao_destroi 0 "prorrogado" "Destroy complete" "-terraform apply"

preparar_tag_em_agora_mais_12h_e_folga_menos_1min_nao_destroi() { plano_json "$1" "$(agora_mais "12 hours 9 minutes")"; }
caso tag_em_agora_mais_12h_e_folga_menos_1min_nao_destroi 0 "prorrogado" "Destroy complete" "-terraform apply"

preparar_tag_em_agora_mais_12h_e_folga_mais_1min_destroi() { plano_json "$1" "$(agora_mais "12 hours 11 minutes")"; }
caso tag_em_agora_mais_12h_e_folga_mais_1min_destroi 0 "Destroy complete" "prorrogado" "+terraform apply"

# ---- Plano obsoleto: um `subir` gravou o state depois do plano ----
preparar_plano_obsoleto_sai_diferente_de_0_sem_destruir() {
  plano_json "$1" "$PASSADO"
  : > "$1/apply.obsoleto"
}
verificar_plano_obsoleto_sai_diferente_de_0_sem_destruir() { saiu_com "$1" false; }
caso plano_obsoleto_sai_diferente_de_0_sem_destruir 1 "nada foi destruído" "Destroy complete" "+terraform apply"

preparar_plano_obsoleto_explica_a_causa_e_o_que_fazer() {
  plano_json "$1" "$PASSADO"
  : > "$1/apply.obsoleto"
}
caso plano_obsoleto_explica_a_causa_e_o_que_fazer 1 "foi alterado depois do plano" ""

preparar_plano_obsoleto_mantem_a_mensagem_do_terraform_no_log() {
  plano_json "$1" "$PASSADO"
  : > "$1/apply.obsoleto"
}
caso plano_obsoleto_mantem_a_mensagem_do_terraform_no_log 1 "Saved plan is stale" ""

preparar_falha_do_apply_por_outro_motivo_sai_1_sem_dizer_obsoleto() {
  plano_json "$1" "$PASSADO"
  : > "$1/apply.erro"
}
caso falha_do_apply_por_outro_motivo_sai_1_sem_dizer_obsoleto 1 "apply falhou" "alterado depois do plano"

# ---- Fila manual repetida antes do apply ----
preparar_fila_manual_na_fila_antes_do_apply_nao_aplica() {
  plano_json "$1" "$PASSADO"
  echo 1 > "$1/gh_queued"
}
verificar_fila_manual_na_fila_antes_do_apply_nao_aplica() { saiu_com "$1" false; }
caso fila_manual_na_fila_antes_do_apply_nao_aplica 0 "execução(ões) manual(is)" "Destroy complete" "+gh run" "-terraform apply"

preparar_fila_manual_em_andamento_antes_do_apply_nao_aplica() {
  plano_json "$1" "$PASSADO"
  echo 2 > "$1/gh_in_progress"
}
caso fila_manual_em_andamento_antes_do_apply_nao_aplica 0 "Há 2 execução" "Destroy complete" "-terraform apply"

preparar_fila_manual_vazia_aplica() { plano_json "$1" "$PASSADO"; }
caso fila_manual_vazia_aplica 0 "Destroy complete" "" "+gh run" "+terraform apply"

preparar_falha_do_gh_nao_aplica_e_sai_diferente_de_0() {
  plano_json "$1" "$PASSADO"
  : > "$1/gh.erro"
}
caso falha_do_gh_nao_aplica_e_sai_diferente_de_0 1 "" "Destroy complete" "-terraform apply"

preparar_guarda_da_fila_roda_depois_do_plano_e_antes_do_apply() { plano_json "$1" "$PASSADO"; }
verificar_guarda_da_fila_roda_depois_do_plano_e_antes_do_apply() {
  local n_plan n_gh n_apply
  n_plan="$(grep -n '^terraform plan' "$1/chamadas.log" | head -n 1 | cut -d: -f1)"
  n_gh="$(grep -n '^gh run' "$1/chamadas.log" | tail -n 1 | cut -d: -f1)"
  n_apply="$(grep -n '^terraform apply' "$1/chamadas.log" | head -n 1 | cut -d: -f1)"
  if [[ -z "$n_plan" || -z "$n_gh" || -z "$n_apply" ]] || ((n_plan > n_gh || n_gh > n_apply)); then
    echo "a ordem deveria ser plan ($n_plan), gh ($n_gh), apply ($n_apply)"
  fi
}
caso guarda_da_fila_roda_depois_do_plano_e_antes_do_apply 0 "" ""

# ---- Falhas que nunca podem virar destroy ----
preparar_falha_do_plan_nao_aplica_e_sai_diferente_de_0() {
  plano_json "$1" "$PASSADO"
  : > "$1/plan.erro"
}
caso falha_do_plan_nao_aplica_e_sai_diferente_de_0 1 "" "Destroy complete" "-terraform apply" "-gh run"

preparar_falha_ao_ler_o_plano_nao_aplica_e_sai_diferente_de_0() {
  plano_json "$1" "$PASSADO"
  : > "$1/show.erro"
}
caso falha_ao_ler_o_plano_nao_aplica_e_sai_diferente_de_0 1 "" "Destroy complete" "-terraform apply"

preparar_json_do_plano_ilegivel_nao_aplica_e_sai_diferente_de_0() {
  echo "isto não é json" > "$1/plano.cru"
}
caso json_do_plano_ilegivel_nao_aplica_e_sai_diferente_de_0 1 "" "Destroy complete" "-terraform apply"

# ---- Mesmos argumentos de hoje ----
preparar_usa_as_mesmas_variaveis_e_o_lock_timeout_de_hoje() {
  plano_json "$1" "$PASSADO"
  EXPIRES_ATUAL="2020-06-01T00:00:00Z"
  SHA_ATUAL="1111111111111111111111111111111111111111"
}
verificar_usa_as_mesmas_variaveis_e_o_lock_timeout_de_hoje() {
  local linha_plan linha_apply esperado
  linha_plan="$(grep '^terraform plan' "$1/chamadas.log")"
  linha_apply="$(grep '^terraform apply' "$1/chamadas.log")"
  for esperado in " -destroy" " -input=false" " -lock-timeout=5m" " -var-file=environments/aws.tfvars" \
    " -var region=us-east-1" " -var expires_at=2020-06-01T00:00:00Z" \
    " -var image_sha=1111111111111111111111111111111111111111" " -var service_desired_count=0" " -out="; do
    if [[ "$linha_plan" != *"$esperado"* ]]; then
      echo "o plan não traz '$esperado': $linha_plan"
      return
    fi
  done
  for esperado in " -input=false" " -lock-timeout=5m"; do
    if [[ "$linha_apply" != *"$esperado"* ]]; then
      echo "o apply não traz '$esperado': $linha_apply"
      return
    fi
  done
  if [[ "$linha_apply" == *"-auto-approve"* || "$linha_apply" == *"-destroy"* ]]; then
    echo "o apply de um plano salvo não leva -auto-approve nem -destroy: $linha_apply"
  fi
}
caso usa_as_mesmas_variaveis_e_o_lock_timeout_de_hoje 0 "Destroy complete" ""

# ---- O endereço padrão do script existe na configuração real ----
# O script lê o prazo de module.app.aws_ecs_cluster.this. Aqui o endereço é lido do próprio script (TTL_ENDERECO) e
# procurado na configuração: o módulo da raiz (main.tf) e o recurso dentro dele. Que o cluster leve
# tags["expires_at"] = var.expires_at vista da raiz é provado no terraform test (tests/raiz-aplicacao.tftest.hcl).
config="$dir/../.."

# Imprime o motivo se o endereço padrão do script não existir na configuração; nada se existir.
motivo_do_endereco() { # script
  local endereco modulo tipo nome origem
  # shellcheck disable=SC2016 # o ${...} é texto do script lido, não uma expansão desta linha
  endereco="$(sed -n 's/^ttl_endereco="${TTL_ENDERECO:-\([^}]*\)}"$/\1/p' "$1")"
  if [[ ! "$endereco" =~ ^module\.([a-z_]+)\.([a-z_]+)\.([a-z_]+)$ ]]; then
    echo "o endereço padrão do script não está no formato module.<modulo>.<tipo>.<nome>: [$endereco]"
    return
  fi
  modulo="${BASH_REMATCH[1]}"
  tipo="${BASH_REMATCH[2]}"
  nome="${BASH_REMATCH[3]}"
  # source = "./modules/x" do bloco `module "<modulo>"` em main.tf
  origem="$(sed -n "/^module \"$modulo\" {/,/^}/p" "$config/main.tf" | sed -n 's/^[[:space:]]*source[[:space:]]*=[[:space:]]*"\(.*\)"/\1/p' | head -n 1)"
  if [[ -z "$origem" || ! -f "$config/$origem/main.tf" ]]; then
    echo "o módulo \"$modulo\" não existe em main.tf (ou o source [$origem] não tem main.tf)"
    return
  fi
  if ! grep -q "^resource \"$tipo\" \"$nome\"" "$config/$origem/main.tf"; then
    echo "o recurso $tipo.$nome não existe em $origem/main.tf: o agendado leria um endereço inexistente"
  fi
}

# caso_endereco NOME SCRIPT sim|nao  (sim = o endereço deve existir; nao = deve ser reprovado)
caso_endereco() {
  local nome="$1" motivo
  if [[ -n "${FILTRO:-}" && ! "$nome" =~ $FILTRO ]]; then return 0; fi
  total=$((total + 1))
  motivo="$(motivo_do_endereco "$2")"
  if [[ "$3" == "sim" && -n "$motivo" ]]; then
    printf '%-62s FALHOU: %s\n' "$nome" "$motivo"
    falhas=$((falhas + 1))
  elif [[ "$3" == "nao" && -z "$motivo" ]]; then
    printf '%-62s FALHOU: %s\n' "$nome" "deveria reprovar um endereço inexistente"
    falhas=$((falhas + 1))
  else
    printf '%-62s ok\n' "$nome"
  fi
}

caso_endereco endereco_padrao_do_script_existe_na_configuracao_real "$script" sim
sed 's/aws_ecs_cluster\.this/aws_ecs_cluster.outro/' "$script" > "$raiz/script-recurso-errado.sh"
caso_endereco endereco_com_recurso_inexistente_reprova "$raiz/script-recurso-errado.sh" nao
sed 's/module\.app\.aws_ecs_cluster/module.nada.aws_ecs_cluster/' "$script" > "$raiz/script-modulo-errado.sh"
caso_endereco endereco_com_modulo_inexistente_reprova "$raiz/script-modulo-errado.sh" nao

# ---- Sem terraform no PATH: pula com aviso, ou falha com CI=true ----
# O `rodar.sh` roda de novo, aninhado, só com a seção do Terraform real (FILTRO) e com um PATH sem terraform.
rodar_sem_terraform() { # valor_de_CI (vazio = CI não definido)
  local bin="$raiz/bin-sem-tf" ferramenta
  if [[ ! -d "$bin" ]]; then
    mkdir -p "$bin"
    for ferramenta in bash cat chmod cp cut date dirname grep head jq mkdir mktemp rm sed sort tail tee tr wc; do
      ln -s "$(command -v "$ferramenta")" "$bin/$ferramenta"
    done
  fi
  if [[ -n "$1" ]]; then
    PATH="$bin" CI="$1" FILTRO=terraform_real "$BASH" "${BASH_SOURCE[0]}" 2>&1
  else
    (unset CI; PATH="$bin" FILTRO=terraform_real "$BASH" "${BASH_SOURCE[0]}" 2>&1) # o CI do job não pode vazar
  fi
}

verifica_aninhado() { # nome codigo_esperado trecho nao_trecho valor_de_CI
  local nome="$1" saida codigo motivo=""
  if [[ -n "${FILTRO:-}" && ! "$nome" =~ $FILTRO ]]; then return 0; fi
  total=$((total + 1))
  saida="$(rodar_sem_terraform "$5")"
  codigo=$?
  if [[ $codigo -ne $2 ]]; then
    motivo="esperava saída $2, saiu $codigo"
  elif [[ -n "$3" && "$saida" != *"$3"* ]]; then
    motivo="a saída não traz '$3'"
  elif [[ -n "$4" && "$saida" == *"$4"* ]]; then
    motivo="a saída não deveria trazer '$4'"
  fi
  if [[ -n "$motivo" ]]; then
    printf '%-62s FALHOU: %s\n' "$nome" "$motivo"
    printf '%s\n' "$saida" | sed 's/^/    | /'
    falhas=$((falhas + 1))
  else
    printf '%-62s ok\n' "$nome"
  fi
}

verifica_aninhado sem_terraform_sem_ci_pula_com_aviso 0 "PULADOS" "FALHOU" ""
verifica_aninhado sem_terraform_com_ci_true_falha 1 "CI=true" "PULADOS" "true"

# ---- Terraform real: o filtro do TTL sobre um plano de verdade ----
# Num `plan -destroy`, o Terraform recalcula os outputs da raiz com as -var da chamada: o output expires_at do
# prior_state repete a -var e nunca reflete o state. O prazo real está na tag do cluster. A config mínima abaixo
# reproduz isso com um terraform_data dentro de um módulo (o recurso real, module.app.aws_ecs_cluster.this, exige a
# AWS); o filtro jq e o endereço/atributo são parametrizados, e o script recebe o mesmo endereço por TTL_ENDERECO e
# TTL_CAMINHO. Sem terraform no PATH os casos são PULADOS com aviso, e com CI=true FALHAM (o job `validar` o instala).
filtro="$dir/../../scripts/expires-at-do-cluster.jq"
terraform_real="$(command -v terraform || true)"

preparar_config_real() { # pasta tag_inicial: aplica a config com a tag dada
  local p="$1"
  mkdir -p "$p/modules/app" "$p/environments"
  cat > "$p/modules/app/main.tf" << 'TF'
variable "expires_at" { type = string }
resource "terraform_data" "cluster" { input = { tags = { expires_at = var.expires_at } } }
TF
  cat > "$p/main.tf" << 'TF'
variable "expires_at" { type = string }
variable "region" { type = string }
variable "image_sha" { type = string }
variable "service_desired_count" { type = number }
module "app" {
  source     = "./modules/app"
  expires_at = var.expires_at
}
output "expires_at" { value = var.expires_at }
TF
  : > "$p/environments/aws.tfvars"
  (cd "$p" && terraform init -input=false > /dev/null \
    && terraform apply -auto-approve -input=false -var "expires_at=$2" -var region=x -var image_sha=y -var service_desired_count=0 > /dev/null)
}

resultado_real() { # nome motivo
  if [[ -n "$2" ]]; then
    printf '%-62s FALHOU: %s\n' "$1" "$2"
    falhas=$((falhas + 1))
  else
    printf '%-62s ok\n' "$1"
  fi
}

if [[ -n "${FILTRO:-}" && ! "terraform_real" =~ $FILTRO ]]; then
  :
elif [[ -z "$terraform_real" ]]; then
  # No CI (o `validar` instala o Terraform) pular em silêncio esconderia a falta da instalação: falha.
  if [[ "${CI:-}" == "true" ]]; then
    for nome in terraform_real_filtro_le_a_tag_do_state_e_nao_o_output terraform_real_tag_futura_com_var_vencida_nao_destroi \
      terraform_real_tag_vencida_destroi_o_state; do
      total=$((total + 1))
      resultado_real "$nome" "terraform fora do PATH com CI=true: o job validar deveria tê-lo instalado (setup-terraform)"
    done
  else
    echo "AVISO: terraform fora do PATH: os casos com Terraform real (terraform_real_*) foram PULADOS."
  fi
else
  total=$((total + 3))
  mkdir -p "$raiz/bin-gh" && cp "$raiz/bin/gh" "$raiz/bin-gh/gh"

  # O output do prior_state repete a -var; o filtro lê a tag do recurso.
  p="$raiz/real-filtro"
  preparar_config_real "$p" "$FUTURO"
  (cd "$p" && terraform plan -destroy -input=false -var "expires_at=$PASSADO" -var region=x -var image_sha=y \
    -var service_desired_count=0 -out=p.tfplan > /dev/null)
  json="$(cd "$p" && terraform show -json p.tfplan)"
  saida_output="$(jq -r '.prior_state.values.outputs.expires_at.value' <<< "$json")"
  saida_tag="$(jq -r -f "$filtro" --arg endereco module.app.terraform_data.cluster \
    --argjson caminho '["input","tags","expires_at"]' <<< "$json")"
  motivo=""
  [[ "$saida_output" == "$PASSADO" ]] || motivo="o output do prior_state deveria repetir a -var ($PASSADO), deu '$saida_output'"
  if [[ -z "$motivo" && "$saida_tag" != "$FUTURO" ]]; then
    motivo="o filtro deveria devolver a tag do state ($FUTURO), devolveu [$saida_tag]"
  fi
  resultado_real terraform_real_filtro_le_a_tag_do_state_e_nao_o_output "$motivo"

  # O script inteiro, com o Terraform real: tag futura e -var vencida => nada é destruído.
  p="$raiz/real-prorrogado"
  preparar_config_real "$p" "$FUTURO"
  : > "$p/github_output"
  saida="$(cd "$p" && PATH="$raiz/bin-gh:$PATH" GITHUB_OUTPUT="$p/github_output" AWS_REGION=x EXPIRES_AT="$PASSADO" SHA=y \
    REPOSITORIO=dono/repo GH_TOKEN=token-falso TTL_ENDERECO=module.app.terraform_data.cluster \
    TTL_CAMINHO='["input","tags","expires_at"]' bash "$script" 2>&1)"
  codigo=$?
  recursos="$(cd "$p" && terraform state list | wc -l)"
  motivo=""
  if [[ $codigo -ne 0 || "$saida" != *"prorrogado"* || $recursos -ne 1 ]] || ! grep -qx "destruiu=false" "$p/github_output"; then
    motivo="esperava saída 0, 'prorrogado', 1 recurso no state e destruiu=false; saiu $codigo com $recursos recurso(s)"
    printf '%s\n' "$saida" | sed 's/^/    | /'
  fi
  resultado_real terraform_real_tag_futura_com_var_vencida_nao_destroi "$motivo"

  # O script inteiro: tag vencida => destrói e o state fica vazio.
  p="$raiz/real-vencido"
  preparar_config_real "$p" "$PASSADO"
  : > "$p/github_output"
  saida="$(cd "$p" && PATH="$raiz/bin-gh:$PATH" GITHUB_OUTPUT="$p/github_output" AWS_REGION=x EXPIRES_AT="$PASSADO" SHA=y \
    REPOSITORIO=dono/repo GH_TOKEN=token-falso TTL_ENDERECO=module.app.terraform_data.cluster \
    TTL_CAMINHO='["input","tags","expires_at"]' bash "$script" 2>&1)"
  codigo=$?
  recursos="$(cd "$p" && terraform state list | wc -l)"
  motivo=""
  if [[ $codigo -ne 0 || $recursos -ne 0 ]] || ! grep -qx "destruiu=true" "$p/github_output"; then
    motivo="esperava saída 0, state vazio e destruiu=true; saiu $codigo com $recursos recurso(s)"
    printf '%s\n' "$saida" | sed 's/^/    | /'
  fi
  resultado_real terraform_real_tag_vencida_destroi_o_state "$motivo"
fi

echo
if [[ $falhas -gt 0 ]]; then
  echo "$falhas de $total casos falharam."
  exit 1
fi
echo "$total casos ok."
