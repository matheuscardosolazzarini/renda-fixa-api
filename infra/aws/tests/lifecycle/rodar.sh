#!/usr/bin/env bash
# Testa scripts/verificar-sem-prevent-destroy.sh: `lifecycle { prevent_destroy = true }` faz o `terraform destroy`
# abortar sem destruir nada (o ambiente fica ligado e o agendado, vermelho), e nem o `terraform test` nem a trava
# de custo enxergam esse atributo. O script reprova qualquer prevent_destroy em .tf sob infra/aws.
# Uso: tests/lifecycle/rodar.sh
set -uo pipefail

dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
script="${VERIFICAR_PREVENT_DESTROY:-$dir/../../scripts/verificar-sem-prevent-destroy.sh}"
arvore="$dir/../.."
raiz="$(mktemp -d)"
trap 'rm -rf "$raiz"' EXIT

falhas=0
total=0

# caso NOME SAIDA_ESPERADA DEVE_CONTER NAO_DEVE_CONTER PASTA
# A pasta do caso é montada por preparar_<nome> em "$raiz/<nome>"; PASTA vazia usa a pasta do caso.
caso() {
  local nome="$1" saida_esperada="$2" contem="$3" nao_contem="$4" pasta="${5:-}" saida codigo motivo=""
  if [[ -n "${FILTRO:-}" && ! "$nome" =~ $FILTRO ]]; then
    return 0
  fi
  total=$((total + 1))
  mkdir -p "$raiz/$nome"
  "preparar_$nome" "$raiz/$nome"
  saida="$(bash "$script" "${pasta:-$raiz/$nome}" 2>&1)"
  codigo=$?
  if [[ $codigo -ne $saida_esperada ]]; then
    motivo="esperava saída $saida_esperada, saiu $codigo"
  elif [[ -n "$contem" && "$saida" != *"$contem"* ]]; then
    motivo="a saída não traz '$contem'"
  elif [[ -n "$nao_contem" && "$saida" == *"$nao_contem"* ]]; then
    motivo="a saída não deveria trazer '$nao_contem'"
  fi
  if [[ -n "$motivo" ]]; then
    printf '%-58s FALHOU: %s\n' "$nome" "$motivo"
    printf '%s\n' "$saida" | sed 's/^/    | /'
    falhas=$((falhas + 1))
  else
    printf '%-58s ok\n' "$nome"
  fi
}

recurso_protegido() { # arquivo valor
  cat > "$1" << TF
resource "aws_db_instance" "this" {
  identifier = "renda-fixa-api"

  lifecycle {
    prevent_destroy = $2
  }
}
TF
}

preparar_prevent_destroy_true_reprova() { recurso_protegido "$1/main.tf" true; }
caso prevent_destroy_true_reprova 1 "main.tf" ""

preparar_prevent_destroy_false_tambem_reprova() { recurso_protegido "$1/main.tf" false; }
caso prevent_destroy_false_tambem_reprova 1 "prevent_destroy" ""

preparar_prevent_destroy_em_modulo_aninhado_reprova() {
  mkdir -p "$1/modules/database"
  echo 'resource "terraform_data" "ok" {}' > "$1/main.tf"
  recurso_protegido "$1/modules/database/main.tf" true
}
caso prevent_destroy_em_modulo_aninhado_reprova 1 "modules/database/main.tf" ""

preparar_prevent_destroy_sem_espacos_reprova() {
  printf 'resource "terraform_data" "x" {\n  lifecycle { prevent_destroy=true }\n}\n' > "$1/main.tf"
}
caso prevent_destroy_sem_espacos_reprova 1 "prevent_destroy" ""

preparar_reprova_e_mostra_a_linha() { recurso_protegido "$1/main.tf" true; }
caso reprova_e_mostra_a_linha 1 "main.tf:5:" ""

preparar_tf_json_com_prevent_destroy_reprova() {
  echo '{"resource":{"terraform_data":{"x":{"lifecycle":{"prevent_destroy":true}}}}}' > "$1/main.tf.json"
}
caso tf_json_com_prevent_destroy_reprova 1 "main.tf.json" ""

