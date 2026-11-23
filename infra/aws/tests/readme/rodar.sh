#!/usr/bin/env bash
# Checagens do README na seção "Nuvem na AWS (F11)", só com bash, awk e grep:
#  (a) os blocos de código de "Lock preso" só usam bash, aws, cd e git: o CloudShell não traz o Terraform;
#  (b) todo `bash <caminho>.sh` da seção existe a partir da pasta definida pelo `cd` anterior (ou da raiz do
#      repositório, se não houve `cd`). O `cd` vale até o próximo título `##` ou `###`;
#  (c) a chave do lock citada em "Lock preso" é a do backend (environments/aws.backend.hcl) mais `.tflock`;
#  (d) o prazo do ambiente provisório não promete menos do que o agendado cumpre;
#  (e) a camada 4 (verificação de zerado) diz o alcance real: uma região, a lista de tipos do script, o que fica de fora.
# Cada regra recebe o caminho de um README; os casos com README sintético provam a regra, e os casos com o
# README real provam o documento. Uso: tests/readme/rodar.sh
set -uo pipefail

dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$dir/../../../.." && pwd)"
readme="${README:-$repo/README.md}"
backend="$repo/infra/aws/environments/aws.backend.hcl"
raiz="$(mktemp -d)"
trap 'rm -rf "$raiz"' EXIT

# Texto da subseção "### Lock preso", até o próximo título.
secao_lock_preso() {
  awk '/^### Lock preso/ { dentro = 1; next } dentro && /^##/ { exit } dentro { print }' "$1"
}

