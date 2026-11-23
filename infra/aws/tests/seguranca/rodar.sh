#!/usr/bin/env bash
# Checagens estáticas das regras de security group em infra/aws/modules (só bash e awk). O terraform test prova
# cada regra nomeada, mas não enxerga uma regra NOVA que alguém acrescente; estas regras olham o código todo:
#  (a) nenhuma regra de entrada ou saída usa o protocolo -1 (todos);
#  (b) o SG das tarefas tem exatamente 2 regras de saída (443 para a internet e 5432 para o SG do RDS);
#  (c) nenhuma regra de saída aponta para 0.0.0.0/0 fora da porta 443;
#  (d) o security group padrão da VPC (aws_default_security_group) existe e não declara ingress nem egress.
# Cada regra recebe a pasta dos módulos; os casos mutantes copiam a árvore real e estragam um ponto.
# Uso: tests/seguranca/rodar.sh
set -uo pipefail

dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
modulos="${MODULOS:-$dir/../../modules}"
raiz="$(mktemp -d)"
trap 'rm -rf "$raiz"' EXIT

# "tipo|nome|atributo=valor;..." de cada bloco resource do tipo dado, sem aspas nos valores.
blocos() { # tipo pasta
  # shellcheck disable=SC2016 # o programa awk usa $0 e $3, que não são do shell
  find "$2" -name '*.tf' -print0 | xargs -0 awk -v tipo="$1" '
    $0 ~ "^resource \"" tipo "\" \"" {
      nome = $3; gsub(/"/, "", nome); dentro = 1; attrs = ""; next
    }
    dentro && /^}/ { print tipo "|" nome "|;" attrs; dentro = 0; next }
    dentro && /^[[:space:]]+[a-z_0-9]+[[:space:]]*=/ {
      linha = $0
      sub(/^[[:space:]]+/, "", linha)
      split(linha, kv, /[[:space:]]*=[[:space:]]*/)
      valor = substr(linha, length(kv[1]) + 1)
      sub(/^[[:space:]]*=[[:space:]]*/, "", valor)
      sub(/[[:space:]]*#.*$/, "", valor)
      gsub(/"/, "", valor)
      attrs = attrs kv[1] "=" valor ";"
    }
  '
}

regra_sem_protocolo_todos() {
  local achados
  achados="$( (blocos aws_vpc_security_group_egress_rule "$1"; blocos aws_vpc_security_group_ingress_rule "$1") | grep 'ip_protocol=-1;' || true)"
  if [[ -n "$achados" ]]; then
    echo "  regra com protocolo -1 (todos): ${achados%%;*}"
    return 1
  fi
}

regra_tarefa_tem_so_duas_saidas() {
  local quantas
  quantas="$(blocos aws_vpc_security_group_egress_rule "$1" | grep -c ';security_group_id=aws_security_group.task.id;' || true)"
  if [[ "$quantas" -ne 2 ]]; then
    echo "  o SG das tarefas tem $quantas regras de saída (esperado: exatamente 2)"
    return 1
  fi
}

regra_saida_aberta_so_em_443() {
  local linha ok=0
  # Sem from_port/to_port (regra para todas as portas) também conta como fora da 443.
  while IFS= read -r linha; do
    if [[ "$linha" == *'cidr_ipv4=0.0.0.0/0;'* && ( "$linha" != *'from_port=443;'* || "$linha" != *'to_port=443;'* ) ]]; then
      echo "  saída para 0.0.0.0/0 fora da porta 443: ${linha%%;*}"
      ok=1
    fi
  done < <(blocos aws_vpc_security_group_egress_rule "$1")
  return $ok
}

regra_default_sg_sem_regras() {
  local arquivo ok=1
  arquivo="$(grep -rl '^resource "aws_default_security_group"' "$1" --include='*.tf' || true)"
  if [[ -z "$arquivo" ]]; then
    echo "  não há aws_default_security_group: a VPC fica com o SG padrão e suas regras abertas"
    return 1
  fi
  ok=0
  if awk '/^resource "aws_default_security_group"/ { dentro = 1; next } dentro && /^}/ { dentro = 0 }
    dentro && /^[[:space:]]*(ingress|egress)[[:space:]]*(\{|=)/ { achou = 1 } END { exit !achou }' "$arquivo"; then
    echo "  o aws_default_security_group declara ingress ou egress: deve ficar sem nenhuma regra"
    ok=1
  fi
  return $ok
}

falhas=0
total=0

# caso NOME ESPERADO(0 aprova, 1 reprova) REGRA PASTA [TRECHO]
caso() {
  local nome="$1" esperado="$2" regra="$3" pasta="$4" trecho="${5:-}" saida codigo motivo=""
  if [[ -n "${FILTRO:-}" && ! "$nome" =~ $FILTRO ]]; then
    return 0
  fi
  total=$((total + 1))
  saida="$("$regra" "$pasta" 2>&1)"
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

# Cópia da árvore real para um mutante.
copia() { # nome
  mkdir -p "$raiz/$1"
  cp -r "$modulos/." "$raiz/$1/"
  echo "$raiz/$1"
}

# ---- (a) protocolo -1 ----
caso modulos_reais_sem_protocolo_todos 0 regra_sem_protocolo_todos "$modulos"
m="$(copia mut-protocolo)"
sed -i '0,/ip_protocol[[:space:]]*=[[:space:]]*"tcp"/s//ip_protocol = "-1"/' "$m/network/main.tf"
caso regra_com_protocolo_todos_reprova 1 regra_sem_protocolo_todos "$m" "protocolo -1"

# ---- (b) duas saídas da tarefa ----
caso modulos_reais_tarefa_tem_so_duas_saidas 0 regra_tarefa_tem_so_duas_saidas "$modulos"
m="$(copia mut-terceira-saida)"
cat >> "$m/network/main.tf" << 'TF'

resource "aws_vpc_security_group_egress_rule" "task_extra" {
  security_group_id = aws_security_group.task.id
  ip_protocol       = "tcp"
  from_port         = 53
  to_port           = 53
  cidr_ipv4         = "10.0.0.0/16"
}
TF
caso terceira_saida_da_tarefa_reprova 1 regra_tarefa_tem_so_duas_saidas "$m" "3 regras"

# ---- (c) saída aberta só em 443 ----
caso modulos_reais_saida_aberta_so_em_443 0 regra_saida_aberta_so_em_443 "$modulos"
m="$(copia mut-saida-80)"
cat >> "$m/network/main.tf" << 'TF'

resource "aws_vpc_security_group_egress_rule" "alb_extra" {
  security_group_id = aws_security_group.alb.id
  ip_protocol       = "tcp"
  from_port         = 80
  to_port           = 80
  cidr_ipv4         = "0.0.0.0/0"
}
TF
caso saida_aberta_na_porta_80_reprova 1 regra_saida_aberta_so_em_443 "$m" "fora da porta 443"

# ---- (d) security group padrão sem regras ----
caso modulos_reais_default_sg_sem_regras 0 regra_default_sg_sem_regras "$modulos"
m="$(copia mut-default-com-regra)"
sed -i 's/^resource "aws_default_security_group" "\([a-z_]*\)" {/&\n  ingress {\n    protocol  = "-1"\n    self      = true\n    from_port = 0\n    to_port   = 0\n  }/' "$m/network/main.tf"
caso default_sg_com_ingress_reprova 1 regra_default_sg_sem_regras "$m" "declara ingress"
m="$(copia mut-sem-default)"
sed -i 's/^resource "aws_default_security_group"/resource "aws_security_group_rule_removida"/' "$m/network/main.tf"
caso vpc_sem_default_sg_adotado_reprova 1 regra_default_sg_sem_regras "$m" "não há aws_default_security_group"

echo
if [[ $falhas -gt 0 ]]; then
  echo "$falhas de $total casos falharam."
  exit 1
fi
echo "$total casos ok."
