#!/usr/bin/env bash
# Verificação de zerado logo depois de um destroy na AWS real (camada 4 da F11, docs/SPEC-F11-EXECUCAO.md).
#
# Chama verificar-zerado.sh e, SÓ quando ele sai com 1 (achou resíduo), espera um pouco e repete UMA vez:
# a Resource Groups Tagging API (e outras listagens) pode demorar a refletir uma exclusão recém-feita
# (atraso de consistência, a confirmar no P8). Fora essa repetição, o resultado é o do script original:
#   0 = conta zerada; 1 = resíduo (também depois da repetição); 2 = não conseguiu verificar.
# A saída 2 NUNCA é repetida: erro de chamada, credencial ou permissão não some esperando, e repetir em
# silêncio esconderia o problema.
#
# Só os jobs da AWS real usam este wrapper, e só depois de um destroy. O ciclo do Floci chama
# verificar-zerado.sh direto, e o agendado sem ambiente ativo também (resíduo ali não é atraso).
#
# Uso: scripts/verificar-zerado-apos-destroy.sh [REGIAO]
# Variável opcional: VERIFICAR_ESPERA_SEGUNDOS (padrão 90) para o intervalo antes da repetição.
set -uo pipefail

dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
espera="${VERIFICAR_ESPERA_SEGUNDOS:-90}"

if [[ ! "$espera" =~ ^[0-9]{1,4}$ ]]; then
  echo "ERRO VERIFICAR_ESPERA_SEGUNDOS inválida: $espera" >&2
  exit 2
fi

bash "$dir/verificar-zerado.sh" "$@"
codigo=$?

if [[ $codigo -ne 1 ]]; then
  exit "$codigo"
fi

echo "AVISO resíduo logo depois do destroy: repetindo a verificação uma vez, em ${espera}s (atraso de consistência)"
sleep "$espera"
bash "$dir/verificar-zerado.sh" "$@"
exit $?
