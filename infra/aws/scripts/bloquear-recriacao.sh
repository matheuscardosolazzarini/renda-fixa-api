#!/usr/bin/env bash
# Bloqueia, antes do apply, um plano que recria o RDS ou a versão do secret da connection string (S5).
#
# Por quê: a senha do banco nasce num recurso efêmero a cada execução e entra no RDS (password_wo) e no secret
# (secret_string_wo) uma única vez, com a versão de escrita fixa em 1. Se um `apply` recriar o RDS, a senha nova
# vai para o banco novo, mas o secret não é reescrito (a versão não mudou): a API subiria com a senha antiga.
# Se recriar só a versão do secret, o valor escrito vem de uma senha que o RDS não tem. Nos dois casos o único
# caminho seguro é destruir o ambiente e subir de novo.
#
# Lê pela entrada padrão o JSON do plano (terraform show -json <plano>), como o trava-custo.sh, e sai com 1 se
# algum item aws_db_instance ou aws_secretsmanager_secret_version tiver delete e create na mesma mudança
# (substituição, em qualquer ordem). Fail closed: entrada que não é JSON, sem resource_changes ou com item
# malformado também reprova. O JSON do plano tem variáveis sensíveis: o script nunca o imprime; as mensagens
# trazem só endereço e tipo. Os erros do jq vão para /dev/null porque podem citar um trecho do dado.
#
# Uso: terraform show -json plano.tfplan | scripts/bloquear-recriacao.sh
set -euo pipefail

if ! command -v jq > /dev/null 2>&1; then
  echo "REPROVADO sem jq: não há como verificar o plano" >&2
  exit 1
fi

# shellcheck disable=SC2016
programa='
def sensivel: ["aws_db_instance", "aws_secretsmanager_secret_version"];

def bem_formado:
  type == "object"
  and ((.address | type) == "string")
  and ((.type | type) == "string")
  and ((.mode | type) == "string")
  and ((.change | type) == "object")
  and ((.change.actions | type) == "array")
  and all(.change.actions[]; type == "string");

if length != 1 then
  "REPROVADO entrada inválida: esperado exatamente um documento JSON"
else
  .[0] as $p
  | if ($p | type) != "object" then
      "REPROVADO entrada inválida: o plano deve ser um objeto JSON"
    elif ($p.resource_changes | type) != "array" then
      "REPROVADO resource_changes ausente ou inválido: sem ele o plano não pode ser verificado"
    elif ([$p.resource_changes[] | bem_formado] | all | not) then
      "REPROVADO resource_changes com item malformado"
    else
      [ $p.resource_changes[]
        | select(.mode == "managed" and (.type | IN(sensivel[])))
        | select((.change.actions | index("delete")) != null and (.change.actions | index("create")) != null)
        | "REPROVADO \(.type) \(.address): o plano recria o recurso"
      ] as $r
      | if ($r | length) > 0 then $r[] else "APROVADO" end
    end
end
'

if ! saida="$(jq -rs "$programa" 2> /dev/null)"; then
  echo "REPROVADO entrada inválida: o plano não é um JSON legível" >&2
  exit 1
fi

if [[ "$saida" == APROVADO ]]; then
  echo "Recriação: nenhum RDS nem versão de secret será recriado."
  exit 0
fi

if [[ "$saida" == REPROVADO* ]]; then
  echo "$saida" >&2
  echo "O ambiente não pode ser atualizado com recriação do banco ou do secret: a senha e a connection string ficariam dessincronizadas." >&2
  echo "Rode o workflow com acao = destruir (o job limpar já destrói o ambiente depois desta falha: espere ele terminar) e depois rode o subir de novo." >&2
  exit 1
fi

# Qualquer outra saída (inclusive vazia) é inesperada: fail closed.
echo "REPROVADO resultado inesperado da análise do plano" >&2
echo "Rode o workflow com acao = destruir e depois rode o subir de novo." >&2
exit 1
