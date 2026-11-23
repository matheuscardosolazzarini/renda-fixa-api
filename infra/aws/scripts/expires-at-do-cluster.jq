# Prazo (expires_at) gravado no state, lido do `terraform show -json` de um plano.
# Vem da tag do cluster (D4) no prior_state, NÃO do output `expires_at`: num `plan -destroy` o Terraform recalcula
# os outputs da raiz com as -var da chamada, então o output sempre devolve o valor passado (o já lido, velho),
# enquanto os atributos dos recursos vêm do state atualizado (refresh).
#
# Argumentos (jq --arg/--argjson):
#   $endereco  endereço do recurso, por exemplo "module.app.aws_ecs_cluster.this"
#   $caminho   caminho do atributo dentro de `values`, por exemplo ["tags","expires_at"]
# Saída: o valor, ou nada se o recurso ou o atributo não existirem (quem chama trata como vencido).
[.prior_state.values.root_module | .. | objects | select(.address? == $endereco) | .values | getpath($caminho)]
| first // empty