# (a) Primeira palavra de cada comando dos blocos de código da seção. Comandos são separados por &&, ||, ; e |;
# linhas de continuação (terminadas em \), atribuições (VAR=...) e comentários não contam como comando.
regra_lock_preso_sem_terraform() {
  local texto violacoes
  texto="$(secao_lock_preso "$1")"
  if [[ -z "$texto" ]]; then
    echo '  a seção "Lock preso" não foi encontrada'
    return 1
  fi
  violacoes="$(awk '
    /^[[:space:]]*```/ { em_bloco = !em_bloco; continuacao = 0; next }
    !em_bloco { next }
    {
      linha = $0
      sub(/^[[:space:]]+/, "", linha)
      if (linha == "" || linha ~ /^#/) { next }
      if (continuacao) { continuacao = (linha ~ /\\$/); next }
      continuacao = (linha ~ /\\$/)
      if (linha ~ /terraform/) { print "  comando com terraform: " linha }
      gsub(/&&|\|\||;|\|/, "\n", linha)
      n = split(linha, partes, "\n")
      for (i = 1; i <= n; i++) {
        parte = partes[i]
        sub(/^[[:space:]]+/, "", parte)
        if (parte == "" || parte ~ /^[A-Za-z_][A-Za-z0-9_]*=/) { continue }
        split(parte, palavras, /[[:space:]]+/)
        if (palavras[1] != "bash" && palavras[1] != "aws" && palavras[1] != "cd" && palavras[1] != "git") {
          print "  comando fora de bash/aws/cd/git: " palavras[1] " (linha: " parte ")"
        }
      }
    }
  ' <<< "$texto")"
  if [[ -n "$violacoes" ]]; then
    echo "$violacoes"
    return 1
  fi
}

# (b) Resolve um caminho do README para um caminho relativo à raiz do repositório. `~/renda-fixa-api` e
# `renda-fixa-api` (a pasta criada pelo `git clone` do bootstrap, a partir da raiz) são a própria raiz.
resolver_caminho() { # cwd caminho
  local cwd="$1" caminho="$2" til='~' # o `~` do README é texto, não o $HOME desta máquina
  case "$caminho" in
    "$til/renda-fixa-api") echo "."; return ;;
    "$til/renda-fixa-api/"*) echo "${caminho#"$til/renda-fixa-api/"}"; return ;;
    "renda-fixa-api") [[ "$cwd" == "." ]] && { echo "."; return; } ;;
    "renda-fixa-api/"*) [[ "$cwd" == "." ]] && { echo "${caminho#renda-fixa-api/}"; return; } ;;
  esac
  if [[ "$cwd" == "." ]]; then echo "$caminho"; else echo "$cwd/$caminho"; fi
}

regra_caminhos_bash_existem() {
  local arquivo="$1" dentro=0 em_bloco=0 cwd="." ok=0 linha resto alvo rel
  while IFS= read -r linha; do
    if ((!em_bloco)) && [[ "$linha" =~ ^##[[:space:]] ]]; then
      if [[ "$linha" == "## Nuvem na AWS"* ]]; then dentro=1; else ((dentro)) && break; fi
      cwd="."
      continue
    fi
    ((dentro)) || continue
    if ((!em_bloco)) && [[ "$linha" =~ ^###[[:space:]] ]]; then cwd="."; fi
    if [[ "$linha" =~ ^[[:space:]]*[\`]{3} ]]; then
      em_bloco=$((!em_bloco))
      continue
    fi
    if ((em_bloco)) && [[ "$linha" =~ ^[[:space:]]*cd[[:space:]]+([^[:space:]]+) ]]; then
      alvo="${BASH_REMATCH[1]}"
      rel="$(resolver_caminho "$cwd" "$alvo")"
      if [[ ! -d "$repo/$rel" ]]; then
        echo "  cd $alvo: a pasta não existe a partir de '$cwd'"
        ok=1
      else
        cwd="$rel"
      fi
    fi
    resto="$linha"
    while [[ "$resto" =~ bash[[:space:]]+([^[:space:]\`\"\'\)]+\.sh) ]]; do
      alvo="${BASH_REMATCH[1]}"
      rel="$(resolver_caminho "$cwd" "$alvo")"
      if [[ ! -f "$repo/$rel" ]]; then
        echo "  bash $alvo: o arquivo não existe a partir de '$cwd' (procurado em $rel)"
        ok=1
      fi
      resto="${resto#*"${BASH_REMATCH[0]}"}"
    done
  done < "$arquivo"
  if ((!dentro)); then
    echo '  a seção "Nuvem na AWS" não foi encontrada'
    ok=1
  fi
  return $ok
}

# (c) A chave do state no backend, mais .tflock, é o objeto do lock que o README manda remover.
regra_chave_do_lock_confere_com_o_backend() {
  local chave
  chave="$(sed -n 's/^key[[:space:]]*=[[:space:]]*"\(.*\)"/\1/p' "$backend")"
  if [[ -z "$chave" ]]; then
    echo "  não achei a chave no $backend"
    return 1
  fi
  if ! secao_lock_preso "$1" | grep -qF "$chave.tflock"; then
    echo "  a seção \"Lock preso\" não cita $chave.tflock"
    return 1
  fi
}

# (d) O ambiente provisório vive 1 h, e o agendado só roda a cada 30 min e leva um ciclo para destruir.
regra_prazo_provisorio_nao_promete_1h() {
  if grep -q 'destrói em até 1 h' "$1"; then
    echo '  o README ainda diz "destrói em até 1 h"'
    return 1
  fi
  if ! grep -q '1 h 30 min' "$1"; then
    echo '  o README não cita o prazo de cerca de 1 h 30 min'
    return 1
  fi
}

# (e) A verificação de zerado olha uma região e uma lista fechada de tipos; o README não pode prometer "tudo o que
# sobrou na conta". A linha da camada 4 diz o alcance (uma região, o script que tem a lista, o que fica de fora) e
# aponta o orçamento como rede de segurança.
regra_alcance_da_verificacao_de_zerado() {
  local linha trecho ok=0
  if grep -q 'tudo o que sobrou na conta' "$1"; then
    echo '  o README ainda diz "tudo o que sobrou na conta"'
    ok=1
  fi
  linha="$(grep '^| 4\. Verificação de zerado' "$1" || true)"
  if [[ -z "$linha" ]]; then
    echo '  a linha "4. Verificação de zerado" da tabela de camadas não foi encontrada'
    return 1
  fi
  if grep -q 'todo recurso pago' <<< "$linha"; then
    echo '  a camada 4 ainda promete "todo recurso pago"'
    ok=1
  fi
  for trecho in 'uma região' 'scripts/verificar-zerado.sh' 'outras regiões' 'orçamento'; do
    if [[ "$linha" != *"$trecho"* ]]; then
      echo "  a camada 4 não cita '$trecho'"
      ok=1
    fi
  done
  return $ok
}

falhas=0
total=0

# caso NOME ESPERADO(0 aprova, 1 reprova) REGRA ARQUIVO [TRECHO que deve aparecer na saída]
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

# README sintético: cabeçalho da seção, o corpo dado e o fechamento.
sintetico() { # nome corpo
  { printf '## Nuvem na AWS (F11)\n\n### Lock preso\n\n%s\n\n## Testes\n' "$2"; } > "$raiz/$1.md"
  echo "$raiz/$1.md"
}

# ---- (a) Lock preso sem terraform ----
caso readme_real_lock_preso_sem_terraform 0 regra_lock_preso_sem_terraform "$readme"

arq="$(sintetico com-terraform $'```bash\ncd renda-fixa-api/infra/aws\nterraform force-unlock ABC\n```')"
caso bloco_com_terraform_reprova 1 regra_lock_preso_sem_terraform "$arq" "terraform"

arq="$(sintetico com-sed $'```bash\naws s3 cp s3://b/k.tflock - | sed s/a/b/\n```')"
caso bloco_com_comando_fora_da_lista_reprova 1 regra_lock_preso_sem_terraform "$arq" "sed"

arq="$(sintetico so-permitidos $'```bash\ncd ~/renda-fixa-api/infra/aws\nBUCKET=x\naws s3 cp "s3://$BUCKET/k.tflock" -\naws s3 rm \\\n  "s3://$BUCKET/k.tflock"\ngit pull\nbash scripts/verificar-zerado.sh us-east-1\n```')"
caso bloco_so_com_bash_aws_cd_e_git_aprova 0 regra_lock_preso_sem_terraform "$arq"

arq="$(sintetico terraform-fora-de-bloco $'O Terraform não vem no CloudShell (terraform force-unlock não serve).\n\n```bash\naws s3 ls\n```')"
caso terraform_so_na_prosa_aprova 0 regra_lock_preso_sem_terraform "$arq"

# ---- (b) Caminhos de bash ----
caso readme_real_caminhos_bash_existem 0 regra_caminhos_bash_existem "$readme"

arq="$(sintetico caminho-errado-apos-cd $'```bash\ncd ~/renda-fixa-api/infra/aws\n```\n\nRode `bash infra/aws/scripts/verificar-zerado.sh us-east-1`.')"
caso caminho_de_raiz_depois_de_cd_em_infra_aws_reprova 1 regra_caminhos_bash_existem "$arq" "bash infra/aws/scripts/verificar-zerado.sh"

arq="$(sintetico caminho-certo-apos-cd $'```bash\ncd ~/renda-fixa-api/infra/aws\nbash scripts/verificar-zerado.sh us-east-1\n```')"
caso caminho_relativo_depois_de_cd_aprova 0 regra_caminhos_bash_existem "$arq"

arq="$(sintetico sem-cd $'Rode `bash infra/aws/scripts/verificar-zerado.sh`.')"
caso sem_cd_vale_a_raiz_do_repositorio 0 regra_caminhos_bash_existem "$arq"

arq="$(sintetico sem-cd-errado $'Rode `bash scripts/verificar-zerado.sh`.')"
caso sem_cd_caminho_de_infra_aws_reprova 1 regra_caminhos_bash_existem "$arq" "bash scripts/verificar-zerado.sh"

arq="$(sintetico script-inexistente $'```bash\nbash infra/aws/scripts/nao-existe.sh\n```')"
caso script_inexistente_reprova 1 regra_caminhos_bash_existem "$arq" "nao-existe.sh"

arq="$(sintetico cd-inexistente $'```bash\ncd ~/renda-fixa-api/infra/nada\n```')"
caso cd_para_pasta_inexistente_reprova 1 regra_caminhos_bash_existem "$arq" "a pasta não existe"

arq="$(sintetico clone-relativo $'```bash\nbash renda-fixa-api/infra/aws/bootstrap/bootstrap.sh --email x\n```')"
caso pasta_do_clone_equivale_a_raiz_do_repositorio 0 regra_caminhos_bash_existem "$arq"

cat > "$raiz/cd-expira.md" << 'MD'
## Nuvem na AWS (F11)

### Lock preso

```bash
cd ~/renda-fixa-api/infra/aws
```

### Validação

Rode `bash infra/aws/scripts/verificar-zerado.sh`.

## Testes
MD
caso cd_vale_so_ate_o_proximo_titulo 0 regra_caminhos_bash_existem "$raiz/cd-expira.md"

# ---- (c) Chave do lock ----
caso readme_real_chave_do_lock_confere_com_o_backend 0 regra_chave_do_lock_confere_com_o_backend "$readme"

arq="$(sintetico chave-errada $'```bash\naws s3 rm s3://b/outra/chave.tflock\n```')"
caso chave_do_lock_diferente_do_backend_reprova 1 regra_chave_do_lock_confere_com_o_backend "$arq" "não cita"

# ---- (d) Prazo do ambiente provisório ----
caso readme_real_prazo_provisorio_diz_1h30 0 regra_prazo_provisorio_nao_promete_1h "$readme"

printf 'O agendado o destrói em até 1 h.\n' > "$raiz/prazo-antigo.md"
caso prazo_de_1h_sem_a_folga_do_agendado_reprova 1 regra_prazo_provisorio_nao_promete_1h "$raiz/prazo-antigo.md" "destrói em até 1 h"

# ---- (e) Alcance da verificação de zerado ----
caso readme_real_alcance_da_verificacao_de_zerado 0 regra_alcance_da_verificacao_de_zerado "$readme"

printf '| 4. Verificação de zerado | Lista na conta, não no state, todo recurso pago e falha se achar algum. | Sim |\n' > "$raiz/camada4-antiga.md"
caso camada_4_que_promete_todo_recurso_pago_reprova 1 regra_alcance_da_verificacao_de_zerado "$raiz/camada4-antiga.md" "todo recurso pago"

printf 'A verificação lista tudo o que sobrou na conta.\n| 4. Verificação de zerado | uma região, scripts/verificar-zerado.sh, outras regiões, orçamento |\n' > "$raiz/sobrou.md"
caso frase_tudo_o_que_sobrou_na_conta_reprova 1 regra_alcance_da_verificacao_de_zerado "$raiz/sobrou.md" "tudo o que sobrou"

echo
if [[ $falhas -gt 0 ]]; then
  echo "$falhas de $total casos falharam."
  exit 1
fi
echo "$total casos ok."