# `removed { lifecycle { destroy = false } }` tira o recurso do state sem destruí-lo: o recurso fica ligado.
preparar_removed_com_destroy_false_reprova() {
  cat > "$1/main.tf" << 'TF'
removed {
  from = aws_db_instance.this

  lifecycle {
    destroy = false
  }
}
TF
}
caso removed_com_destroy_false_reprova 1 "main.tf:5:" ""

preparar_destroy_false_sem_espacos_reprova() {
  printf 'removed {\n  from = aws_db_instance.this\n  lifecycle { destroy=false }\n}\n' > "$1/main.tf"
}
caso destroy_false_sem_espacos_reprova 1 "destroy" ""

preparar_destroy_false_em_json_reprova() {
  echo '{"removed":[{"from":"aws_db_instance.this","lifecycle":{"destroy":false}}]}' > "$1/main.tf.json"
}
caso destroy_false_em_json_reprova 1 "main.tf.json" ""

preparar_removed_com_destroy_true_aprova() {
  printf 'removed {\n  from = aws_db_instance.this\n  lifecycle {\n    destroy = true\n  }\n}\n' > "$1/main.tf"
}
caso removed_com_destroy_true_aprova 0 "APROVADO" "REPROVADO"

preparar_outro_atributo_terminado_em_destroy_aprova() {
  printf 'resource "aws_volume_attachment" "x" {\n  skip_destroy = false\n}\n' > "$1/main.tf"
}
caso outro_atributo_terminado_em_destroy_aprova 0 "APROVADO" "REPROVADO"

preparar_ignora_a_pasta_dot_terraform() {
  mkdir -p "$1/.terraform/modules/x"
  recurso_protegido "$1/.terraform/modules/x/main.tf" true
  echo 'resource "terraform_data" "ok" {}' > "$1/main.tf"
}
caso ignora_a_pasta_dot_terraform 0 "APROVADO" "REPROVADO"

# O floci_override.tf é gerado só na raiz da pasta (scripts/gerar-floci-override.sh); um com esse nome em
# subpasta é código comum e não escapa da verificação.
preparar_floci_override_em_subpasta_com_prevent_destroy_reprova() {
  mkdir -p "$1/modules/database"
  echo 'resource "terraform_data" "ok" {}' > "$1/main.tf"
  recurso_protegido "$1/modules/database/floci_override.tf" true
}
caso floci_override_em_subpasta_com_prevent_destroy_reprova 1 "modules/database/floci_override.tf" ""

preparar_ignora_o_floci_override_gerado() {
  recurso_protegido "$1/floci_override.tf" true
  echo 'resource "terraform_data" "ok" {}' > "$1/main.tf"
}
caso ignora_o_floci_override_gerado 0 "APROVADO" "REPROVADO"

preparar_ignora_comentario_que_cita_o_atributo() {
  cat > "$1/main.tf" << 'TF'
# Não usar prevent_destroy: o destroy do agendado abortaria.
// prevent_destroy também fica proibido aqui
resource "terraform_data" "ok" {}
TF
}
caso ignora_comentario_que_cita_o_atributo 0 "APROVADO" "REPROVADO"

preparar_pasta_sem_prevent_destroy_aprova() {
  echo 'resource "terraform_data" "ok" { lifecycle { ignore_changes = [input] } }' > "$1/main.tf"
}
caso pasta_sem_prevent_destroy_aprova 0 "APROVADO" ""

preparar_pasta_inexistente_sai_2() { :; }
caso pasta_inexistente_sai_2 2 "não existe" "APROVADO" "$raiz/nao-existe"

preparar_pasta_sem_arquivo_tf_sai_2() { :; }
caso pasta_sem_arquivo_tf_sai_2 2 "nenhum arquivo .tf" "APROVADO"

preparar_arvore_atual_aprova() { :; }
caso arvore_atual_aprova 0 "APROVADO" "REPROVADO" "$arvore"

echo
if [[ $falhas -gt 0 ]]; then
  echo "$falhas de $total casos falharam."
  exit 1
fi
echo "$total casos ok."
