#!/usr/bin/env bash
# Reprova `lifecycle { prevent_destroy = ... }` e `removed { lifecycle { destroy = false } }` em qualquer .tf
# (ou .tf.json) sob a pasta indicada.
# Motivo: com o atributo, o `terraform destroy` aborta ("Instance cannot be destroyed") sem destruir nada, o
# ambiente fica ligado e o agendado fica vermelho a cada ciclo. O plano em JSON não traz o atributo, então nem o
# `terraform test` nem a trava de custo o enxergam: só a leitura do código o barra.
# O `destroy = false` do bloco `removed` deixa o recurso vivo e fora do state, onde o destroy do agendado não o enxerga.
#
# Ignora .terraform/ (módulos baixados), o ./floci_override.tf da raiz da pasta (gerado, só no ciclo do Floci; um
# com esse nome em subpasta é verificado) e linhas de
# comentário (# ou //). Comentário de bloco /* */ não é tratado: o formato não é usado neste repositório.
#
# Uso: scripts/verificar-sem-prevent-destroy.sh [PASTA]   (padrão: infra/aws, relativo a este script)
# Saída: 0 = nenhum prevent_destroy; 1 = achou; 2 = pasta inexistente ou sem arquivo .tf.
set -euo pipefail

pasta="${1:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
if [[ ! -d "$pasta" ]]; then
  echo "ERRO: a pasta $pasta não existe." >&2
  exit 2
fi
cd "$pasta"

arquivos="$(find . -name .terraform -prune -o -type f \( -name '*.tf' -o -name '*.tf.json' \) ! -path ./floci_override.tf -print | sort)"
if [[ -z "$arquivos" ]]; then
  echo "ERRO: nenhum arquivo .tf em $pasta: nada foi verificado." >&2
  exit 2
fi

achados=0
while IFS= read -r arquivo; do
  # grep sai 1 sem ocorrência; o `|| true` evita derrubar o script por isso.
  while IFS= read -r linha; do
    conteudo="${linha#*:}"
    if [[ "$conteudo" =~ ^[[:space:]]*(#|//) ]]; then
      continue
    fi
    echo "REPROVADO proteção contra destroy: ${arquivo#./}:$linha"
    achados=$((achados + 1))
  done < <(grep -nE 'prevent_destroy|(^|[^A-Za-z0-9_])"?destroy"?[[:space:]]*[=:][[:space:]]*false' "$arquivo" || true)
done <<< "$arquivos"

if [[ $achados -gt 0 ]]; then
  echo 'Não são permitidos prevent_destroy (o destroy do ambiente efêmero abortaria sem destruir nada) nem destroy = false (bloco removed, que tira o recurso do state sem destruí-lo).' >&2
  exit 1
fi
echo "APROVADO: nenhum prevent_destroy nem destroy = false em $(grep -c . <<< "$arquivos") arquivo(s) .tf."
