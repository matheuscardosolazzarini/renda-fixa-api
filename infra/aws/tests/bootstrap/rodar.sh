#!/usr/bin/env bash
# Confere a policy da role do CI (bootstrap.sh --imprimir-policy-ci, que não chama a AWS) com jq:
# JSON válido, tamanho dentro do limite das policies inline e ecs:RunTask e ecs:StopTask restritos
# ao que o ambiente usa (S2). Precisa de bash e jq. Uso: tests/bootstrap/rodar.sh
set -uo pipefail

dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
bootstrap="${BOOTSTRAP:-$dir/../../bootstrap/bootstrap.sh}"
conta="123456789012" # a conta fictícia que o próprio --imprimir-policy-ci usa
limite=10240

if ! policy="$(bash "$bootstrap" --imprimir-policy-ci)"; then
  echo "FALHOU: --imprimir-policy-ci não executou"
  exit 1
fi

falhas=0
total=0

confere() { # descricao filtro-jq (precisa devolver true)
  local descricao="$1" filtro="$2" resultado
  total=$((total + 1))
  resultado="$(jq -r "$filtro" <<< "$policy" 2> /dev/null || echo erro)"
  if [[ "$resultado" == "true" ]]; then
    printf '%-80s ok\n' "$descricao"
  else
    printf '%-80s FALHOU (jq devolveu: %s)\n' "$descricao" "$resultado"
    falhas=$((falhas + 1))
  fi
}

total=$((total + 1))
tamanho="$(printf '%s' "$policy" | tr -d ' \n\t\r' | wc -c | tr -d ' ')"
if (( tamanho <= limite )); then
  printf '%-80s ok\n' "tamanho $tamanho dentro do limite de $limite caracteres"
else
  printf '%-80s FALHOU\n' "tamanho $tamanho acima do limite de $limite caracteres"
  falhas=$((falhas + 1))
fi

confere "a policy é JSON com Statement" '.Statement | type == "array" and length > 0'

confere "nenhum Allow de RunTask ou StopTask usa Resource curinga" \
  '[.Statement[] | select(.Effect == "Allow") | select([.Action] | flatten | map(IN("ecs:RunTask", "ecs:StopTask")) | any) | select(.Resource == "*" or (.Resource | type) == "array")] | length == 0'

confere "RunTask só na task definition do migrator, com ecs:cluster do projeto" \
  "[.Statement[] | select(.Effect == \"Allow\") | select([.Action] | flatten | index(\"ecs:RunTask\"))]
   | length == 1
     and .[0].Action == \"ecs:RunTask\"
     and .[0].Resource == \"arn:aws:ecs:us-east-1:$conta:task-definition/renda-fixa-migrator:*\"
     and .[0].Condition.ArnEquals[\"ecs:cluster\"] == \"arn:aws:ecs:us-east-1:$conta:cluster/renda-fixa-api\""

confere "StopTask só em tarefas do cluster do projeto" \
  "[.Statement[] | select(.Effect == \"Allow\") | select([.Action] | flatten | index(\"ecs:StopTask\"))]
   | length == 1
     and .[0].Action == \"ecs:StopTask\"
     and .[0].Resource == \"arn:aws:ecs:us-east-1:$conta:task/renda-fixa-api/*\""

confere "PassRole continua só na execution role" \
  "[.Statement[] | select(.Action == \"iam:PassRole\")] | length == 1 and .[0].Resource == \"arn:aws:iam::$conta:role/renda-fixa-api-execution\""

# Ações que a rede restrita (saída da tarefa, SG padrão adotado sem regras) e o ALB exigem da role do CI.
# shellcheck disable=SC2016 # o $a é variável do jq, não do shell
confere "a role pode revogar e autorizar regras de entrada e saída dos security groups" \
  '[.Statement[] | select(.Effect == "Allow") | .Action | if type == "array" then .[] else . end] as $a
   | all(["ec2:AuthorizeSecurityGroupIngress", "ec2:AuthorizeSecurityGroupEgress", "ec2:RevokeSecurityGroupIngress", "ec2:RevokeSecurityGroupEgress", "ec2:ModifySecurityGroupRules"][]; . as $x | $a | index($x) != null)'

# shellcheck disable=SC2016 # o $a é variável do jq, não do shell
confere "a role pode ler security groups (o provider os lê ao adotar o padrão) e alterar atributos do ALB" \
  '[.Statement[] | select(.Effect == "Allow") | .Action | if type == "array" then .[] else . end] as $a
   | ($a | index("ec2:Describe*") != null) and ($a | index("elasticloadbalancing:ModifyLoadBalancerAttributes") != null)'

echo
if [[ $falhas -gt 0 ]]; then
  echo "$falhas de $total verificações falharam."
  exit 1
fi
echo "$total verificações ok."
