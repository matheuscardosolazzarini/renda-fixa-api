#!/usr/bin/env bash
# Testa scripts/bloquear-recriacao.sh (S5): o `subir` não pode aplicar um plano que recria o RDS ou a versão do
# secret, porque a senha efêmera e a connection string de escrita única ficariam dessincronizadas.
# Os planos são JSON mínimos, escritos aqui. Precisa de bash e jq. Uso: tests/bloquear-recriacao/rodar.sh
#
# aprova: saída 0. reprova: saída exatamente 1 e a mensagem traz o endereço e o comando de recuperação.
# Em todos os casos o valor sentinela SEGREDO-CANARIO-123 não pode aparecer na saída.
set -uo pipefail

dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
script="${BLOQUEAR_RECRIACAO:-$dir/../../scripts/bloquear-recriacao.sh}"

falhas=0
total=0

rc() { # endereco tipo acoes-json
  printf '{"address":"%s","mode":"managed","type":"%s","name":"x","change":{"actions":%s,"after":{"password":"SEGREDO-CANARIO-123"}}}' "$1" "$2" "$3"
}
plano() { printf '{"format_version":"1.2","resource_changes":[%s]}' "$1"; }

DB="module.database.aws_db_instance.this"
SV="module.app.aws_secretsmanager_secret_version.connection_string"

# nome | esperado (aprova/reprova) | trecho exigido na saída (só reprova) | entrada
caso() {
  local nome="$1" esperado="$2" trecho="$3" entrada="$4" saida codigo motivo=""
  total=$((total + 1))
  saida="$(printf '%s' "$entrada" | bash "$script" 2>&1)"
  codigo=$?
  if [[ "$esperado" == "aprova" && $codigo -ne 0 ]]; then
    motivo="deveria aprovar (saiu com $codigo)"
  elif [[ "$esperado" == "reprova" && $codigo -ne 1 ]]; then
    motivo="deveria sair com 1 (saiu com $codigo)"
  elif [[ "$esperado" == "reprova" && "$saida" != *"$trecho"* ]]; then
    motivo="a mensagem não traz '$trecho'"
  elif [[ "$trecho" == module.* && "$saida" != *destruir* ]]; then
    motivo="a mensagem não manda usar o destruir"
  elif [[ "$saida" == *SEGREDO-CANARIO-123* ]]; then
    motivo="vazou o valor sentinela na saída"
  fi
  if [[ -n "$motivo" ]]; then
    printf '%-55s FALHOU: %s\n' "$nome" "$motivo"
    printf '%s\n' "$saida" | sed 's/^/    | /'
    falhas=$((falhas + 1))
  else
    printf '%-55s ok\n' "$nome"
  fi
}

caso "aprova-rds-criado-no-primeiro-apply" aprova "" "$(plano "$(rc "$DB" aws_db_instance '["create"]')")"
caso "aprova-rds-atualizado-no-lugar" aprova "" "$(plano "$(rc "$DB" aws_db_instance '["update"]')")"
caso "aprova-rds-sem-mudanca" aprova "" "$(plano "$(rc "$DB" aws_db_instance '["no-op"]')")"
caso "aprova-secret-version-sem-mudanca" aprova "" "$(plano "$(rc "$SV" aws_secretsmanager_secret_version '["no-op"]')")"
caso "aprova-outro-recurso-substituido" aprova "" "$(plano "$(rc module.app.aws_ecs_task_definition.api aws_ecs_task_definition '["delete","create"]')")"
caso "aprova-rds-so-apagado-no-destroy" aprova "" "$(plano "$(rc "$DB" aws_db_instance '["delete"]')")"
caso "reprova-rds-delete-e-create" reprova "$DB" "$(plano "$(rc "$DB" aws_db_instance '["delete","create"]')")"
caso "reprova-rds-create-antes-de-delete" reprova "$DB" "$(plano "$(rc "$DB" aws_db_instance '["create","delete"]')")"
caso "reprova-secret-version-substituida" reprova "$SV" "$(plano "$(rc "$SV" aws_secretsmanager_secret_version '["delete","create"]')")"
caso "reprova-os-dois-listados" reprova "$SV" "$(plano "$(rc "$DB" aws_db_instance '["delete","create"]'),$(rc "$SV" aws_secretsmanager_secret_version '["delete","create"]')")"
caso "reprova-entrada-vazia" reprova "inválid" ""
caso "reprova-nao-e-json" reprova "inválid" "isto nao e json"
caso "reprova-sem-resource-changes" reprova "resource_changes" '{"format_version":"1.2"}'
caso "reprova-resource-changes-nulo" reprova "resource_changes" '{"resource_changes":null}'
caso "reprova-actions-malformado" reprova "malformado" "$(plano '{"address":"a","mode":"managed","type":"aws_db_instance","change":{"actions":"replace"}}')"

echo
if [[ $falhas -gt 0 ]]; then
  echo "$falhas de $total casos falharam."
  exit 1
fi
echo "$total casos ok."
