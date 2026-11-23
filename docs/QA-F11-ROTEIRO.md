# Roteiro de QA — F11: Nuvem na AWS (Terraform, travas, workflow e imagem de migração)

Roteiro exploratório para o agente `qa-exploratorio`. O relatório vai em `docs/QA-F11.md`.
**Status: aguardando aprovação do Matheus** (ver "Decisões antes de executar", no fim).

A F11 não tem endpoint nem regra de negócio nova. Entrega o Terraform de `infra/aws/`, a imagem de
migração (`Dockerfile.migrator`), o workflow `infra-aws.yml` (validar, subir, destruir, limpar e
agendado), o `bootstrap.sh` do CloudShell e os scripts `trava-custo.sh`, `verificar-zerado.sh`,
`verificar-zerado-apos-destroy.sh`, `bloquear-recriacao.sh`, `parar-migracao.sh` e
`gerar-floci-override.sh`. A regra mais importante é de custo: **nada pode ficar ligado esquecido**.

Por isso o "usuário" desta fase não é quem chama a API. São dois:

- **o operador**: o Matheus daqui a três meses, seguindo só o README, rodando o bootstrap, os scripts
  na mão e o roteiro de "Lock preso" depois de um susto;
- **o agendado**: o job que roda a cada 30 min sem ninguém olhando e decide sozinho se destrói.

A pergunta que guia cada cenário: **como um dos dois poderia acabar com algo ligado, cobrança,
segredo vazado, ou um vermelho que ninguém entende?**

A conta da AWS ainda não existe (P0) e a validação ao vivo (P8) é com o Matheus. Este QA roda **sem
AWS real e sem GitHub**: só local, com o emulador Floci, contêineres e as próprias ferramentas.

## O que a validação já provou (não se repete aqui)

A `docs/VALIDACAO-F11.md` (19/11/2026) provou, e este roteiro não refaz:

- `fmt`, `validate` e 54 casos de `terraform test`, no Windows e no Linux;
- as quatro suítes de shell (`trava-custo` 53, `verificar-zerado` 19 com shim, `bloquear-recriacao`
  15, `bootstrap` 6) e o shellcheck dos 11 scripts;
- um ciclo completo no Floci no caminho feliz (27 recursos criados e destruídos), com trava,
  bloqueio de recriação, artefatos D1/D2 do Floci e verificação de zerado;
- a trava reprovando NAT, Multi-AZ e `desired_count = 2` num plano real alterado com `jq`;
- a verificação reprovando o ambiente aplicado e um secret avulso, aprovando o vazio e saindo 2 com
  endpoint inexistente;
- a imagem de migração num PostgreSQL limpo, a segunda execução idempotente e a API subindo depois;
- senha e connection string com contagem zero no state, no plano e no `terraform show -json`;
- busca de segredos no repositório, `actionlint`, `zizmor`, `dotnet build` e 712 testes.

## O que a validação não cobriu (foco deste roteiro)

- os scripts usados como o README ensina, mas com entradas fora do caminho feliz: argumento ausente
  ou errado, região errada, endpoint lento ou fora do ar, credencial ausente; códigos de saída,
  `stdout`/`stderr` e mensagens em português;
- a trava de custo contra **planos reais** com mudanças que um operador faria (classe do banco,
  disco, CPU, recurso novo num módulo, proteção contra exclusão), e contra planos de `destroy`,
  prorrogação e troca de SHA;
- a verificação de zerado contra resíduos que a spec cita e que **não** cita (nome parecido, tag
  diferente, outra região, tipos fora da lista), e o tempo que ela leva;
- ciclos no Floci fora do caminho feliz: subir duas vezes (prorrogar), `plan` depois do `apply`,
  `destroy` com marcações, com state ausente ou parcial, `apply` interrompido, região trocada;
- os passos de shell do workflow extraídos do YAML e rodados com entradas adversas, e as corridas
  entre o agendado e um `subir`;
- o `bootstrap.sh` contra o Floci: idempotência, e-mail inválido, rodar duas vezes, policy;
- o `Dockerfile.migrator` com banco fora do ar, sem privilégio, com TLS obrigatório, interrompido,
  e o que vaza para o log (que o `subir` imprime no Actions de um repositório público);
- o README seguido ao pé da letra, e a coerência entre README, spec, validação e cabeçalhos dos
  scripts.

## Regras herdadas do agente, adaptadas à fase

- **Caixa-preta primeiro.** Use os scripts como o README e o cabeçalho de cada um ensinam. Abra o
  código de `infra/aws/` só para montar uma mutação (achar a linha a trocar na **cópia**) ou para
  explicar um achado já reproduzido.
- **Tudo reproduzível.** Cada achado traz o comando exato, a saída obtida (sem segredo), o código de
  saída, o esperado e a fonte da expectativa (spec, README, cabeçalho do script, bom senso).
- **Nada é corrigido.** O único arquivo que o QA cria no repositório é `docs/QA-F11.md`.
- **Não destrua dados de ninguém.** Tudo o que o QA cria tem prefixo `qa11` e, quando aceita tag,
  `qa=f11`. Nada de `docker compose down -v`, nada de apagar volume ou imagem que já existia.
- **Fail open é sempre achado.** Script que sai 0 (ou job que ficaria verde) sem ter conseguido
  verificar é achado, como um 500 seria na API.
- **Não invente resultado.** O que o Floci não suportar (serviço, operação, comportamento), ou o que
  depender da AWS real ou do GitHub, fica `NÃO EXECUTADO` com o motivo, e vai para a lista do P8.
- **Segredo nunca na tela.** Nenhum comando do QA imprime senha, connection string ou o JSON do
  plano. Para conferir um segredo, compare hash ou conte ocorrências.

## Ambiente

### O que precisa estar de pé

- **Docker Desktop** aberto, engine Linux. Nada é instalado na máquina: as ferramentas vêm de
  imagens, e o que roda no Windows é só o Git Bash (com `git`, GNU `date`, `awk`, `tar`).
- **Floci** `floci/floci:2.5.0@sha256:a0fbf944d7f338cc0856f1ef83831ea749ff2be71956eb7a7fce08fc21458e19`,
  sempre com `-u root` e o socket do Docker montado (ECS e RDS do Floci rodam como contêineres
  filhos). Já está no cache local.
- **AWS CLI** `amazon/aws-cli@sha256:538a9a80bc7c78e3228bc65100404a5dbd89ad812a547004c05ea20109543432`
  (no cache como `amazon/aws-cli:latest`, mesmo digest).
- **jq** `ghcr.io/jqlang/jq@sha256:096b83865ad59b5b02841f103f83f45c51318394331bf1995e187ea3be937432`
  (1.7.1, no cache).
- **Terraform 1.17.0**. No Windows ele existe (`terraform version`: 1.17.0, `windows_amd64`), mas os
  scripts precisam de Linux, `bash` 5, `aws` e `jq` juntos, como no `ubuntu-24.04`. Por isso o QA
  monta uma **imagem de execução** (`qa11-runner:tmp`) com o binário Linux do Terraform 1.17.0
  copiado de `hashicorp/terraform:1.17.0`, que **não está no cache** (decisão 1). O Terraform do
  Windows só é usado se a decisão 1 for negada (ver "Caminho alternativo").
- **Provider** `hashicorp/aws` 6.73.0 para Linux: o `terraform init` baixa do registry (o
  `.terraform/` do repositório tem só o do Windows). Cache em volume próprio de cada trilha.
- **PostgreSQL descartável** `postgres:16@sha256:f9a7b9ebf6f56201f949db745e47bc423c0f0f8294bafe991cc10bf74b029657`
  (no cache), para a imagem de migração e a API.
- **Bundle e imagens .NET** (só a trilha D): `mcr.microsoft.com/dotnet/sdk:8.0` (no cache) para gerar
  o `efbundle`; `mcr.microsoft.com/dotnet/runtime-deps:8.0` para montar a imagem de migração (**não**
  está no cache, decisão 1); a imagem da API conforme a decisão 2.

### O que não pode ser tocado

- **Os contêineres `fixedincome-api` (porta 8080) e `fixedincome-db` (porta 5432) do Matheus**, que
  estavam no ar em 19/11/2026. Não parar, não reiniciar, não `exec`, não conectar no banco deles, não
  usar as portas 5432 e 8080. O PostgreSQL do QA é sempre um contêiner `qa11-pg*` próprio, numa rede
  `qa11-net`, sem porta publicada (ou na 25432, se precisar do host). A API do QA, se subir, usa a
  28080.
- **A imagem `renda-fixa-api-api:latest`** (do `docker compose` do Matheus): no máximo leitura (rodar
  um contêiner novo a partir dela, se a decisão 2 permitir). Nunca remover nem retaguear.
- **O repositório** (`c:\Users\matheuss\Desktop\renda-fixa\renda-fixa-api`): só leitura. Nada de
  `terraform init`, `gerar-floci-override.sh`, `dotnet build` ou `docker build` nele: o
  `floci_override.tf`, o `floci.tfstate`, o `.terraform/` e o `bin/obj` iriam parar na árvore dele.
  Todo comando que gera arquivo roda numa **cópia** (volume Docker ou pasta de rascunho).
- **A AWS real e o GitHub.** Nenhuma chamada ao GitHub (nem `gh`, nem `git clone`/`ls-remote`, nem
  `ghcr.io`) e nenhuma à AWS real. Rede de segurança: a imagem de execução roda sempre com
  `AWS_ENDPOINT_URL` apontando para o Floci, credenciais fictícias `test`/`test`,
  `AWS_CONFIG_FILE=/dev/null` e `AWS_SHARED_CREDENTIALS_FILE=/dev/null`, e o Terraform só roda numa
  pasta com `floci_override.tf`. Mesmo que algo escape para a AWS, a credencial fictícia é recusada.
  Os passos com `gh`, `docker buildx` e `curl` para o GHCR rodam com **shims** num contêiner sem rede.
- **O Docker Desktop**: não parar, não resetar, não limpar cache.

### Preparação (Git Bash)

No PowerShell 5.1, `curl` é alias de `Invoke-WebRequest`: use o Git Bash para tudo.

0. **Variáveis e foto do estado** (só leitura):

   ```bash
   export MSYS_NO_PATHCONV=1
   REPO=/c/Users/matheuss/Desktop/renda-fixa/renda-fixa-api
   QA="<pasta de rascunho da sessão>/qaf11"     # nunca dentro do repositório
   mkdir -p "$QA"/{passos/shims/tf,passos/shims/gh,passos/shims/ghcr,passos/shims/docker,passos/fixtures,ctx,saidas}
   WQA="$(cygpath -w "$QA")"
   cd "$REPO" && git rev-parse HEAD && git status --short > "$QA/git-status-antes.txt"
   ls -la --time-style=full-iso infra/aws/.terraform > "$QA/terraform-dir-antes.txt" 2>&1
   docker ps -a --format '{{.Names}} {{.Image}} {{.Status}} {{.Ports}}' > "$QA/antes-ps.txt"
   docker images --digests --format '{{.Repository}}:{{.Tag}} {{.Digest}}' | sort > "$QA/antes-images.txt"
   docker volume ls -q | sort > "$QA/antes-volumes.txt"
   docker network ls --format '{{.Name}}' | sort > "$QA/antes-networks.txt"
   netstat -ano | grep -E ':(4566|4567|25432|28080) ' || echo "portas do QA livres"
   docker info --format 'memoria={{.MemTotal}} cpus={{.NCPU}}'
   ```

   Esperado: 4566, 4567, 25432 e 28080 livres; `fixedincome-api` e `fixedincome-db` na lista.
   Registre memória e CPUs (duas trilhas com Floci e RDS filhos pedem pelo menos 6 GB livres).

1. **Cópia da árvore de trabalho.** A fase não está commitada (HEAD `f141590` é anterior a ela). A
   cópia leva o que entraria num commit: rastreados mais não rastreados que o `.gitignore` não ignora.

   ```bash
   mkdir -p "$QA/arvore" && cd "$REPO" && git ls-files -co --exclude-standard -z | tar --null -T - -cf - | tar -xf - -C "$QA/arvore"
   test ! -e "$QA/arvore/infra/aws/.terraform" && test ! -e "$QA/arvore/infra/aws/floci_override.tf" && echo "cópia limpa"
   grep -rlI $'\r' "$QA/arvore/infra" "$QA/arvore/.github/workflows/infra-aws.yml" "$QA/arvore/Dockerfile.migrator" || echo "sem CRLF"
   ```

   Se aparecer arquivo com CRLF, registre (um `.sh` com CRLF quebra no `bash` do CloudShell e do
   runner) e converta **só na cópia**.

2. **Imagem de execução** (`qa11-runner:tmp`):

   ```bash
   cat > "$QA/Dockerfile.runner" <<'EOF'
   FROM amazon/aws-cli@sha256:538a9a80bc7c78e3228bc65100404a5dbd89ad812a547004c05ea20109543432
   COPY --from=hashicorp/terraform:1.17.0 /bin/terraform /usr/local/bin/terraform
   COPY --from=ghcr.io/jqlang/jq@sha256:096b83865ad59b5b02841f103f83f45c51318394331bf1995e187ea3be937432 /jq /usr/local/bin/jq
   ENTRYPOINT []
   EOF
   docker build -t qa11-runner:tmp -f "$WQA\\Dockerfile.runner" "$WQA\\ctx"
   docker run --rm qa11-runner:tmp bash -c 'terraform version | head -1; jq --version; aws --version; bash --version | head -1; date --version | head -1'
   ```

   Esperado: Terraform 1.17.0 `linux_amd64`, jq 1.7.1, aws-cli 2.x, bash 5.x, GNU coreutils. Se o
   binário do jq não estiver em `/jq`, ache-o com `docker create` + `docker export | tar -t`.
   Registre o digest de `hashicorp/terraform:1.17.0` que foi baixado.

3. **Floci e runners.** Três contêineres de execução: `a` e `b` compartilham a rede do seu Floci (lá
   dentro, `localhost:4566` é o Floci, como no runner do GitHub); `c` não tem rede nenhuma (shims).

   ```bash
   porta() { [ "$1" = a ] && echo 4566 || echo 4567; }
   sobe_floci() { # a|b
     docker run -d --name "qa11$1-floci" -u root -p "$(porta "$1")":4566 \
       -v /var/run/docker.sock:/var/run/docker.sock \
       floci/floci@sha256:a0fbf944d7f338cc0856f1ef83831ea749ff2be71956eb7a7fce08fc21458e19
     local limite=$((SECONDS + 90))
     until curl -s -o /dev/null --max-time 2 "http://localhost:$(porta "$1")/"; do
       [ $SECONDS -ge $limite ] && { echo "Floci $1 não respondeu"; docker logs --tail 50 "qa11$1-floci"; return 1; }
       sleep 1
     done
   }
   ENVS=(-e AWS_ENDPOINT_URL=http://localhost:4566 -e AWS_ACCESS_KEY_ID=test -e AWS_SECRET_ACCESS_KEY=test
         -e AWS_REGION=us-east-1 -e AWS_DEFAULT_REGION=us-east-1 -e AWS_PAGER= -e AWS_EC2_METADATA_DISABLED=true
         -e AWS_CONFIG_FILE=/dev/null -e AWS_SHARED_CREDENTIALS_FILE=/dev/null
         -e TF_PLUGIN_CACHE_DIR=/plugins -e TF_IN_AUTOMATION=true -e RUNNER_TEMP=/tmp)
   sobe_runner() { # a|b|c
     local rede="container:qa11$1-floci"; [ "$1" = c ] && rede=none
     docker volume create "qa11$1-work" > /dev/null; docker volume create "qa11$1-plugins" > /dev/null
     docker run -d --name "qa11$1-runner" --network "$rede" -v "qa11$1-work:/work" -v "qa11$1-plugins:/plugins" \
       -v "$WQA\\passos:/passos:ro" "${ENVS[@]}" qa11-runner:tmp sleep infinity
     docker exec "qa11$1-runner" mkdir -p /work/repo
     docker cp "$QA/arvore/." "qa11$1-runner:/work/repo"
   }
   rx() { docker exec -i -w "${3:-/work/repo/infra/aws}" "qa11$1-runner" bash -c "$2"; }  # trilha "comando" [pasta]
   ra() { rx a "$@"; }; rb() { rx b "$@"; }; rc() { rx c "$@"; }
   sobe_floci a && sobe_runner a          # trilha A
   sobe_floci b && sobe_runner b          # trilha B (só se for rodar em paralelo ou quando chegar nela)
   sobe_runner c                          # trilha C, sem rede
   ```

   Se o Floci de uma trilha for recriado (`docker rm -f` e `sobe_floci` de novo), recrie também o
   runner dela: o runner fica preso ao namespace de rede do Floci antigo.

4. **Scripts auxiliares do QA** (em `$QA/passos`, montado só leitura em `/passos`). São do QA, não
   da fase; registre no relatório que existem.

   ```bash
   cat > "$QA/passos/init.sh" <<'EOF'
   #!/usr/bin/env bash
   # Na pasta infra/aws de uma cópia, dentro de um runner com Floci: override, init e a execution role (substituta do bootstrap).
   set -eo pipefail
   bash scripts/gerar-floci-override.sh "$AWS_ENDPOINT_URL" > /dev/null
   terraform init -input=false -reconfigure -lock-timeout=5m > /tmp/init.log
   aws iam get-role --role-name renda-fixa-api-execution > /dev/null 2>&1 || aws iam create-role --role-name renda-fixa-api-execution \
     --assume-role-policy-document '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"Service":"ecs-tasks.amazonaws.com"},"Action":"sts:AssumeRole"}]}' > /dev/null
   echo "init: ok em $PWD"
   EOF
   cat > "$QA/passos/sobe.sh" <<'EOF'
   #!/usr/bin/env bash
   # Uso: bash /passos/sobe.sh [expires_at] [service_desired_count]. Plano, trava, bloqueio e apply, como o validar.
   set -eo pipefail
   test -f floci_override.tf || { echo "ABORTADO: sem floci_override.tf nesta pasta"; exit 9; }
   expira="${1:-$(date -u -d '+2 hours' +%Y-%m-%dT%H:%M:%SZ)}"; quantas="${2:-0}"
   terraform plan -input=false -lock-timeout=5m -var-file=environments/floci.tfvars \
     -var "expires_at=$expira" -var "service_desired_count=$quantas" -out=floci.tfplan > /tmp/plan.log
   terraform show -json floci.tfplan | bash scripts/trava-custo.sh
   terraform show -json floci.tfplan | bash scripts/bloquear-recriacao.sh
   terraform apply -input=false -lock-timeout=5m floci.tfplan > /tmp/apply.log
   echo "sobe: ok (expires_at=$expira, desired=$quantas)"
   EOF
   cat > "$QA/passos/desce.sh" <<'EOF'
   #!/usr/bin/env bash
   # Uso: bash /passos/desce.sh [expires_at] [image_sha]. Padrão: as marcações do destruir. Depois, os artefatos D1/D2 do Floci.
   set -eo pipefail
   test -f floci_override.tf || { echo "ABORTADO: sem floci_override.tf nesta pasta"; exit 9; }
   terraform destroy -auto-approve -input=false -lock-timeout=5m -var-file=environments/floci.tfvars \
     -var "expires_at=${1:-1970-01-01T00:00:00Z}" -var "image_sha=${2:-0000000000000000000000000000000000000000}" \
     -var service_desired_count=0 > /tmp/destroy.log
   tail -n 1 /tmp/destroy.log
   bash /passos/artefatos.sh
   EOF
   cat > "$QA/passos/segredo.sh" <<'EOF'
   #!/usr/bin/env bash
   # Só hash e tamanho do valor do secret e as versões. Nunca o valor.
   set -eo pipefail
   v="$(aws secretsmanager get-secret-value --secret-id renda-fixa-api/connection-string --query SecretString --output text)"
   printf 'sha256=%s tamanho=%s\n' "$(printf '%s' "$v" | sha256sum | cut -c1-16)" "${#v}"
   aws secretsmanager list-secret-version-ids --secret-id renda-fixa-api/connection-string --query 'Versions[].[VersionId,join(`,`,VersionStages)]' --output text
   EOF
   ```

5. **Extração dos passos do workflow.** Cada passo de shell do `infra-aws.yml` é copiado do YAML do
   repositório (sem alteração) para um arquivo e rodado como o Actions roda (`bash --noprofile --norc
   -eo pipefail`), com `GITHUB_OUTPUT` e `GITHUB_STEP_SUMMARY` em arquivos temporários e o `env:` de
   nível de workflow reproduzido.

   ```bash
   WF="$REPO/.github/workflows/infra-aws.yml"
   passo() { # "nome exato do passo" [ocorrência, padrão 1]: imprime o bloco `run: |`, sem a indentação
     awk -v alvo="$1" -v n="${2:-1}" '
       function nome(l) { sub(/^ *- name: */, "", l); gsub(/^"|"$/, "", l); return l }
       /^ *- name:/ { if (dentro || achou) exit; if (nome($0) == alvo && ++vistas == n) achou = 1; next }
       achou && !dentro && /^ *run: [|]$/ { match($0, /^ */); ind = RLENGTH; dentro = 1; next }
       dentro { if ($0 ~ /^[[:space:]]*$/) { print ""; next }
                match($0, /^ */); if (RLENGTH <= ind) exit; print substr($0, ind + 3) }' "$WF"
   }
   P="$QA/passos"
   passo "Floci: remover artefatos que o emulador não apaga" > "$P/artefatos.sh"
   passo "Validar entradas"                                 > "$P/wf-validar-entradas.sh"
   passo "Guarda de 45 dias sem commit"                     > "$P/wf-guarda-45.sh"
   passo "Resolver o SHA da imagem"                         > "$P/wf-resolver-sha.sh"
   passo "Conferir as imagens no GHCR (sem login)"          > "$P/wf-ghcr.sh"
   passo "Calcular expires_at"                              > "$P/wf-prazo.sh"
   passo "Ler expires_at e image_sha do state"              > "$P/wf-ler-saidas.sh"          # destruir
   passo "Ler expires_at e image_sha do state" 2            > "$P/wf-ler-saidas-limpar.sh"   # limpar
   passo "Guarda - execução manual em andamento"            > "$P/wf-guarda-gh.sh"
   passo "Ler o state"                                      > "$P/wf-ler-state.sh"
   passo "Ambiente ativo - checar o TTL"                    > "$P/wf-ttl.sh"
   passo "Migração - esperar o fim e exigir exitCode 0"     > "$P/wf-esperar-migracao.sh"
   wc -l "$P"/wf-*.sh "$P/artefatos.sh"                     # nenhum com 0 linhas
   diff "$P/wf-ler-saidas.sh" "$P/wf-ler-saidas-limpar.sh" && echo "destruir e limpar leem o state igual"

   wf() { # trilha "VAR='valor' ..." passo.sh [pasta]: roda o passo como o Actions e mostra código, GITHUB_OUTPUT e resumo
     rx "$1" "rm -f /tmp/gho /tmp/ghs; touch /tmp/gho /tmp/ghs
       env $2 GITHUB_OUTPUT=/tmp/gho GITHUB_STEP_SUMMARY=/tmp/ghs IMAGE=ghcr.io/matheuscardosolazzarini/renda-fixa-api \
         SERVICE=renda-fixa-api LOG_GROUP=/ecs/renda-fixa-api MARCACAO_EXPIRES_AT=1970-01-01T00:00:00Z \
         MARCACAO_IMAGE_SHA=0000000000000000000000000000000000000000 TF_IN_AUTOMATION=true AWS_PAGER= \
         bash --noprofile --norc -eo pipefail /passos/$3
       echo \"codigo=\$?\"; echo '--- GITHUB_OUTPUT'; cat /tmp/gho; echo '--- RESUMO'; cat /tmp/ghs" "${4:-/work/repo/infra/aws}"
   }
   ```

   Se um passo sair vazio, o nome mudou ou o bloco é `run: >` (dobrado): leia o trecho no YAML e
   extraia à mão, registrando como foi feito.

6. **Shims** (só para a trilha C, sem rede). Arquivos em `$QA/passos/shims/<ferramenta>/<comando>`;
   cada cenário põe a pasta certa na frente do `PATH`. Se o bind mount do Windows não preservar o bit
   de execução, copie os shims para dentro do runner `c` e aplique `chmod +x` lá.

   ```bash
   cat > "$QA/passos/shims/tf/terraform" <<'EOF'
   #!/usr/bin/env bash
   # terraform falso: só "output -raw <nome>". FALSO_EXPIRES / FALSO_SHA definidos = devolve o valor; indefinidos = erro de output ausente.
   if [[ "$1 $2" == "output -raw" ]]; then
     case "$3" in
       expires_at) [[ -n "${FALSO_EXPIRES+x}" ]] && { printf '%b' "$FALSO_EXPIRES"; exit 0; } ;;
       image_sha)  [[ -n "${FALSO_SHA+x}" ]] && { printf '%b' "$FALSO_SHA"; exit 0; } ;;
     esac
     printf 'Error: Output "%s" not found\n' "$3" >&2; exit 1
   fi
   echo "terraform falso: comando não previsto: $*" >&2; exit 99
   EOF
   cat > "$QA/passos/shims/gh/gh" <<'EOF'
   #!/usr/bin/env bash
   # gh falso: FALSO_GH_<STATUS> é o número devolvido para --status <status> (padrão 0; VAZIO = saída vazia). FALSO_GH_FALHA=<status>|todos = erro.
   status=""; for ((i = 1; i <= $#; i++)); do [[ "${!i}" == --status ]] && { j=$((i + 1)); status="${!j}"; }; done
   [[ "${FALSO_GH_FALHA:-}" == "$status" || "${FALSO_GH_FALHA:-}" == todos ]] && { echo "HTTP 502: Bad Gateway (https://api.github.com/...)" >&2; exit 1; }
   var="FALSO_GH_${status^^}"; valor="${!var:-0}"; [[ "$valor" == VAZIO ]] && valor=""
   printf '%s\n' "$valor"
   EOF
   cat > "$QA/passos/shims/ghcr/curl" <<'EOF'
   #!/usr/bin/env bash
   # curl falso para o passo do GHCR. FALSO_TOKEN_JSON (padrão {"token":"tok"}; VAZIO = corpo vazio), FALSO_TOKEN_FALHA=1,
   # FALSO_HTTP_API e FALSO_HTTP_MIGRATOR (padrão 200; 000 = timeout).
   args="$*"
   case "$args" in
     *ghcr.io/token*)
       [[ -n "${FALSO_TOKEN_FALHA:-}" ]] && { echo "curl: (6) Could not resolve host: ghcr.io" >&2; exit 6; }
       padrao='{"token":"tok"}'; corpo="${FALSO_TOKEN_JSON:-$padrao}"; [[ "$corpo" == VAZIO ]] && corpo=""; printf '%s' "$corpo" ;;
     *manifests/*-migrator*) c="${FALSO_HTTP_MIGRATOR:-200}"; printf '%s' "$c"; [[ "$c" == 000 ]] && exit 28; exit 0 ;;
     *manifests/*) c="${FALSO_HTTP_API:-200}"; printf '%s' "$c"; [[ "$c" == 000 ]] && exit 28; exit 0 ;;
     *) echo "curl falso: chamada não prevista: $args" >&2; exit 99 ;;
   esac
   EOF
   cat > "$QA/passos/shims/docker/docker" <<'EOF'
   #!/usr/bin/env bash
   # docker falso: só "buildx imagetools inspect". Devolve /passos/fixtures/$FALSO_IMAGETOOLS; FALSO_DOCKER_FALHA=1 = erro.
   [[ "$1 $2 $3" == "buildx imagetools inspect" ]] || { echo "docker falso: não previsto: $*" >&2; exit 99; }
   [[ -n "${FALSO_DOCKER_FALHA:-}" ]] && { echo "ERROR: ghcr.io/matheuscardosolazzarini/renda-fixa-api:latest: not found" >&2; exit 1; }
   cat "/passos/fixtures/${FALSO_IMAGETOOLS:?defina FALSO_IMAGETOOLS}"
   EOF
   ```

   As fixtures do `imagetools` (formato a confirmar no P8, `docker buildx imagetools inspect --format
   '{{json .Image}}'`) ficam no QA11-20.

### Caminho alternativo, se a decisão 1 for negada

Sem `hashicorp/terraform:1.17.0`, o Terraform do Windows roda no Git Bash contra o Floci publicado
em `localhost:4566`, numa cópia em `$QA/arvore-win/infra/aws`, e `aws`/`jq` viram scripts no `PATH`
que chamam as imagens (`docker run --rm -i --network container:qa11a-floci ...`). Funciona, mas cada
chamada da CLI vira um contêiner: os tempos medidos (QA11-02, QA11-26) não valem como referência e
são marcados assim no relatório. A trilha D não depende disso.

## Cenários

Legenda: **BUG** quando o resultado contraria a spec, o README, o cabeçalho do script ou uma regra
do agente; **RISCO** quando é defensável pela spec, mas surpreende o operador, depende de um
comportamento que só a AWS ou o GitHub confirmam, ou é um caso que a spec não previu (ver decisão 5).

Severidade dos bugs no relatório:

- **alta**: algo pago fica ligado ou fora da vista (verificação que aprova com recurso pago na região
  verificada, `destroy` que "passa" e deixa recurso, agendado verde com ambiente vencido, trava que
  aprova o que a spec proíbe, caminho automático que destrói o ambiente errado); senha ou connection
  string em saída, log, state ou plano impresso; script que sai 0 sem ter verificado (fail open).
- **média**: vermelho sem mensagem acionável; código de saída diferente do documentado; comando do
  README que falha copiado literalmente; mensagem enganosa (culpa a coisa errada); limitação que
  surpreende o operador e não está no README.
- **baixa**: texto, acento, nome ou número divergente entre documentos, sem efeito.

Prioridade de cada cenário: **[P1]** essencial, **[P2]** importante, **[P3]** se houver tempo.
Trilha: **A** (Floci A, com `apply`), **B** (Floci B, sem `apply` do ambiente), **C** (sem rede,
shims), **D** (Docker e PostgreSQL, sem Floci), **E** (leitura).

### Reconhecimento

#### QA11-00 — O Floci, os contêineres filhos e a linha de base [P1, A, 20 min]

Objetivo: saber como o Floci cria contêineres filhos (nome, rótulo, porta publicada) e se eles somem
no `destroy`; medir o ciclo e a verificação de zerado como referência. **Decide se as trilhas A e B
podem rodar em paralelo.**

Pré-condição: Preparação 0 a 5; Floci A e runner `a` no ar.

```bash
ra 'bash /passos/init.sh'
docker ps -a --format '{{.Names}} {{.Image}} {{.Ports}}' > "$QA/saidas/00-antes.txt"
ra 'time bash /passos/sobe.sh' 2>&1 | tee "$QA/saidas/00-sobe.txt"
# durante o apply, noutro terminal, a cada 20 s:
docker ps --format '{{.Names}} | {{.Image}} | {{.Ports}} | {{.Labels}}' | grep -v -E '^(fixedincome|qa11[abc]-runner)'
ra 'time bash scripts/verificar-zerado.sh; echo saida=$?' 2>&1 | tee "$QA/saidas/00-zerado-com-ambiente.txt"
ra 'time bash /passos/desce.sh' 2>&1 | tee "$QA/saidas/00-desce.txt"
ra 'time bash scripts/verificar-zerado.sh; echo saida=$?'
docker ps -a --format '{{.Names}} {{.Image}} {{.Ports}}' | diff "$QA/saidas/00-antes.txt" -
```

Esperado (VALIDACAO-F11, critérios 2 e 4): trava `APROVADO`, bloqueio sem recriação, `apply` em
cerca de 2 min, verificação com ambiente saindo 1 (cerca de 22 achados), `destroy` com 27 recursos,
verificação final 0, e nenhum contêiner filho sobrando.

Registre: o nome e as portas dos filhos (em especial se algum tenta publicar a **5432**, ocupada pelo
`fixedincome-db`); o tempo de cada etapa; o nome do contêiner do RDS (usado no QA11-11).

- Filho com nome fixo ou porta fixa no host: as trilhas A e B **não** fazem `apply` ao mesmo tempo
  (o plano de paralelismo já prevê isso; confirme).
- Filho que sobra depois do `destroy`: vai para o QA11-35 (higiene local).
- `apply` falhando por porta 5432 ocupada: RISCO (afeta quem roda o ciclo local com o `docker
  compose` no ar; o README não avisa), e o QA para e pergunta ao Matheus antes de seguir.

### A. Scripts como o README ensina, fora do caminho feliz

#### QA11-01 — Entradas, códigos de saída e mensagens de cada script [P1, B e C, 45 min]

Objetivo: cada script responde a entrada errada com código de saída coerente com o cabeçalho, erro
em `stderr`, mensagem em português que diz o que fazer, e nunca sai 0 sem ter feito o trabalho.

Pré-condição: runner `b` com `bash /passos/init.sh` feito (só para ter o override; nada aplicado) e
Floci B vazio; runner `c` para as linhas marcadas "C".

Rode cada linha com `rb '<comando>; echo saida=$?'` (ou `rc`), separando `stdout` de `stderr`
(`2> /tmp/err; cat /tmp/err`). Registre saída, código e o tempo de cada uma.

| # | Script e entrada | Esperado (fonte) |
|---|---|---|
| a | `env -u AWS_REGION -u AWS_DEFAULT_REGION bash scripts/verificar-zerado.sh` | 2, `ERRO região ausente ou inválida...` em `stderr` (cabeçalho) |
| b | `verificar-zerado.sh US-EAST-1`, `"us-east-1 "`, `us_east_1` | 2 nos três (cabeçalho) |
| c | `verificar-zerado.sh sa-east-1` (válida, vazia no Floci) | 0, e a linha final **cita `sa-east-1`** (o operador precisa ver qual região foi olhada) |
| d | `verificar-zerado.sh xx-qualquer-9` (formato válido, região inexistente) | Registre o que o Floci faz. Na AWS real seria erro de endpoint (2). Se sair 0: RISCO, o operador acha que conferiu |
| e | `AWS_REGION=us-east-2 bash scripts/verificar-zerado.sh us-east-1` | argumento prevalece; linha final cita `us-east-1` |
| f | `verificar-zerado.sh us-east-1 sobra` (argumento a mais) | Registre: ignorar em silêncio é aceitável; aprovar outra coisa não |
| g | `PATH=/usr/bin:/bin bash scripts/verificar-zerado.sh` (sem `jq`; confira antes com `command -v jq`) | 2, `ERRO ferramenta ausente: jq` |
| h | `VERIFICAR_ESPERA_SEGUNDOS=abc`, `-1`, `99999` e vazio em `verificar-zerado-apos-destroy.sh` | 2 nas quatro, com a mensagem; nenhuma chamada à AWS antes |
| i | `VERIFICAR_ESPERA_SEGUNDOS=0 bash scripts/verificar-zerado-apos-destroy.sh` com o Floci vazio | 0, sem repetição |
| j | `bash scripts/trava-custo.sh < /dev/null` e o mesmo com `bloquear-recriacao.sh` | 1 nos dois (fail closed), mensagem dizendo que a entrada está vazia |
| k | JSON de **state** em vez de plano: `terraform show -json \| bash scripts/trava-custo.sh` (sem plano, com state vazio) | 1, e a mensagem permite entender que faltou o plano |
| l | Plano em texto: `terraform show floci.tfplan \| bash scripts/trava-custo.sh` (gere um plano antes) | 1, "não é JSON" ou equivalente |
| m | Plano inexistente, **sem** `pipefail`: `terraform show -json nao-existe.tfplan \| bash scripts/trava-custo.sh` | 1 (a trava recebe vazio). Com `set -o pipefail`, também diferente de 0 |
| n | `bash scripts/trava-custo.sh` sem pipe, num terminal de verdade (`winpty docker exec -it qa11b-runner bash`, depois `timeout 15 bash scripts/trava-custo.sh`) | Registre: se fica esperando a entrada sem avisar, RISCO baixa (o operador acha que travou) |
| o | `bash scripts/parar-migracao.sh` com o Floci vazio | 0, `Cluster renda-fixa-api não existe...` |
| p | `parar-migracao.sh "qa11 com espaco"` e `parar-migracao.sh ''` | Registre; não pode sair 0 dizendo que parou algo |
| q | `env -u AWS_REGION -u AWS_DEFAULT_REGION bash scripts/parar-migracao.sh` | 1 com mensagem que cite região. Se o código da AWS sair vazio (`ERRO ecs list-tasks: ` e nada mais), BUG média: o `destruir` fica vermelho sem dizer por quê |
| r | `bash scripts/gerar-floci-override.sh http://localhost:4566/`, `localhost:4566`, `'http://a;b'` (numa **cópia descartável** da pasta, `/work/tmp-ovr`) | 1 nas três, `URL do Floci inválida` |
| s | `gerar-floci-override.sh` rodado de outra pasta (`cd /tmp && bash /work/tmp-ovr/scripts/gerar-floci-override.sh`) | o arquivo vai para a pasta `infra/aws` do script, e a mensagem diz o caminho |
| t (C) | `bash bootstrap/bootstrap.sh` (pasta `infra/aws` da cópia) sem argumento, `--help`, `-h`, `--Email x@y.com`, `--email` sozinho | 1 em todos, `ERRO ...` em português, **antes** de qualquer chamada à AWS (no runner `c` não há rede: qualquer chamada falharia de outro jeito) |
| u (C) | `--email voce@exemplo.com` no runner `c` (sem rede) | 1, e a mensagem deve ser sobre credencial/rede, não um rastro cru da CLI. Registre o texto |

Esperado geral (bom senso, CLAUDE.md): erro em `stderr`; resumo final em uma linha; nenhum valor do
plano nem segredo impresso (confira com `grep -c Password=` em tudo o que foi capturado: 0).

- BUG alta: qualquer linha que saia 0 sem ter verificado.
- BUG média: código de saída diferente do cabeçalho; mensagem que não diz o que fazer.
- BUG baixa: mensagem em inglês ou sem acento quando as vizinhas têm.

#### QA11-02 — Endpoint lento ou parando no meio, e o tempo da verificação [P1, B, 40 min]

Objetivo: a verificação de zerado, o `apos-destroy` e o `parar-migracao` com o "provedor" travado ou
caindo no meio. O agendado tem `timeout-minutes: 60` e roda a cada 30 min; uma verificação que
pendura por muito tempo acumula execuções e atrasa o próximo `destroy`.

Pré-condição: Floci B vazio, runner `b`.

```bash
# a. linha de base: Floci vazio, e Floci com 20 secrets e 10 log groups qa11
rb 'time bash scripts/verificar-zerado.sh; echo saida=$?'
rb 'for i in $(seq 1 20); do aws secretsmanager create-secret --name qa11/carga-$i --secret-string x --tags Key=qa,Value=f11 >/dev/null; done
    for i in $(seq 1 10); do aws logs create-log-group --log-group-name /qa11/renda-fixa-api-$i; done'
rb 'time bash scripts/verificar-zerado.sh > /tmp/z.txt; echo saida=$?; grep -c ^RESIDUO /tmp/z.txt'
# b. Floci congelado do começo ao fim (limite de 20 min para o QA não ficar preso)
docker pause qa11b-floci
rb 'time timeout 1200 bash scripts/verificar-zerado.sh; echo saida=$?'
docker unpause qa11b-floci
# c. Floci congelado no meio: começa a verificação, congela por 90 s, descongela
rb 'time bash scripts/verificar-zerado.sh; echo saida=$?' & sleep 3; docker pause qa11b-floci; sleep 90; docker unpause qa11b-floci; wait
# d. o mesmo para o parar-migracao e o apos-destroy
docker pause qa11b-floci
rb 'time timeout 900 bash scripts/parar-migracao.sh; echo saida=$?'
rb 'time timeout 900 env VERIFICAR_ESPERA_SEGUNDOS=5 bash scripts/verificar-zerado-apos-destroy.sh; echo saida=$?'
docker unpause qa11b-floci
# e. porta fechada (sem ninguém escutando)
rb 'time AWS_ENDPOINT_URL=http://localhost:4599 bash scripts/verificar-zerado.sh; echo saida=$?'
rb 'time AWS_ENDPOINT_URL=http://localhost:4599 bash scripts/parar-migracao.sh; echo saida=$?'
```

Esperado (cabeçalho: "timeout... 2 = não conseguiu verificar"; spec: fail closed): (a) 0 no vazio;
1 com os 30 resíduos listados (20 secrets e 10 log groups, todos contêm `renda-fixa-api` ou são
secrets); (b) e (c) 2, nunca 0, e o resumo final diz quantos erros; (d) `apos-destroy` sai 2 **sem**
repetir; (e) 2 e 1, rápido.

Registre o tempo de cada caso e compare: com o Floci congelado, quanto leva até o 2? Se passar de
30 min, RISCO média (agendados se acumulam; o `destroy` do próximo ciclo atrasa). O `parar-migracao`
não tem `--cli-connect-timeout` nem `--cli-read-timeout` no cabeçalho: se pendurar até o `timeout`
do QA, RISCO média (num `destruir`, ele roda antes do `destroy` e pode consumir o tempo do job).

Limpeza: apague os 20 secrets (`--force-delete-without-recovery`) e os 10 log groups; verificação 0.

#### QA11-03 — Credencial ausente, perfil inexistente e o que o Floci não valida [P2, B, 20 min]

Objetivo: o operador no CloudShell com sessão expirada, ou o runner com a credencial OIDC vencida no
meio do `destroy` (sessão de 2 h), recebe vermelho com mensagem que aponta a credencial.

```bash
rb 'env -u AWS_ACCESS_KEY_ID -u AWS_SECRET_ACCESS_KEY bash scripts/verificar-zerado.sh; echo saida=$?'
rb 'env -u AWS_ACCESS_KEY_ID -u AWS_SECRET_ACCESS_KEY bash scripts/parar-migracao.sh; echo saida=$?'
rb 'AWS_PROFILE=qa11-inexistente bash scripts/verificar-zerado.sh; echo saida=$?'
rb 'AWS_ACCESS_KEY_ID=QA11INVALIDA AWS_SECRET_ACCESS_KEY=x AWS_SESSION_TOKEN=lixo bash scripts/verificar-zerado.sh; echo saida=$?'
rb 'env -u AWS_ACCESS_KEY_ID -u AWS_SECRET_ACCESS_KEY bash bootstrap/bootstrap.sh --email qa11@exemplo.com.br; echo saida=$?'
```

Esperado: 2 / 1 / 2 com mensagem sobre credencial; o bootstrap sai diferente de 0 antes de criar
qualquer coisa e diz o motivo. O quarto caso provavelmente **passa** no Floci (ele não valida
credencial): registre como "não reproduzível localmente" e mande para o P8 (credencial vencida de
verdade, inclusive no meio do `destroy`).

- BUG média: mensagem que é só o rastro cru da CLI em inglês no bootstrap, sem o `ERRO ...` do script.

### B. Trava de custo e bloqueio de recriação contra planos reais

Todos os planos desta seção ficam **dentro do volume** do runner; o JSON vai direto para o script,
como no workflow. Nada de `terraform show -json ... > arquivo` fora do volume, nada de imprimir.

#### QA11-04 — Mudanças de operador num plano real, contra a trava [P1, B, 50 min]

Objetivo: a trava reprova, com mensagem clara, o que um operador mudaria "só para testar", e o QA
descobre o que ela deixa passar que a spec não previu.

Pré-condição: runner `b` com `init` feito na pasta base; Floci B sem nenhum ambiente (os planos são
contra state vazio: tudo `create`).

Para cada mutação: copie a pasta base para `/work/m/<id>` (sem state), altere **na cópia** (ache a
linha com `grep -rn`), rode o plano e a trava:

```bash
mut() { # id "comando de alteração na cópia"
  rb "rm -rf /work/m/$1 && mkdir -p /work/m && cp -r /work/repo/infra/aws /work/m/$1 && rm -f /work/m/$1/floci.tfstate* /work/m/$1/*.tfplan
      cd /work/m/$1 && $2 && terraform plan -input=false -lock-timeout=5m -var-file=environments/floci.tfvars \
      -var expires_at=\$(date -u -d '+2 hours' +%Y-%m-%dT%H:%M:%SZ) -var service_desired_count=0 -out=m.tfplan > /tmp/plan-$1.log 2>&1 \
      || { echo PLANO_FALHOU; tail -n 15 /tmp/plan-$1.log; exit 0; }
      terraform show -json m.tfplan | bash scripts/trava-custo.sh; echo trava=\$?"
}
mut M01 "sed -i 's/db.t4g.micro/db.t4g.small/' modules/database/main.tf"
```

| Id | Mudança (na cópia) | Esperado | Fonte |
|---|---|---|---|
| M01 | `instance_class` `db.t4g.small` | reprova, cita `instance_class` | spec, trava |
| M02 | `instance_class` `db.t3.micro` (também "barata") | reprova; a mensagem diz o valor aceito? | spec |
| M03 | `allocated_storage` 30 | reprova | spec |
| M04 | `storage_type` removido (fica o padrão) | reprova (`gp3` exigido ou valor desconhecido) | S1 |
| M05 | `backup_retention_period` 1 | reprova | spec |
| M06 | `publicly_accessible = true` | reprova | P5 |
| M07 | `performance_insights_enabled = true` | reprova | P5 |
| M08 | `enabled_cloudwatch_logs_exports = ["postgresql"]` | spec diz "sem"; a trava checa? Se aprovar: RISCO (custo de ingestão e log group residual) | spec, "Banco" |
| M09 | CPU 512 na task definition da API; memória 1024 na da migração | reprova as duas, citando cada endereço | spec |
| M10 | `ephemeral_storage { size_in_gib = 100 }` na task definition da API | a spec não prevê; Fargate cobra acima de 20 GiB. Se aprovar: RISCO (decisão 5) | não previsto |
| M11 | Container Insights ligado no cluster (`setting { name = "containerInsights" value = "enabled" }`) | spec diz "desligado"; se a trava aprovar: RISCO ou BUG (decisão 5) | spec, "Aplicação" |
| M12 | `desired_count = 2` literal na chamada do módulo (contorna a `validation` da variável) | reprova | spec |
| M13 | `aws_appautoscaling_target` (máximo 10) e `aws_appautoscaling_policy` para o serviço | reprova (tipos fora da lista) | lista positiva |
| M14 | `aws_cloudwatch_metric_alarm` (barato e inofensivo) | reprova; a mensagem orienta o operador sobre onde mudar a lista? | lista positiva |
| M15 | `aws_eip` e `aws_nat_gateway` numa subnet | reprova, listando **os dois** | spec |
| M16 | segundo bloco `module "app2"` igual ao `app`, com outro `name` | reprova pelas contagens (2 ALB, 2 serviços, 4 task definitions) | S1 |
| M17 | `retention_in_days` 1 no log group (mais barato) | reprova (valor exato 3); registre se a mensagem deixa claro que é "exatamente 3" | spec |
| M18 | 10 `aws_secretsmanager_secret` extras (`count = 10`) | a spec não limita; cada secret cobra. Registre | não previsto |
| M19 | `engine_version = "17"` | registre o resultado da trava (não há limite de versão) | informativo |
| M20 | `storage_throughput = 500` com `gp3` | registre (o throughput acima do incluso cobra; a AWS recusa abaixo de 400 GB) | não previsto |

Esperado geral: exit 1 em toda reprovação; cada linha cita tipo, endereço e atributo, nunca o valor;
`grep -c Password= /tmp/plan-*.log` igual a 0 (o log humano do `plan` também vai para o Actions).

- BUG alta: a trava aprova algo que a spec proíbe (M01 a M09, M12 a M17).
- RISCO (decisão 5): M08, M10, M11, M18 e M20 aprovados.

#### QA11-05 — Mudanças que impedem o `destroy` [P1, B (trava) e A (ciclo), 60 min]

Objetivo: a premissa é "nada fica ligado esquecido", e um `destroy` que falha deixa tudo ligado. A
spec manda `deletion_protection = false`, `enable_deletion_protection = false`,
`skip_final_snapshot = true`, `delete_automated_backups = true` e `recovery_window_in_days = 0`, mas
a lista de limites da trava não cita esses atributos. Uma pessoa que "protege o banco" num ambiente
efêmero quebra o TTL.

| Id | Mudança (na cópia) | O que observar |
|---|---|---|
| V1 | `deletion_protection = true` no RDS | trava; `destroy` no Floci; verificação |
| V2 | `enable_deletion_protection = true` no ALB | idem |
| V3 | `skip_final_snapshot = false` com `final_snapshot_identifier = "qa11-final"` | `destroy` deixa snapshot? verificação o lista (`rds-snapshot-manual`)? |
| V4 | `delete_automated_backups = false` | idem para backup retido |
| V5 | `lifecycle { prevent_destroy = true }` no RDS | o `destroy` aborta **antes** de destruir qualquer coisa? |
| V6 | `recovery_window_in_days = 30` no secret | depois do `destroy`, a verificação acusa `secret ... (agendado para exclusao)`; e um novo `sobe.sh` com o mesmo nome de secret falha? |

Passos: (1) na trilha B, `mut` com a mudança e anote a trava; (2) na trilha A, numa cópia
`/work/v/<id>` com `init`, rode `bash /passos/sobe.sh`, depois `bash /passos/desce.sh; echo
desce=$?` e `bash scripts/verificar-zerado.sh; echo saida=$?`; (3) se sobrou algo, desfaça a mudança
na cópia (ou use a CLI) até o Floci voltar a zero, e registre o caminho que o operador teria de seguir
e se o README cobre.

Esperado (spec, "Premissa de custo" e "Banco"): ou a trava reprova a mudança antes do `apply`, ou o
`destroy` termina e a verificação aprova. Se o Floci não aplicar a proteção (o `destroy` passa mesmo
com V1/V2), registre "Floci não reproduz" e mande para o P8.

- Trava aprova **e** o `destroy` deixa recurso pago (no Floci): BUG ou RISCO alta (decisão 5).
- V5 abortando o `destroy` inteiro: o agendado ficaria vermelho a cada 30 min com o ambiente todo no
  ar. Registre a mensagem que o operador veria.

#### QA11-06 — Planos de `destroy`, prorrogação, troca de SHA e state vazio [P2, A, 30 min]

Objetivo: a trava e o bloqueio nos planos que o operador ou o workflow geram fora do primeiro `apply`.

Pré-condição: ambiente no ar na trilha A (`bash /passos/sobe.sh`).

```bash
ra 'terraform plan -destroy -input=false -var-file=environments/floci.tfvars -var expires_at=1970-01-01T00:00:00Z -out=d.tfplan > /tmp/d.log
    terraform show -json d.tfplan | bash scripts/trava-custo.sh; echo trava=$?; terraform show -json d.tfplan | bash scripts/bloquear-recriacao.sh; echo bloqueio=$?'
for caso in "expires_at=$(date -u -d '+5 hours' +%Y-%m-%dT%H:%M:%SZ) -var service_desired_count=0" \
            "expires_at=$(date -u -d '+5 hours' +%Y-%m-%dT%H:%M:%SZ) -var service_desired_count=1" \
            "expires_at=$(date -u -d '+5 hours' +%Y-%m-%dT%H:%M:%SZ) -var service_desired_count=0 -var image_sha=$(printf 'a%.0s' {1..40})"; do
  ra "terraform plan -input=false -var-file=environments/floci.tfvars -var $caso -out=p.tfplan > /tmp/p.log
      terraform show -json p.tfplan | jq -r '.resource_changes[] | select(.change.actions != [\"no-op\"]) | \"\(.change.actions|join(\"+\")) \(.address)\"'
      terraform show -json p.tfplan | bash scripts/trava-custo.sh; echo trava=\$?; terraform show -json p.tfplan | bash scripts/bloquear-recriacao.sh; echo bloqueio=\$?"
done
ra 'terraform plan -refresh-only -input=false -var-file=environments/floci.tfvars -var expires_at=1970-01-01T00:00:00Z -out=r.tfplan > /dev/null; terraform show -json r.tfplan | bash scripts/trava-custo.sh; echo trava=$?'
```

Esperado: plano de `destroy` aprovado nos dois (o `delete` isolado fica de fora, P5); prorrogação
com `desired_count` 0 ou 1 aprovada, mudando só a tag do cluster, o output e o serviço (mais a
diferença conhecida de `referenced_security_group_id` do Floci); troca de SHA (o que o README diz
que acontece ao prorrogar sem `sha`) aprovada, com as duas task definitions substituídas e **sem**
recriar RDS nem versão do secret. O `-refresh-only` reprova (sem itens): informativo.

Depois, com o ambiente destruído e o state vazio, repita o `plan -destroy`: a trava reprova por
`resource_changes` vazio (fail closed). Registre se a mensagem deixa claro que "não há nada a
destruir" não é custo.

- BUG alta: a troca de SHA aparece como substituição do RDS ou da versão do secret.

#### QA11-07 — Recriação forçada por mudança de operador [P1, A, 30 min]

Objetivo: o `bloquear-recriacao.sh` reprova planos reais em que o RDS ou a versão do secret seriam
substituídos, e a mensagem manda o operador para o caminho certo (README: "rodar `destruir` e depois
`subir`").

Pré-condição: ambiente no ar na trilha A, numa cópia `/work/r` com `init` (para não sujar a base).

Mudanças, uma por vez, cada uma desfeita antes da seguinte: `username` do banco; `db_name`; o `name`
do módulo `database`; `vpc_cidr` do módulo `network` (`10.20.0.0/16` para `10.30.0.0/16`); o nome do
secret; `storage_encrypted = false`. Para cada uma:

```bash
ra '<alteração> && terraform plan -input=false -var-file=environments/floci.tfvars -var expires_at=$(date -u -d "+3 hours" +%Y-%m-%dT%H:%M:%SZ) -out=x.tfplan > /tmp/x.log
    terraform show -json x.tfplan | jq -r ".resource_changes[] | select(.change.actions | index(\"delete\")) | .address"
    terraform show -json x.tfplan | bash scripts/bloquear-recriacao.sh; echo bloqueio=$?' /work/r
```

Esperado (S5, README "Recriação do banco ou do secret"): sempre que a lista mostrar o RDS ou a versão
do secret, saída 1 com o endereço, sem valores, e a orientação de `destruir` e `subir`. Quando nenhum
dos dois for substituído (talvez `vpc_cidr`), saída 0, e registre o que é recriado.

- BUG alta: substituição do RDS ou da versão do secret aprovada.
- BUG baixa: mensagem que não diz o que fazer, ou que diverge do README.

### C. Verificação de zerado contra resíduos

Os resíduos são criados **à mão** no Floci B, com prefixo `qa11`. Antes de cada bloco, a verificação
precisa estar em 0. Se o Floci não suportar a criação de um tipo, registre o erro e marque
`NÃO EXECUTADO` só para aquele item.

#### QA11-08 — Resíduos que a spec cita [P1, B, 45 min]

Objetivo: cada tipo da lista da spec ("Verificação de zerado") é achado quando sobra **fora** do
state e sem a tag do projeto, com a linha `RESIDUO <tipo> <id>` e saída 1.

| Resíduo | Como criar (Floci B) | Tipo esperado na saída |
|---|---|---|
| cluster ECS vazio | `aws ecs create-cluster --cluster-name qa11-vazio` | cluster (spec P5: cluster vazio é resíduo) |
| serviço ECS | task definition `qa11-td` (imagem `postgres:16@sha256:f9a7...`, `sleep 600`) e `create-service --desired-count 0` | `ecs-servico` |
| tarefa em execução | `run-task` da `qa11-td` no `qa11-vazio` | `ecs-tarefa`; depois de `stop-task`, some |
| ALB e target group | VPC `10.99.0.0/16` com duas subnets `qa11`, `elbv2 create-load-balancer` e `create-target-group` | `load-balancer`, `target-group` |
| instância RDS | `rds create-db-instance --db-instance-identifier qa11-db --db-instance-class db.t4g.micro --engine postgres --master-username qa --master-user-password QA11senha0123456789` | `rds-instancia` |
| snapshot manual | `rds create-db-snapshot` da `qa11-db` | `rds-snapshot-manual` |
| backup automático retido | `qa11-db` com retenção 1, apagada com `--delete-automated-backups false` | `rds-backup-automatico` |
| Elastic IP | `ec2 allocate-address` | `elastic-ip` |
| NAT gateway | numa subnet `qa11`, com o EIP | `nat-gateway` |
| ENI com IPv4 público | `create-network-interface` e `associate-address` | `eni-ipv4-publico` |
| instância EC2 | `ec2 run-instances` (qualquer AMI que o Floci aceite) | `ec2-instancia` |
| VPC endpoint | `ec2 create-vpc-endpoint` | `vpc-endpoint` |
| secret com outro nome | `secretsmanager create-secret --name qa11/qualquer` | `secret` |
| secret agendado | `delete-secret --recovery-window-in-days 7` de outro | `secret ... (agendado para exclusao)` |
| log group do projeto | `/ecs/renda-fixa-api-qa11` | `log-group` |
| chave KMS de cliente | `kms create-key`; depois `schedule-key-deletion` | `kms-chave-cliente`; depois só `AVISO` |
| só a tag | uma VPC `qa11` com `project=renda-fixa-api` | `recurso-com-tag` |

Para cada linha: crie, rode `rb 'time bash scripts/verificar-zerado.sh; echo saida=$?'`, apague,
rode de novo (0). Registre o tempo; com o RDS do Floci, a criação pode levar minutos.

Esperado (spec "Verificação de zerado"; README "Resíduos"): saída 1 e o tipo certo em todos.

- BUG alta: qualquer item que a spec lista e a verificação aprova.

#### QA11-09 — Resíduos que a spec não cita, ou quase iguais [P1, B, 45 min]

Objetivo: o que escapa da verificação. Para cada caso, registre se foi achado e, se não foi, o custo
real do recurso (a classificação é da decisão 5).

| Caso | Como criar | O que se espera descobrir |
|---|---|---|
| log group parecido | `/ecs/renda-fixa`, `/ecs/Renda-Fixa-Api`, `/aws/lambda/qa11` | só nomes que **contêm** `renda-fixa-api` são achados (o script usa `contains`; a spec diz "prefixo"). Log group sem dados quase não custa |
| tag parecida | VPC com `Project=renda-fixa-api`, outra com `project=Renda-Fixa-Api`, outra com `project="renda-fixa-api "` | tags diferenciam maiúsculas: não achadas |
| bucket S3 com a tag do projeto | `s3api create-bucket qa11-bucket` + `put-bucket-tagging project=renda-fixa-api` | `AVISO` e ignorado (contrato com o bootstrap). Bucket cobra armazenamento |
| ELB clássico | `aws elb create-load-balancer --load-balancer-name qa11-classico ...` sem tag | `elbv2 describe-load-balancers` não lista o clássico. Cobra por hora |
| volume EBS | `ec2 create-volume --size 1 --availability-zone us-east-1a` sem tag | não está na lista. Cobra |
| cluster Aurora sem instância | `rds create-db-cluster --engine aurora-postgresql ...` | `describe-db-instances` não lista cluster. Cobra armazenamento |
| hosted zone, alarme, repositório ECR | `route53 create-hosted-zone`, `cloudwatch put-metric-alarm`, `ecr create-repository` | fora da lista; custo pequeno |
| **outra região** | secret, EIP e cluster em `us-east-2` (`--region us-east-2`) | `verificar-zerado.sh us-east-1` aprova; `... us-east-2` reprova. A verificação é de uma região só (spec: "na região") |
| muitos secrets | 60 secrets `qa11/pag-NN` | 60 linhas `RESIDUO secret` (paginação); registre o tempo |

Esperado: o comportamento de cada um, registrado. Confira também se o README ("Resíduos e como cada
um é tratado" e "Lock preso", passo 4) avisa que a verificação olha **uma região** e uma lista
fechada de tipos.

- RISCO média: a limitação de região, se o README não a explicita (o roteiro de "Lock preso" passa
  `us-east-1`, mas o CloudShell abre na região do console).

#### QA11-10 — Estados do ECS: cluster INACTIVE, tarefa STOPPED, task definition INACTIVE [P3, B, 20 min]

Objetivo: conferir contra o Floci o que a suíte com shim (19 casos) já cobre: avisos não reprovam,
o que está vivo reprova.

```bash
rb 'aws ecs create-cluster --cluster-name qa11-c --tags key=project,value=renda-fixa-api >/dev/null
    arn=$(aws ecs register-task-definition --family qa11-td2 --requires-compatibilities FARGATE --network-mode awsvpc --cpu 256 --memory 512 \
      --container-definitions "[{\"name\":\"x\",\"image\":\"postgres:16\",\"essential\":true}]" --tags key=project,value=renda-fixa-api \
      --query taskDefinition.taskDefinitionArn --output text)
    aws ecs deregister-task-definition --task-definition "$arn" >/dev/null
    bash scripts/verificar-zerado.sh; echo saida=$?
    aws ecs delete-cluster --cluster qa11-c >/dev/null; bash scripts/verificar-zerado.sh; echo saida=$?'
```

Esperado (I1, S4; README, último parágrafo de "Lock preso"): com o cluster ativo, saída 1 (cluster);
a task definition INACTIVE só como `AVISO`; depois de apagar o cluster, saída 0 com avisos. Se o
Floci não guardar a tag da task definition, registre.

### D. Ciclos no Floci

#### QA11-11 — Subir duas vezes seguidas (prorrogar) [P1, A, 30 min]

Objetivo: provar a promessa da D6 e do README ("prorrogar não reescreve senha nem secret"; "só um
update no lugar da tag `expires_at` do cluster e do output; nada é recriado") num ciclo real.

```bash
E1=$(date -u -d '+2 hours' +%Y-%m-%dT%H:%M:%SZ); E2=$(date -u -d '+5 hours' +%Y-%m-%dT%H:%M:%SZ)
ra "bash /passos/sobe.sh $E1 0 && terraform output -raw expires_at; echo; bash /passos/segredo.sh"
ra 'aws ecs describe-clusters --clusters renda-fixa-api --include TAGS --query "clusters[0].tags" --output json'
# a senha do secret ainda abre o banco do Floci? (valor só numa variável do shell, nunca impresso)
PGPW="$(docker exec qa11a-runner bash -c 'aws secretsmanager get-secret-value --secret-id renda-fixa-api/connection-string --query SecretString --output text | sed -n "s/.*Password=\([^;]*\).*/\1/p"')"
FILHO="<contêiner do RDS anotado no QA11-00>"
docker exec -e PGPASSWORD="$PGPW" "$FILHO" psql -h 127.0.0.1 -U fixedincome -d fixedincome -tAc 'select 1'; unset PGPW
ra "terraform plan -input=false -var-file=environments/floci.tfvars -var expires_at=$E2 -var service_desired_count=0 -out=p2.tfplan > /tmp/p2.log
    terraform show -json p2.tfplan | jq -r '.resource_changes[] | select(.change.actions != [\"no-op\"]) | \"\(.change.actions|join(\"+\")) \(.address)\"'"
ra "bash /passos/sobe.sh $E2 0 && terraform output -raw expires_at; echo; bash /passos/segredo.sh"
# repita o psql com a senha lida de novo do secret; repita a contagem da validação:
ra 'v=$(aws secretsmanager get-secret-value --secret-id renda-fixa-api/connection-string --query SecretString --output text)
    s=$(printf "%s" "$v" | sed -n "s/.*Password=\([^;]*\).*/\1/p")
    for f in floci.tfstate; do printf "%s: senha=%s conn=%s Password==%s\n" $f "$(grep -c -F "$s" $f)" "$(grep -c -F "$v" $f)" "$(grep -c "Password=" $f)"; done
    terraform show -json | grep -c -F "$s"; terraform show -json p2.tfplan | grep -c -F "$s"'
ra 'set +e; terraform plan -detailed-exitcode -input=false -var-file=environments/floci.tfvars -var expires_at='"$E2"' -var service_desired_count=0 > /tmp/pd.log; echo plan=$?; grep -E "will be|must be" /tmp/pd.log'
```

Esperado: `expires_at` muda de `E1` para `E2` no output e na tag do cluster; o hash, o tamanho e as
versões do secret **não** mudam; o `psql` abre o banco antes e depois; contagens de senha e
connection string em 0; no plano da prorrogação, só o update do cluster (mais a diferença conhecida
do Floci em `referenced_security_group_id`, que é informativa). No `plan -detailed-exitcode` final,
qualquer diferença **além** da conhecida vai para o relatório.

Se o `psql` falhar já na primeira vez, o Floci não aplica a senha de escrita única: registre e marque
essa parte `NÃO EXECUTADO` (vai para o P8).

- BUG alta: secret reescrito ou senha que deixa de abrir o banco depois da prorrogação.

#### QA11-12 — `destroy` com marcações, state ausente e state parcial [P1, A, 45 min]

Objetivo: o `destruir`, o `limpar` e o agendado destroem pelo state, com marcações quando os outputs
faltam. A verificação precisa salvar o operador quando o state mente.

```bash
# a. marcações (o que o destruir usa sem outputs)
ra 'bash /passos/sobe.sh && bash /passos/desce.sh 1970-01-01T00:00:00Z 0000000000000000000000000000000000000000; bash scripts/verificar-zerado.sh; echo saida=$?'
# b. state sumido: o ambiente existe, o state não
ra 'bash /passos/sobe.sh && mv floci.tfstate /work/state-escondido.tfstate
    terraform destroy -auto-approve -input=false -var-file=environments/floci.tfvars -var expires_at=1970-01-01T00:00:00Z > /tmp/d.log; echo destroy=$?; tail -n 2 /tmp/d.log
    time VERIFICAR_ESPERA_SEGUNDOS=10 bash scripts/verificar-zerado-apos-destroy.sh; echo saida=$?
    mv /work/state-escondido.tfstate floci.tfstate && bash /passos/desce.sh && bash scripts/verificar-zerado.sh; echo saida=$?'
# c. state parcial: o RDS fora do state
ra 'bash /passos/sobe.sh && terraform state list | grep aws_db_instance
    terraform state rm "$(terraform state list | grep aws_db_instance)"
    bash /passos/desce.sh; echo desce=$?; bash scripts/verificar-zerado.sh; echo saida=$?'
# d. state parcial: o cluster fora do state (repita c com aws_ecs_cluster)
# e. o que a validation do Terraform aceita como expires_at no destroy
for v in 2026-11-19T10:00:00Z 2026-11-19T10:00:00.5Z 2026-11-19T10:00:00+03:00 2026-11-19t10:00:00z "amanhã"; do
  ra "terraform plan -destroy -input=false -var-file=environments/floci.tfvars -var expires_at='$v' > /tmp/v.log 2>&1; echo \"$v -> \$?\"; grep -m1 -i error /tmp/v.log"
done
```

Esperado: (a) todos os recursos destruídos e 0; (b) o `destroy` "passa" sem nada a destruir, mas a
verificação sai 1 nas duas tentativas e lista o ambiente inteiro (é exatamente o caso que a camada 4
existe para pegar); (c) e (d) o `destroy` falha ou deixa o órfão, e a verificação lista o órfão; nos
dois, registre a mensagem que o operador veria e se o README ("Lock preso", passos 4 e 5) leva à
limpeza; (e) os formatos que o passo "Ler expires_at" do `destruir` deixa passar (regex com `.5`,
offset) também precisam passar na `validation` do Terraform.

- BUG alta: (b) com verificação 0, ou (e) com um formato aceito pelo passo do workflow e recusado pela
  `validation` (o `destruir` e o agendado ficariam vermelhos para sempre sem destruir).

Limpeza: apague o órfão pela CLI e rode `desce.sh` até a verificação dar 0.

#### QA11-13 — `apply` interrompido como um cancelamento do GitHub [P1, A, 45 min]

Objetivo: aproximar localmente o I2 (pendente do P8): o que um cancelamento durante o `apply` deixa,
e se o roteiro de recuperação do README basta. O GitHub cancela com SIGINT, depois SIGTERM (7,5 s)
e SIGKILL (mais 2,5 s); o QA reproduz a mesma sequência.

```bash
ra 'terraform plan -input=false -var-file=environments/floci.tfvars -var expires_at=$(date -u -d "+2 hours" +%Y-%m-%dT%H:%M:%SZ) -out=floci.tfplan > /dev/null
    terraform apply -input=false -lock-timeout=5m floci.tfplan > /tmp/apply-int.log 2>&1 & pid=$!
    until aws rds describe-db-instances --query "DBInstances[0].DBInstanceStatus" --output text 2>/dev/null | grep -qv None; do sleep 2; done
    kill -INT $pid; sleep 7.5; kill -TERM $pid 2>/dev/null; sleep 2.5; kill -KILL $pid 2>/dev/null; wait $pid; echo apply=$?
    tail -n 20 /tmp/apply-int.log; ls -la .terraform.tfstate.lock.info floci.tfstate 2>&1; terraform state list | wc -l
    bash scripts/verificar-zerado.sh; echo saida=$?'
```

Depois, siga o README "Lock preso" e "Depois de cancelar um `subir` durante o `apply`", adaptado ao
Floci (sem CloudShell; `force-unlock` só se houver lock): `destroy` com marcações, verificação,
remoção dos órfãos pela tag ou pelo console (aqui, CLI), na ordem do passo 5, e verificação até 0.
Repita uma segunda vez matando durante a criação do ALB ou do cluster, em vez do RDS.

Esperado (README "Riscos aceitos" e "Lock preso"): pode sobrar órfão e lock; a verificação **lista**
cada órfão; o roteiro do README, seguido na ordem, leva a 0. Registre o tempo total de recuperação,
cada passo do README que não funcionou como escrito e se o `floci.tfstate` ficou íntegro.

- BUG alta: órfão pago que a verificação não lista.
- BUG média: passo do roteiro de recuperação que não funciona como escrito.

#### QA11-14 — State no S3 do Floci, com `use_lockfile` [P2, A, 45 min]

Objetivo: o caminho real usa backend S3 com `use_lockfile`; o ciclo do Floci troca por `local`. Aqui
o QA reproduz o backend do README contra o S3 do Floci, para ver lock preso, `force-unlock` e o passo
"Ler o state" do agendado como serão na AWS.

```bash
ra 'rm -rf /work/s3 && cp -r /work/repo/infra/aws /work/s3 && cd /work/s3 && rm -f floci.tfstate* *.tfplan
    bash scripts/gerar-floci-override.sh > /dev/null && sed -i "/^terraform {/,/^}/d" floci_override.tf && grep -c backend floci_override.tf
    aws s3api create-bucket --bucket qa11-tfstate > /dev/null && aws s3api put-bucket-versioning --bucket qa11-tfstate --versioning-configuration Status=Enabled
    printf "%s\n" "endpoints = { s3 = \"http://localhost:4566\" }" "use_path_style = true" "skip_credentials_validation = true" \
      "skip_requesting_account_id = true" "skip_metadata_api_check = true" "skip_region_validation = true" > qa-floci-s3.hcl
    terraform init -input=false -reconfigure -backend-config=environments/aws.backend.hcl -backend-config="bucket=qa11-tfstate" -backend-config=qa-floci-s3.hcl' /work
```

O comando de `init` é o do README ("Lock preso") mais o arquivo do QA com o endpoint do Floci. Depois:

1. `terraform state list` sem nenhum objeto no bucket: registre código e mensagem. O passo "Ler o
   state" do agendado só trata `No state file was found` (texto do backend local): se o S3 der erro
   com outro texto, o agendado ficaria vermelho sem ambiente; se der 0 com lista vazia, ótimo.
2. `apply` e, no meio, `kill -KILL` (como no QA11-13): o `.tflock` fica no bucket
   (`aws s3api list-objects-v2 --bucket qa11-tfstate`)?
3. Com o lock preso: `wf a "" wf-ler-state.sh /work/s3` (lê sem lock?), depois um `destroy` com
   `-lock-timeout=30s` (o real é 5m; reduzido para o QA): a mensagem traz o `ID:` que o README manda
   usar?
4. `terraform force-unlock -force <ID>` (o README usa sem `-force`, interativo; registre se funciona
   pelo `docker exec -it` com confirmação), `destroy`, verificação.
5. Bucket inexistente (`-backend-config="bucket=qa11-nao-existe"`): o `init` falha com mensagem clara
   (o agendado fica vermelho, como a spec quer).

Se o S3 do Floci não suportar escrita condicional (o `use_lockfile` depende dela), registre a
mensagem e marque `NÃO EXECUTADO` a partir do passo 2.

#### QA11-15 — Região trocada com o ambiente no ar [P2, A, 40 min]

Objetivo: `region` é variável e só tem `validation` de formato (P1); o workflow passa `-var
region=$AWS_REGION`, e `AWS_REGION` é uma variable do environment que o operador edita. Se ela mudar
com o ambiente no ar, o `refresh` não acha os recursos na região nova, tira-os do state e planeja
criar tudo de novo; o ambiente antigo vira órfão numa região que ninguém verifica.

Pré-condição: confirmar que o Floci isola regiões:
`rb 'aws secretsmanager create-secret --name qa11/regiao --secret-string x --region us-east-2 > /dev/null; aws secretsmanager list-secrets --region us-east-1 --query "length(SecretList)"'`
(0 = isola; apague o secret depois). Se não isolar, `NÃO EXECUTADO` e o caso vai para o P8.

```bash
ra 'bash /passos/sobe.sh && terraform state list | wc -l'
ra 'terraform plan -input=false -var-file=environments/floci.tfvars -var region=us-east-2 -var expires_at=$(date -u -d "+2 hours" +%Y-%m-%dT%H:%M:%SZ) -out=rg.tfplan > /tmp/rg.log
    grep -E "has been deleted|will be created|Plan:" /tmp/rg.log | tail -n 5
    terraform show -json rg.tfplan | bash scripts/trava-custo.sh; echo trava=$?; terraform show -json rg.tfplan | bash scripts/bloquear-recriacao.sh; echo bloqueio=$?'
ra 'terraform apply -input=false rg.tfplan > /dev/null; echo apply=$?
    for r in us-east-1 us-east-2; do bash scripts/verificar-zerado.sh $r > /tmp/z-$r.txt; echo "$r saida=$?"; grep -c ^RESIDUO /tmp/z-$r.txt; done'
# o operador corrige a variable e roda o destruir com a região de volta
ra 'terraform destroy -auto-approve -input=false -var-file=environments/floci.tfvars -var region=us-east-1 -var expires_at=1970-01-01T00:00:00Z > /tmp/d.log; echo destroy=$?; tail -n 1 /tmp/d.log
    for r in us-east-1 us-east-2; do bash scripts/verificar-zerado.sh $r > /dev/null; echo "$r saida=$?"; done'
```

Esperado (premissa de custo): algo precisa parar isso antes do `apply` (trava, bloqueio ou
`validation`). Registre o que de fato acontece em cada passo. Na AWS real a policy nega escrita fora
de `us-east-1`, mas o `refresh` (leitura) pode já ter tirado os recursos do state antes do
`AccessDenied`: isso vai para o P8.

- BUG ou RISCO alta (decisão 5): o plano com região trocada aprovado pelas travas, deixando um
  ambiente inteiro fora do state.

Limpeza: destrua nas duas regiões (o state certo para cada uma, ou a CLI) até as duas verificações
darem 0.

#### QA11-16 — Tarefas ECS no Floci: migração solta, parada e `destroy` imediato [P3, A, 30 min]

Objetivo: `parar-migracao.sh` com uma tarefa viva da família `renda-fixa-migrator`, o `destroy` logo
depois, e o passo "esperar o fim" do `subir` com `exitCode` nulo. Informativo: a spec diz que o Floci
não prova tarefa rodando.

```bash
ra 'bash /passos/sobe.sh
    rev=$(aws ecs register-task-definition --family renda-fixa-migrator --requires-compatibilities FARGATE --network-mode awsvpc --cpu 256 --memory 512 \
      --container-definitions "[{\"name\":\"migrator\",\"image\":\"postgres:16@sha256:f9a7b9ebf6f56201f949db745e47bc423c0f0f8294bafe991cc10bf74b029657\",\"entryPoint\":[\"sleep\",\"600\"],\"essential\":true}]" \
      --query taskDefinition.taskDefinitionArn --output text)
    sub=$(terraform output -json public_subnet_ids | jq -r "join(\",\")"); sg=$(terraform output -raw task_security_group_id)
    aws ecs run-task --cluster renda-fixa-api --task-definition "$rev" --launch-type FARGATE --count 1 --propagate-tags TASK_DEFINITION \
      --network-configuration "awsvpcConfiguration={subnets=[$sub],securityGroups=[$sg],assignPublicIp=ENABLED}" --query "tasks[0].taskArn" --output text > /tmp/tarefa
    sleep 15; aws ecs describe-tasks --cluster renda-fixa-api --tasks $(cat /tmp/tarefa) --query "tasks[0].lastStatus" --output text
    bash scripts/parar-migracao.sh; echo parar=$?
    bash /passos/desce.sh; echo desce=$?; bash scripts/verificar-zerado.sh; echo saida=$?
    aws ecs deregister-task-definition --task-definition "$rev" > /dev/null'
```

Variações: (b) sem o `parar-migracao` antes do `destroy` (o `destroy` do cluster falha? com que
mensagem? a verificação lista a tarefa?); (c) o passo extraído "Migração - esperar o fim e exigir
exitCode 0" contra uma tarefa parada à força (`wf a "CLUSTER=renda-fixa-api TAREFA=<arn>"
wf-esperar-migracao.sh`): mensagem com `exitCode`, `stopCode` e `stoppedReason`, e o `aws logs tail`
falhando sem derrubar o diagnóstico.

Esperado: a tarefa é parada, o `destroy` termina, a verificação dá 0; em (b), vermelho com tarefa e
cluster listados; em (c), `codigo=1` e diagnóstico legível. Se o Floci não rodar a tarefa,
`NÃO EXECUTADO`.

#### QA11-17 — `floci_override.tf` esquecido na pasta [P2, A, 20 min]

Objetivo: o override troca o backend e aponta o provider para o Floci, e o `.gitignore` o esconde do
`git status`. Um operador que rodou o ciclo local e depois usa a mesma pasta para os comandos do
README ("Lock preso") pode operar o Floci achando que opera a AWS.

```bash
ra 'rm -rf /work/esq && cp -r /work/repo/infra/aws /work/esq && cd /work/esq && bash scripts/gerar-floci-override.sh > /dev/null
    terraform init -input=false -backend-config=environments/aws.backend.hcl -backend-config="bucket=qa11-bucket-real" > /tmp/i.log 2>&1; echo init=$?; tail -n 8 /tmp/i.log
    terraform state list > /dev/null 2>&1; echo state=$?' /work
cd "$QA/arvore" && git init -q 2>/dev/null; touch infra/aws/floci_override.tf && git status --short --ignored infra/aws | head; rm infra/aws/floci_override.tf
```

Esperado (bom senso): o `init` com a configuração do backend real **falha** (ou avisa) quando há um
override de backend; o operador tem algum sinal de que o arquivo existe. Registre o que acontece: se
o `init` aceita e o `state list`/`destroy` seguintes operam o Floci (ou um state local) sem aviso,
RISCO média (o README não fala do arquivo fora da seção do PR). O `git status` sem `--ignored` não
mostra o arquivo.

### E. Passos de shell do workflow, com entradas adversas

Todos rodam com `wf`, como o Actions roda. A trilha C não tem rede: um passo que tente sair dela
falha, o que também é informação.

#### QA11-18 — "Validar entradas" (`horas` e `sha`) [P1, C, 20 min]

| `HORAS` | `SHA_ENTRADA` | Esperado |
|---|---|---|
| `4` | vazio | 0, `horas=4` no `GITHUB_OUTPUT` |
| `1`, `12`, `08`, `09` | vazio | 0, `horas=1`, `12`, `8`, `9` |
| `0`, `13`, `099`, `-1`, `+4`, `4.0`, `1e1`, ` 4`, `4 `, vazio | vazio | 1, `::error::horas deve ser um inteiro de 1 a 12` |
| `$'4\n'`, `$'4\nhoras=12'` | vazio | 1 (nada de injeção no `GITHUB_OUTPUT`) |
| `٤` (dígito árabe), `４` (largura total), com `LANG=C.UTF-8` e com `LANG=en_US.UTF-8` | vazio | 1 nos dois locales |
| `4` | 40 hex minúsculos | 0 |
| `4` | 40 maiúsculos, 39, 41, com espaço, `$(id)`, `$'<40 hex>\nsha=x'` | 1, `::error::sha deve ter exatamente 40...` |

```bash
wf c "HORAS='08' SHA_ENTRADA=''" wf-validar-entradas.sh /work
wf c "HORAS=\$'4\nhoras=12' SHA_ENTRADA=''" wf-validar-entradas.sh /work
```

Esperado (spec "Pipeline": `horas` de 1 a 12 e `sha` com 40 hexadecimais, validados antes de
qualquer uso; README "Como subir e destruir").

- BUG alta: qualquer valor fora da regra aceito, ou uma linha extra no `GITHUB_OUTPUT`.

#### QA11-19 — Guarda de 45 dias sem commit [P2, Git Bash, 15 min]

O passo usa `git log`, que não existe no runner: roda no Git Bash, num repositório descartável.

```bash
g45() { d="$QA/repo45"; rm -rf "$d"; git init -q "$d"
  (cd "$d" && quando="$(date -R -d "$1")" && GIT_COMMITTER_DATE="$quando" GIT_AUTHOR_DATE="$quando" \
     git -c user.name=qa -c user.email=qa@local commit -q --allow-empty -m qa \
   && bash --noprofile --norc -eo pipefail "$QA/passos/wf-guarda-45.sh"; echo "codigo=$?") }
g45 "-44 days -23 hours"; g45 "-45 days"; g45 "-45 days +5 minutes"; g45 "-200 days"; g45 "+3 days"; g45 "now"
```

Esperado (spec S11; README "Riscos aceitos"): passa até 44 dias, bloqueia a partir de 45 com a
mensagem que pede um commit; data no futuro passa (registre a mensagem com dias negativos). Compare
com o que o README diz que o operador deve fazer ("botão Enable workflow" e um commit).

- BUG média: mensagem com número de dias errado ou sem dizer o que fazer.

#### QA11-20 — "Resolver o SHA da imagem" e "Conferir as imagens no GHCR" [P1, C, 30 min]

Fixtures em `$QA/passos/fixtures/` (o formato real do `imagetools` é verificado no P8):

```bash
S1=$(printf '1%.0s' {1..40}); S2=$(printf '2%.0s' {1..40}); F="$QA/passos/fixtures"
echo "{\"config\":{\"Labels\":{\"org.opencontainers.image.revision\":\"$S1\"}}}" > "$F/simples.json"
echo "{\"linux/amd64\":{\"config\":{\"Labels\":{\"org.opencontainers.image.revision\":\"$S1\"}}},\"unknown/unknown\":{\"config\":{}}}" > "$F/com-atestado.json"
echo "{\"linux/amd64\":{\"config\":{\"Labels\":{\"org.opencontainers.image.revision\":\"$S1\"}}},\"linux/arm64\":{\"config\":{\"Labels\":{\"org.opencontainers.image.revision\":\"$S2\"}}}}" > "$F/divergente.json"
echo '{"config":{"Labels":{}}}' > "$F/sem-rotulo.json"
echo "{\"config\":{\"Labels\":{\"org.opencontainers.image.revision\":\"${S1^^}A\"}}}" > "$F/invalido.json"
: > "$F/vazio.json"; echo 'nao e json' > "$F/nao-json.json"
for f in simples com-atestado divergente sem-rotulo invalido vazio nao-json; do
  wf c "PATH=/passos/shims/docker:/usr/local/bin:/usr/bin:/bin FALSO_IMAGETOOLS=$f.json SHA_ENTRADA=''" wf-resolver-sha.sh /work
done
wf c "PATH=/passos/shims/docker:/usr/local/bin:/usr/bin:/bin FALSO_DOCKER_FALHA=1 SHA_ENTRADA=''" wf-resolver-sha.sh /work
wf c "PATH=/passos/shims/docker:/usr/local/bin:/usr/bin:/bin SHA_ENTRADA=$S2" wf-resolver-sha.sh /work   # não chama o docker
```

Esperado (spec "Imagens e SHA"): `simples` e `com-atestado` resolvem `S1`; `divergente`, `sem-rotulo`,
`invalido`, `vazio` e `nao-json` saem 1 com `::error::Não foi possível resolver um SHA válido`;
`FALSO_DOCKER_FALHA` sai 1 (registre se a mensagem é a do script ou só a do docker); com
`SHA_ENTRADA`, o docker não é chamado.

GHCR (`SHA=$S1`, `PATH=/passos/shims/ghcr:...`):

| Caso | Variáveis do shim | Esperado |
|---|---|---|
| tudo certo | (padrão) | 0, duas linhas "Tag ... encontrada" |
| sem token | `FALSO_TOKEN_JSON='{"erro":1}'` | 1, "não devolveu o token" |
| corpo vazio | `FALSO_TOKEN_JSON=VAZIO` | 1, mesma mensagem |
| DNS fora | `FALSO_TOKEN_FALHA=1` | 1; registre se sai a mensagem do script ou só a do `curl` |
| API ausente | `FALSO_HTTP_API=404` | 1, cita a tag e o HTTP 404 |
| só a `-migrator` ausente | `FALSO_HTTP_MIGRATOR=404` | 1, cita `<sha>-migrator` |
| privado | `FALSO_HTTP_API=401` | 1 |
| rede caiu | `FALSO_HTTP_API=000` | 1; a mensagem diz "não existe ou o pacote não é público". Se só disser isso para um timeout, BUG baixa (mensagem enganosa) |
| limite de taxa | `FALSO_HTTP_API=429` | 1; idem |

Confira também que o token sai só no `::add-mask::` e em nenhuma outra linha.

#### QA11-21 — "Calcular expires_at" [P2, C e A, 15 min]

```bash
for h in 1 2 4 11 12; do wf c "HORAS=$h" wf-prazo.sh /work; done
wf c "HORAS=4 TZ=America/Sao_Paulo" wf-prazo.sh /work
```

Esperado (D4, I3; README "Como subir", passo 2): formato `AAAA-MM-DDTHH:MM:SSZ`; o provisório é
agora + 1 h, ou o definitivo se for menor (com `HORAS=1`, iguais); o fuso do runner não muda nada.
Depois, na trilha A, use o definitivo de `HORAS=12` num `plan` imediato e num `plan` 2 min depois:
os dois aceitos pela `validation` (12 h exatas são aceitas, teste S6).

#### QA11-22 — "Ler expires_at e image_sha do state" e "checar o TTL" [P1, C, 30 min]

Rode os dois passos com o `terraform` falso (`PATH=/passos/shims/tf:/usr/local/bin:/usr/bin:/bin`).
`FALSO_EXPIRES` e `FALSO_SHA` indefinidos simulam output ausente; `\n` vira quebra de linha.

| `FALSO_EXPIRES` | `FALSO_SHA` | "Ler" (destruir/limpar) esperado | "TTL" (agendado) esperado |
|---|---|---|---|
| indefinido | indefinido | marcações `1970-...` e 40 zeros | `vencido=true`, aviso "ausente ou inválido" |
| daqui a 3 h | 40 hex | os dois valores | `vencido=false`, resumo "restam 2h59min" ou 3h0min |
| daqui a 50 s | 40 hex | os dois | `vencido=false`, "restam 0h0min" (registre se confunde) |
| agora exato | 40 hex | os dois | `vencido=true` (restante 0) |
| 1 h atrás | 40 hex | os dois | `vencido=true` |
| `2026-11-19T10:00:00.5Z`, `...+03:00` | 40 hex | passam | conforme a data |
| `2026-02-30T10:00:00Z`, `2026-11-19T25:00:00Z`, `2026-11-19T1Z` | 40 hex | passam pela regex (registre) | `date` recusa? então vencido com aviso; se `date` aceitar algo absurdo, registre o horário que ele entendeu |
| `9999-12-31T23:59:59Z` | 40 hex | passa | `vencido=false` para sempre (só por edição manual do state: informativo) |
| `lixo`, vazio | maiúsculo, 39 | marcações | vencido, marcações |
| `2026-11-19T10:00:00Z\nimage_sha=evil` | 40 hex | marcação; nenhuma linha extra no `GITHUB_OUTPUT` | idem |

```bash
F3=$(date -u -d '+3 hours' +%Y-%m-%dT%H:%M:%SZ); S1=$(printf '1%.0s' {1..40})
wf c "PATH=/passos/shims/tf:/usr/local/bin:/usr/bin:/bin FALSO_EXPIRES=$F3 FALSO_SHA=$S1" wf-ttl.sh /work
wf c "PATH=/passos/shims/tf:/usr/local/bin:/usr/bin:/bin" wf-ler-saidas.sh /work
```

Esperado (spec "agendado", passo 4: "ausente, inválido ou vencido conta como vencido"; README
"Destruir").

- BUG alta: algum caso ausente ou inválido virando `vencido=false`, ou injeção no `GITHUB_OUTPUT`.

#### QA11-23 — "Ler o state" do agendado, com state real [P1, A, 30 min]

Objetivo: o agendado distingue "sem ambiente" de "erro de leitura" pelo texto `No state file was
found`. Esse texto precisa ser o que o Terraform 1.17.0 escreve, senão o agendado fica vermelho sem
ambiente, ou verde sem ler.

Numa cópia `/work/ls` com `init` (backend local do override):

| Situação | Como montar | Esperado |
|---|---|---|
| nunca houve state | `rm -f floci.tfstate*` | `ativo=false` |
| depois de um `destroy` | `sobe.sh` + `desce.sh` | `ativo=false` |
| ambiente no ar | `sobe.sh` | `ativo=true` e a contagem |
| state corrompido | `truncate -s 50% floci.tfstate` (cópia de segurança antes) | `codigo=1`, `::error::Falha ao ler o state` e as primeiras linhas do erro |
| state que não é JSON | `echo lixo > floci.tfstate` | `codigo=1` |
| só um data source no state | `terraform apply -target=data.aws_caller_identity.current` (ou o endereço que existir) | registre: `ativo=true` leva a um `destroy` inofensivo |
| lock preso | um `apply` em segundo plano segurando o lock | lê sem esperar? registre |

```bash
ra 'rm -f floci.tfstate*; terraform state list; echo codigo=$?' /work/ls      # texto exato do erro
wf a "" wf-ler-state.sh /work/ls
```

Registre o texto exato de cada erro e compare com o `grep` do passo. Com o S3 (QA11-14, passo 1), o
mesmo para o backend real.

- BUG alta: "nunca houve state" dando `codigo=1` (agendado vermelho para sempre sem ambiente) ou erro
  de leitura dando `ativo=false` (fail open).

#### QA11-24 — "Guarda - execução manual em andamento" (`gh run list`) [P2, C, 15 min]

`PATH=/passos/shims/gh:/usr/local/bin:/usr/bin:/bin` e `REPOSITORIO=matheuscardosolazzarini/renda-fixa-api`.

| Shim | Esperado |
|---|---|
| tudo 0 | `pular=false` |
| `FALSO_GH_IN_PROGRESS=1` | `pular=true` e `::warning::` com 1 |
| `FALSO_GH_QUEUED=2 FALSO_GH_WAITING=1` | `pular=true`, 3 |
| `FALSO_GH_PENDING=VAZIO` | registre: `$((ativas + ))` é erro de sintaxe; o passo fica vermelho? com qual mensagem? |
| `FALSO_GH_FALHA=queued` | `codigo` diferente de 0, vermelho; registre a mensagem |
| `FALSO_GH_IN_PROGRESS=null` | registre: `null` numa expressão aritmética vale 0 e daria `pular=false` em silêncio |

Esperado (spec "agendado", passo 1). Falha do `gh` deixando o ciclo vermelho é aceitável (o próximo
tenta de novo); `pular=false` com a resposta ilegível é fail open.

#### QA11-25 — Corridas entre o agendado e um `subir` [P1, A, 40 min]

Objetivo: a guarda do QA11-24 olha a fila **uma vez**, no início. Entre ela e o "Ler o state" passam
o checkout, a credencial, o setup e o `init` (perto de 1 min). Um `subir` disparado nessa janela faz o
agendado ler um state no meio do `apply`. O QA reproduz a janela sem a guarda.

```bash
# a. o agendado lê o state durante a fase 0 de um subir que começa do zero
ra 'bash /passos/desce.sh > /dev/null 2>&1; rm -f floci.tfstate*'
docker exec -d -w /work/repo/infra/aws qa11a-runner bash -c "bash /passos/sobe.sh $(date -u -d '+1 hour' +%Y-%m-%dT%H:%M:%SZ) 0 > /tmp/sobe-corrida.log 2>&1"
for i in $(seq 1 18); do
  ra 'printf "%s recursos=%s expires=%s\n" "$(date +%T)" "$(terraform state list 2>/dev/null | wc -l)" "$(terraform output -raw expires_at 2>/dev/null || echo AUSENTE)"'
  sleep 10
done
```

No primeiro instante em que houver recursos no state e `expires` ausente, rode o que o agendado
rodaria: `wf a "" wf-ler-state.sh` e `wf a "" wf-ttl.sh`; se der `vencido=true`, rode
`bash scripts/parar-migracao.sh` e o `destroy` do agendado com `-lock-timeout=5m` (o real). Registre:
o `destroy` espera o `apply` terminar e destrói o ambiente recém-criado? Quanto tempo o `subir`
"real" ficaria esperando na fase seguinte, e com que erro?

```bash
# b. subir disparado enquanto o agendado destrói
ra 'bash /passos/sobe.sh > /dev/null'
docker exec -d -w /work/repo/infra/aws qa11a-runner bash -c 'bash /passos/desce.sh > /tmp/desce-corrida.log 2>&1'
sleep 5; ra 'terraform plan -input=false -lock-timeout=30s -var-file=environments/floci.tfvars -var expires_at=$(date -u -d "+2 hours" +%Y-%m-%dT%H:%M:%SZ) -out=c.tfplan; echo plan=$?'
```

Esperado (spec: "evita ler um state no meio do `apply`"; README "O agendado"): em (a), nada que o
agendado faça pode destruir um ambiente que está sendo criado; em (b), a mensagem de lock diz o que
está acontecendo (quem segura, desde quando). O `-lock-timeout=30s` é redução do QA; o real é 5m.

- Classificação da decisão 5 (depende da janela do GitHub, que só o P8 mede).

#### QA11-26 — O agendado de ponta a ponta no Floci [P1, A, 40 min]

Objetivo: a sequência inteira do agendado (sem a guarda do `gh`, que é do QA11-24), com o Terraform
e a CLI reais, nos quatro estados da spec. A única adaptação é `-var-file=environments/floci.tfvars`
no lugar de `aws.tfvars` + `region`, e os artefatos D1/D2 do Floci antes da verificação.

```bash
agendado() { # pasta
  wf a "" wf-ler-state.sh "$1" | tee /tmp/ls.txt
  if grep -q 'ativo=false' /tmp/ls.txt; then ra 'time bash scripts/verificar-zerado.sh; echo saida=$?' "$1"; return; fi
  wf a "" wf-ttl.sh "$1" | tee /tmp/ttl.txt
  grep -q 'vencido=true' /tmp/ttl.txt || return 0
  exp=$(sed -n 's/^expires_at=//p' /tmp/ttl.txt); sha=$(sed -n 's/^image_sha=//p' /tmp/ttl.txt)
  ra "time { bash scripts/parar-migracao.sh && terraform destroy -auto-approve -input=false -lock-timeout=5m -var-file=environments/floci.tfvars \
      -var expires_at=$exp -var image_sha=$sha -var service_desired_count=0 > /tmp/d.log && bash /passos/artefatos.sh \
      && VERIFICAR_ESPERA_SEGUNDOS=10 bash scripts/verificar-zerado-apos-destroy.sh; }; echo fim=\$?" "$1"
}
agendado /work/repo/infra/aws                                                     # 1. sem ambiente
ra 'bash /passos/sobe.sh'; agendado /work/repo/infra/aws                          # 2. no prazo
ra "bash /passos/sobe.sh $(date -u -d '+3 minutes' +%Y-%m-%dT%H:%M:%SZ)"; sleep 200; agendado /work/repo/infra/aws   # 3. vencido
ra 'bash /passos/sobe.sh && terraform state pull | jq "del(.outputs.expires_at) | .serial += 1" > /tmp/s.json && terraform state push /tmp/s.json'
agendado /work/repo/infra/aws                                                     # 4. expires_at ausente
```

Esperado (spec "agendado"; README "O agendado"): (1) verificação 0, verde; (2) verde, resumo com o
tempo restante, nada destruído; (3) e (4) destruído e verificação 0, com o aviso de `expires_at`
ausente no (4). Registre o tempo total de cada ciclo: com o `destroy` e uma eventual repetição da
verificação, cabe folgado em 30 min e em `timeout-minutes: 60`?

- BUG alta: (3) ou (4) terminando sem destruir, ou (2) destruindo.

#### QA11-27 — `destruir` e `limpar` de ponta a ponta, e a repetição da verificação [P2, A, 30 min]

```bash
# a. caminho normal do destruir
ra 'bash /passos/sobe.sh'
wf a "" wf-ler-saidas.sh | tee /tmp/ls.txt   # use os valores lidos no destroy, como o job
ra "bash scripts/parar-migracao.sh && terraform destroy -auto-approve -input=false -lock-timeout=5m -var-file=environments/floci.tfvars \
    -var expires_at=<lido> -var image_sha=<lido> -var service_desired_count=0 > /tmp/d.log && bash /passos/artefatos.sh \
    && VERIFICAR_ESPERA_SEGUNDOS=10 bash scripts/verificar-zerado-apos-destroy.sh; echo fim=\$?"
# b. resíduo que persiste nas duas tentativas
ra 'aws secretsmanager create-secret --name qa11/persiste --secret-string x > /dev/null
    time VERIFICAR_ESPERA_SEGUNDOS=10 bash scripts/verificar-zerado-apos-destroy.sh; echo saida=$?
    aws secretsmanager delete-secret --secret-id qa11/persiste --force-delete-without-recovery > /dev/null'
# c. resíduo que some durante a espera (atraso de consistência simulado)
ra 'aws secretsmanager create-secret --name qa11/some --secret-string x > /dev/null
    (sleep 8; aws secretsmanager delete-secret --secret-id qa11/some --force-delete-without-recovery > /dev/null) &
    VERIFICAR_ESPERA_SEGUNDOS=20 bash scripts/verificar-zerado-apos-destroy.sh; echo saida=$?'
```

Esperado (P6, cabeçalho do `apos-destroy`): (a) `fim=0`; (b) saída 1, as duas listas e o aviso de
repetição; (c) saída 0. Leia o log de (c) como o Matheus leria no Actions: `RESIDUO`, depois `AVISO`,
depois `APROVADA`. Fica claro que o final é verde e por quê? Se não, BUG baixa.

### F. Bootstrap

#### QA11-28 — `bootstrap.sh` contra o Floci [P1, B (Floci novo), 45 min]

Objetivo: o README diz que o bootstrap é idempotente e pode rodar de novo "para corrigir um passo que
falhou ou para atualizar as policies". O QA roda como o README manda, duas vezes, e com as entradas
erradas que o operador digitaria.

Pré-condição: Floci B **recriado** (vazio, sem a execution role do `init.sh`) e runner `b` recriado.

```bash
rb 'time bash bootstrap/bootstrap.sh --email voce@exemplo.com > /tmp/b1.out 2> /tmp/b1.err; echo saida=$?; cat /tmp/b1.out; cat /tmp/b1.err'
rb 'time bash bootstrap/bootstrap.sh --email voce@exemplo.com > /tmp/b2.out 2> /tmp/b2.err; echo saida=$?; diff /tmp/b1.out /tmp/b2.out; cat /tmp/b2.err'
rb 'aws s3api get-bucket-versioning --bucket renda-fixa-api-tfstate-000000000000; aws s3api get-bucket-lifecycle-configuration --bucket renda-fixa-api-tfstate-000000000000
    aws s3api get-public-access-block --bucket renda-fixa-api-tfstate-000000000000; aws s3api get-bucket-policy --bucket renda-fixa-api-tfstate-000000000000 --query Policy --output text | jq -c ".Statement[].Effect"
    aws iam get-role --role-name renda-fixa-gha --query "Role.MaxSessionDuration"; aws iam list-role-policies --role-name renda-fixa-gha
    aws iam list-open-id-connect-providers; aws budgets describe-budgets --account-id 000000000000 2>&1 | head -5'
rb 'bash scripts/verificar-zerado.sh; echo saida=$?'
```

Depois:

1. **Policy gerenciada anexada à mão**: crie `qa11-extra` (`iam create-policy`) e anexe à
   `renda-fixa-gha`; rode o bootstrap de novo. O cabeçalho promete só um `AVISO` em `stderr` com
   saída 0: registre (decisão 5: basta avisar?).
2. **E-mails** (`--email` e `BUDGET_EMAIL`): vazio, `a@b`, `a b@c.com`, `'a"b@c.com'`, `a@b.c`,
   `álvaro@x.com`, um com 300 caracteres, e `voce@exemplo.com` (o exemplo do README, aceito: o alerta
   iria para um domínio de exemplo). `BUDGET_EMAIL=x@y.com` com `--email z@w.com`: qual vale?
3. **Região**: `AWS_REGION=sa-east-1 bash bootstrap/bootstrap.sh --email ...` (CloudShell aberto em
   outra região): o bucket é criado em `us-east-1`? (`get-bucket-location`)
4. **Falha no meio**: se o Floci não tiver Budgets, a primeira execução já para no fim; registre que
   tudo antes ficou criado e que a segunda execução não quebra no que já existe.

Esperado (README "Bootstrap no CloudShell"; spec "Bootstrap"): segunda execução sem erro e com
"já existe"/"atualizada"; saída só com identificadores (sem e-mail, sem segredo); bucket com
versionamento, expiração de 7 dias, bloqueio público e negação sem TLS; role de CI com sessão de
7200 s; **a verificação de zerado aprova depois do bootstrap** (contrato: os recursos do bootstrap
não usam a tag `project`). O que o Floci não suportar (OIDC, Budgets, service-linked roles), marque
`NÃO EXECUTADO` por item.

- BUG alta: verificação reprovando por causa do bootstrap (o agendado sem ambiente ficaria vermelho
  para sempre).
- BUG média: segunda execução falhando no que já existe.

#### QA11-29 — A policy da role do CI contra o que os jobs e os scripts chamam [P1, Git Bash e jq, 40 min]

Objetivo: na AWS real, uma permissão de leitura que falte na policy faz a verificação sair 2 em todo
ciclo (agendado vermelho para sempre) e uma de escrita derruba o `subir` no meio. Isso dá para
conferir sem AWS, cruzando a policy impressa com as chamadas.

```bash
cd "$REPO" && bash infra/aws/bootstrap/bootstrap.sh --imprimir-policy-ci > "$QA/saidas/policy-ci.json"; echo saida=$?
JQ="docker run --rm -i ghcr.io/jqlang/jq@sha256:096b83865ad59b5b02841f103f83f45c51318394331bf1995e187ea3be937432"
$JQ -e . < "$QA/saidas/policy-ci.json" > /dev/null && echo "JSON válido"
$JQ -r '.Statement[] | [.Effect, (.Action | if type=="array" then join(",") else . end), (.Condition // {} | tostring)] | @tsv' < "$QA/saidas/policy-ci.json"
grep -ohE 'aws [a-z0-9-]+ [a-z0-9-]+' infra/aws/scripts/*.sh .github/workflows/infra-aws.yml | sort -u
grep -oE '(achar [a-z0-9-]+ .*|chamar [a-z0-9-]+ [a-z0-9-]+)' infra/aws/scripts/verificar-zerado.sh | sort -u
```

Monte a tabela "chamada → ação IAM → coberta pela policy?" para: cada chamada do
`verificar-zerado.sh` (inclusive `ecs describe-clusters` e `ecs describe-tasks`, que entraram com S4 e
I1 e não aparecem na lista de permissões do cabeçalho), do `parar-migracao.sh`, dos passos do
workflow (`ecs run-task`, `describe-tasks`, `stop-task`, `describe-services`, `elbv2
describe-target-health`, `logs tail`, que usa `logs:FilterLogEvents`) e do S3 do state (objeto e
`.tflock`). Confira também: o Deny tem toda a lista da spec (NAT, `AllocateAddress`, `RunInstances`,
`CreateVpcEndpoint`, `CreateTransitGateway`, `kms:CreateKey`, `route53:CreateHostedZone`, RDS fora
de `db.t4g.micro` ou Multi-AZ); não há `iam:Create*`, `iam:Put*`, `iam:Attach*`, `budgets:*` nem
`*:*`; a escrita regional tem `aws:RequestedRegion`; e liste as ações **permitidas** que poderiam
criar algo pago fora do desenho (por exemplo `elasticloadbalancing:CreateLoadBalancer` também cria
ELB clássico; `rds:CreateDBSnapshot`; `ec2:CreateVolume`), cruzando com o QA11-09.

Esperado (spec "Permissões da role do CI"; P6): toda chamada coberta. O que só a AWS confirma (a ação
exata que cada API exige, condições, `AccessDenied`) vai para o P8.

- BUG alta: chamada da verificação de zerado ou do `destroy` sem ação correspondente na policy.
- RISCO: ação permitida com potencial de custo fora do desenho.

### G. Imagem de migração e a API com a mesma connection string

#### QA11-30 — Bundle e imagens (preparação da trilha D) [P1, D, 25 min]

Na cópia, nunca no repositório. O bundle é o mesmo comando do `release.yml`, num contêiner do SDK.

```bash
docker run --rm -v "$WQA\\arvore:/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 bash -c \
  'dotnet tool restore && dotnet ef migrations bundle --project src/FixedIncome.Infrastructure --startup-project src/FixedIncome.Api \
   --configuration Release --self-contained --target-runtime linux-x64 --output artifacts/efbundle --force && ls -la artifacts'
docker build -f "$WQA\\arvore\\Dockerfile.migrator" -t qa11-migrator:tmp "$WQA\\arvore\\artifacts"
docker image inspect qa11-migrator:tmp --format '{{.Config.User}} {{json .Config.Entrypoint}} {{.Size}}'
docker network create qa11-net
```

Imagem da API: conforme a decisão 2 (`qa11-api:tmp` montada da cópia, ou `renda-fixa-api-api:latest`
só para rodar um contêiner novo; registre a data de criação dela e que o `Dockerfile` não mudou na F11).

Senha sentinela, 32 alfanuméricos como a do Terraform: `SENHA=QA11sentinelaSenha0123456789abcd`.
Toda saída da trilha D é conferida com `grep -c "$SENHA"` (esperado 0).

#### QA11-31 — Migração fora do caminho feliz [P1, D, 50 min]

Para cada caso: `time docker run --rm --name qa11-mig --network qa11-net <env> qa11-migrator:tmp >
"$QA/saidas/31-<caso>.txt" 2>&1; echo saida=$?` e depois `grep -c "$SENHA"` e `grep -c Password=` no
arquivo.

| Caso | Montagem | Esperado |
|---|---|---|
| variável ausente | sem `-e ConnectionStrings__DefaultConnection` | exit diferente de 0, mensagem que cite a variável |
| só `--connection` | `qa11-migrator:tmp --connection "<conn>"` sem a variável | exit diferente de 0 e mensagem clara (spec: a fábrica exige a variável) |
| banco fora do ar | `Host=qa11-nao-existe` e `Host=<ip da rede sem ninguém>` | exit diferente de 0; **tempo até falhar** (o `subir` espera até 10 min); senha não aparece |
| porta fechada | `qa11-pg` parado (`docker stop`) | idem |
| senha errada | `qa11-pg` no ar, `Password=$SENHA` errada | exit diferente de 0; senha não aparece |
| banco inexistente | `Database=qa11_nao_existe` | registre: cria o banco ou falha? (no RDS, o banco existe) |
| usuário sem privilégio | role `qa11_semdireito` com `LOGIN`, sem `CREATE` no schema `public` (padrão do PG 16 para quem não é dono) | exit diferente de 0, mensagem de permissão |
| TLS obrigatório | `qa11-pg-tls` (abaixo), connection string **sem** `SSL Mode` | exit 0 (Npgsql negocia TLS); controle: com `SSL Mode=Disable`, recusado pelo servidor |
| interrompida | `docker stop -t 1 qa11-mig` 2 s depois de iniciar, depois rodar de novo | a segunda execução termina; `__EFMigrationsHistory` com 3 linhas e as 5 tabelas |
| banco à frente | linha extra em `__EFMigrationsHistory` com id `29991231000000_Futura` | registre (prorrogar com um `sha` antigo cai aqui): exit e mensagem |

```bash
docker run -d --name qa11-pg --network qa11-net -e POSTGRES_USER=fixedincome -e POSTGRES_PASSWORD="$SENHA" -e POSTGRES_DB=fixedincome \
  postgres:16@sha256:f9a7b9ebf6f56201f949db745e47bc423c0f0f8294bafe991cc10bf74b029657
printf '%s\n' 'local all all trust' 'hostssl all all 0.0.0.0/0 scram-sha-256' 'hostnossl all all 0.0.0.0/0 reject' > "$QA/pg_hba_tls.conf"
docker run -d --name qa11-pg-tls --network qa11-net -e POSTGRES_USER=fixedincome -e POSTGRES_PASSWORD="$SENHA" -e POSTGRES_DB=fixedincome \
  -v "$WQA\\pg_hba_tls.conf:/qa/pg_hba.conf:ro" postgres:16@sha256:f9a7b9ebf6f56201f949db745e47bc423c0f0f8294bafe991cc10bf74b029657 \
  -c ssl=on -c ssl_cert_file=/etc/ssl/certs/ssl-cert-snakeoil.pem -c ssl_key_file=/etc/ssl/private/ssl-cert-snakeoil.key -c hba_file=/qa/pg_hba.conf
CONN="Host=qa11-pg;Port=5432;Database=fixedincome;Username=fixedincome;Password=$SENHA"   # mesma forma da connection string do Terraform
```

Se o certificado `snakeoil` não existir na imagem, gere um autoassinado com `openssl` dentro do
contêiner e registre. A forma da connection string vem do critério 6 da validação
(`Host=...;Port=...;Database=fixedincome;Username=fixedincome;Password=***`).

Esperado (spec D3; P8 "ausência de segredo nos logs, inclusive no `aws logs tail` da falha da
migração"): todo caso de falha sai diferente de 0 com mensagem legível, em tempo compatível com a
espera de 10 min do `subir`; **nenhuma** saída contém a senha.

- BUG alta: senha ou connection string em qualquer saída (no ECS ela vai para o CloudWatch, e o
  `subir` imprime o `aws logs tail` no Actions de um repositório público).
- RISCO: TLS obrigatório recusado sem `SSL Mode` (o RDS 16 exige TLS por padrão, verificar no P8).

#### QA11-32 — A API com a mesma connection string: TLS e o que vai para o log [P2, D, 30 min]

Na falha do serviço, o `subir` imprime as últimas 60 linhas do log da API no Actions. O QA confere o
que a API escreve nos casos ruins.

```bash
docker run -d --name qa11-api --network qa11-net -p 28080:8080 -e ASPNETCORE_ENVIRONMENT=Production -e IndexRatesImport__Enabled=false \
  -e ConnectionStrings__DefaultConnection="Host=qa11-pg-tls;Port=5432;Database=fixedincome;Username=fixedincome;Password=$SENHA" <imagem da API>
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:28080/health; curl -s -o /dev/null -w '%{http_code}\n' http://localhost:28080/api/assets
docker logs qa11-api 2>&1 | grep -c "$SENHA"; docker rm -f qa11-api
# senha errada, banco fora do ar e banco sem migrations (o caso B2 da spec): repita trocando a connection string
```

Esperado: com TLS obrigatório e o banco migrado (QA11-31), `/health` e `/api/assets` 200; nos casos
ruins, `/health` 200 (não consulta o banco), `/api/assets` com o envelope de erro e **nenhuma** senha
no log. Com o banco sem migrations e a importação **ligada** (`IndexRatesImport__Enabled=true`, só
se a decisão 2 permitir chamar o BC), registre a mensagem da falha da importação (é o que a ordem da
D3 evita).

### H. Documentação

#### QA11-33 — README seguido ao pé da letra [P1, E e B, 50 min]

Siga a seção "Nuvem na AWS (F11)" do README na ordem, como o operador faria, executando contra o
Floci o que for executável e lendo o resto. Para cada subseção, anote onde o texto falha, omite ou
contradiz o código.

1. **Aviso do Free plan**: a data de fechamento está como `(preencher no P0...)` (conhecido; só
   confirme).
2. **Bootstrap no CloudShell**: o `git clone` (não execute: é GitHub) vem antes do `bash
   renda-fixa-api/infra/aws/bootstrap/bootstrap.sh`; o caminho relativo bate com a pasta do clone?
   O exemplo `voce@exemplo.com` passa na validação "estrita" (QA11-28)? O texto diz que o script
   imprime "os três identificadores": confira com a saída do QA11-28.
3. **Configuração no GitHub**: os nomes das variables (`AWS_ROLE_ARN` de repositório, `AWS_REGION` e
   `TF_STATE_BUCKET` do environment) batem com o `infra-aws.yml`.
4. **Como subir e destruir**: `horas` "inteiro de 1 a 12, padrão 4" contra o QA11-18 (`08` passa?
   o README deveria dizer?); "prazo de 15 min" contra o passo de espera; "Prorrogar" contra o
   QA11-11 e o QA11-06 (troca de SHA).
5. **O agendado**: "minutos 7 e 37" contra o `cron`; "o agendado o destrói em até 1 h" (passo 2)
   contra o provisório de 1 h mais o intervalo de 30 min do cron mais o atraso do GitHub.
6. **Camadas e Resíduos**: a tabela de resíduos contra o QA11-08 e o QA11-09 (região única, tipos fora
   da lista, "endereços IPv4 públicos da conta" contra o que o script lista).
7. **Lock preso**, ao pé da letra: `cd renda-fixa-api/infra/aws`, o `init` do README (contra o
   QA11-14), `terraform force-unlock <LOCK_ID>` (interativo); depois o passo 4 manda rodar
   `bash infra/aws/scripts/verificar-zerado.sh us-east-1`: a partir de qual pasta? O CloudShell traz
   o Terraform instalado? (Leitura da documentação pública da AWS, só se a decisão 4 permitir; senão
   `NÃO EXECUTADO` e vai para o P8.)
8. **Validação local e no PR**: o README diz o que o job faz, mas ensina a rodar o ciclo do Floci
   localmente? O operador que quiser testar uma mudança na infra antes do PR consegue só com o README?
   E o aviso de que o ciclo local deixa `floci_override.tf` e `floci.tfstate` na pasta (QA11-17)?
9. Todo comando de script no README usa `bash` (o README promete isso no último parágrafo)?

Esperado: cada comando copiável funciona na pasta em que o README deixa o operador.

- BUG média: comando do README que falha copiado literalmente.
- BUG baixa: promessa imprecisa (tempo, número, nome).

#### QA11-34 — Coerência entre README, spec, validação, ROADMAP e cabeçalhos [P2, E, 30 min]

Compare, e registre cada divergência com as duas citações:

- critério 13 no ROADMAP ("`subir` cancelado é limpo pelo `limpar`") contra a spec ("cancelado
  **fora de um `apply`**");
- spec "Log groups com prefixo do projeto" contra o filtro `contains` (QA11-09);
- spec "IPv4 ... e endereços IPv4 públicos da conta" contra o que o script lista;
- cabeçalho do `verificar-zerado.sh` (lista de permissões) contra as chamadas (QA11-29);
- cabeçalhos (`Uso:` e `Saída:`) de cada script contra o comportamento do QA11-01;
- README "scripts de parada" contra os nomes `parar-migracao.sh` e `verificar-zerado-apos-destroy.sh`
  (a validação já notou que o README é menos específico; só confirme se atrapalha o operador);
- a spec manda `-lock-timeout` "em todo comando do Terraform": `state list`, `output` e `state pull`
  não o têm (informativo: não pegam lock?);
- o "Status" no topo da spec ("aprovada... com as decisões D1 a D6") contra as "Decisões de
  implementação" (o que mudou depois da aprovação está todo lá?).

### I. Higiene local

#### QA11-35 — O que o ciclo deixa na máquina [P2, todas, 15 min, no fim]

```bash
docker ps -a --format '{{.Names}} {{.Image}} {{.Status}} {{.Ports}}' | diff "$QA/antes-ps.txt" -
rx a 'ls -la /work/repo/infra/aws; git -C /work/repo status --short 2>/dev/null || true'
diff <(cd "$REPO" && git status --short) "$QA/git-status-antes.txt" && echo "repositório igual"
ls -la --time-style=full-iso "$REPO/infra/aws/.terraform" | diff "$QA/terraform-dir-antes.txt" -
test ! -e "$REPO/infra/aws/floci_override.tf" && echo "sem override no repositório"
```

Registre: contêineres filhos do Floci que sobreviveram ao `destroy` ou à remoção do Floci (o job
`validar` remove só os de nome `floci`); se o `terraform init` na cópia alterou o
`.terraform.lock.hcl` (hashes novos de plataforma); arquivos `.terraform.tfstate.lock.info` deixados
pelos `kill`. Contêiner filho sobrando depois do `destroy` é BUG baixa (só local) e vai para o README
de "Validação local".

## O que pode rodar em paralelo

O gargalo é o Docker: cada Floci com RDS sobe um PostgreSQL filho, e os `apply` levam minutos.

1. **Sequencial no início (uma instância):** Preparação 0 a 6 e **QA11-00**. O QA11-00 decide se
   dois Flocis podem coexistir.
2. **Instância 1, trilha A (Floci A, porta 4566), um cenário de cada vez:** QA11-00, 11, 12, 13, 07,
   05 (parte do ciclo), 06, 23, 26, 27, 25, 16, 17, 21 (parte do `plan`), 14, 15. Depois, se sobrar
   tempo, os de leitura QA11-33 e 34. Cerca de 7 h.
3. **Instância 2, trilha B (Floci B, porta 4567), sem `apply` do ambiente:** QA11-01 (partes B), 02,
   03, 04, 05 (parte da trava), 08, 09, 10 e, por último, 28 (com o Floci B recriado). Em seguida a
   **trilha C e D** (sem Floci): QA11-01 (partes C), 18, 19, 20, 21, 22, 24, 29, 30, 31, 32. Cerca de
   7 h.
4. Se o QA11-00 mostrar contêiner filho com nome ou porta fixa, a criação de RDS à mão do QA11-08
   (trilha B) não pode coincidir com um `apply` da trilha A: combine pelos horários ou rode o QA11-08
   depois da trilha A.
5. **QA11-35 por último**, com tudo parado.

Cada instância escreve numa seção própria do `docs/QA-F11.md` (por exemplo "Trilha A" e "Trilhas B,
C e D"), e o coordenador junta a lista de bugs no fim.

**Duração estimada:** com duas instâncias, cerca de 7 a 8 h de relógio, mais 45 min de preparação.
Com uma só, 13 a 14 h. Só os **[P1]**: cerca de 8 h com uma instância, 4 a 5 h com duas. As
estimativas contam o RDS do Floci em cerca de 1,5 min por `apply` (medido na validação); se o
QA11-00 medir muito mais, refaça a conta.

## Fora deste roteiro (vai para o P8)

Só a AWS real ou o GitHub comprovam. Nada disso é marcado aqui; o QA anota o que observou de
relacionado no Floci, com a ressalva.

**AWS**

- Enforcement da policy: Deny da camada 6b (inclusive `ModifyDBInstance` e as chaves de condição do
  RDS), `aws:RequestedRegion`, `PassRole` restrito, `RunTask` com `--propagate-tags` e `ecs:cluster`,
  `StopTask` no ARN da tarefa, e as permissões não exercidas no Floci (`logs:TagResource`,
  `logs:TagLogGroup`, KMS via RDS).
- O que acontece com o state quando um `apply` recebe `AccessDenied` no meio (QA11-15 com a policy).
- OIDC e trust por `sub`, sessão de 2 h vencendo no meio de um `destroy`, credencial expirada de
  verdade (QA11-03).
- Comportamento real dos serviços: atraso da tagging API, cluster e serviço INACTIVE em
  `list-clusters` e na tagging API, log group do RDS e tag do serviço apagado (D1 e D2 do Floci),
  chave KMS gerenciada e `PendingDeletion`, proteção contra exclusão (QA11-05), janela de recuperação
  de secret, cobrança de IPv4 público.
- O ambiente de verdade: tarefa Fargate puxando do GHCR público, injeção do secret, driver `awslogs`,
  ALB e health check, `health_check_grace_period_seconds = 120`, RDS 16 exigindo TLS (QA11-31 é só a
  aproximação local), importação do BC a partir de `us-east-1`.
- O que o `aws logs tail` e o log da API imprimem no Actions numa falha real (QA11-31 e 32 são a
  aproximação).
- Budgets, e-mail de inscrição, Cost Explorer, Free account plan e a data de fechamento.
- CloudShell: ferramentas instaladas (Terraform?), região em que abre, persistência da pasta, MFA.

**GitHub**

- O `if` do `limpar`, o output `iniciou` com job cancelado, `needs`, `failure()`/`cancelled()`.
- Filas de `concurrency` (`infra-aws-manual`, `infra-aws-limpar`, `infra-aws-agendado`) e a janela
  real entre a guarda do agendado e o `init` (QA11-25).
- Environment `aws` com branch policy e sem reviewers; variables de repositório e de environment.
- Atraso do `cron`, desativação após 60 dias, e o que conta como atividade.
- `gh run list` com os status reais, `GITHUB_STEP_SUMMARY`, `::add-mask::` e os logs públicos.
- O job `validar` num PR (critério 2), a primeira execução do `release.yml` com a `-migrator`
  (`--network host`, pull do GHCR), o token anônimo do GHCR, o formato de `docker buildx imagetools
  inspect` (QA11-20 usa fixtures) e o pacote público.
- Cancelamento de um `subir` durante o `apply` na AWS real (I2; QA11-13 é a aproximação).

Também fora: critérios 1, 3 a 9 já provados pela validação; o achado do zizmor no `release.yml`
(decisão pendente do Matheus, critério 8); qualquer comportamento da API além do que a F11 toca.

## Limpeza ao fim

```bash
docker rm -f qa11a-runner qa11b-runner qa11c-runner qa11a-floci qa11b-floci qa11-pg qa11-pg-tls qa11-api qa11-mig 2>/dev/null
docker ps -a --format '{{.Names}} {{.Image}} {{.Status}} {{.Ports}}' | diff "$QA/antes-ps.txt" -   # sobra = filho do Floci: remover pelo nome e registrar
docker volume rm qa11a-work qa11b-work qa11c-work qa11a-plugins qa11b-plugins qa11c-plugins
docker network rm qa11-net
docker image rm qa11-runner:tmp qa11-migrator:tmp qa11-api:tmp 2>/dev/null
docker volume ls -q | sort | comm -13 "$QA/antes-volumes.txt" -       # vazio
docker network ls --format '{{.Name}}' | sort | comm -13 "$QA/antes-networks.txt" -   # vazio
docker images --digests --format '{{.Repository}}:{{.Tag}} {{.Digest}}' | sort | comm -13 "$QA/antes-images.txt" -   # só os pulls autorizados
cd "$REPO" && git status --short | diff - "$QA/git-status-antes.txt" && echo "repositório do Matheus igual (exceto docs/QA-F11.md)"
```

- Tudo o que tem `qa11` no nome é do QA e é removido. Os volumes e imagens que já existiam não são
  tocados. `fixedincome-api` e `fixedincome-db` continuam como estavam (confira o `Status` no diff).
- As imagens baixadas com autorização (`hashicorp/terraform:1.17.0`, `dotnet/runtime-deps:8.0`, a da
  API se montada) ficam no cache e são listadas no relatório para o Matheus remover, se quiser.
- O bundle (cerca de 125 MB), a cópia `$QA/arvore` e os logs ficam no rascunho e podem ser apagados.
- O relatório lista os recursos `qa11` criados no Floci (somem com o contêiner) e confirma que nada foi
  escrito na AWS real nem no GitHub.

## Decisões antes de executar

1. **Downloads.** O QA precisa de: `hashicorp/terraform:1.17.0` (ou o zip `linux_amd64` 1.17.0 de
   `releases.hashicorp.com`, conferido pelo `SHA256SUMS`), o provider `hashicorp/aws` 6.73.0 do
   registry, `mcr.microsoft.com/dotnet/runtime-deps:8.0` e os pacotes NuGet do `dotnet tool restore`
   e do bundle. Nada é instalado na máquina; tudo fica no cache do Docker ou em volume. Autoriza? Sem
   o Terraform Linux, vale o "Caminho alternativo" (tempos sem valor de referência); sem o
   `runtime-deps`, a trilha D fica `NÃO EXECUTADO`.
2. **Imagem da API** (QA11-32): montar `qa11-api:tmp` da cópia (baixa `aspnet:8.0`) ou rodar um
   contêiner novo a partir da `renda-fixa-api-api:latest`, sem alterá-la? E, no último caso do QA11-32,
   ligar a importação do BC (chama a API pública do Banco Central)?
3. **Duas instâncias em paralelo**, cada uma com seu Floci (cerca de 2 a 3 GB a mais de memória com os
   PostgreSQL filhos)?
4. **Leitura de documentação pública da AWS** (ferramentas do CloudShell, QA11-33 item 7), sem
   nenhuma chamada à conta? Ou `NÃO EXECUTADO` e P8?
5. **Classificação de casos que a spec não prevê**, para o QA não decidir sozinho. Contam como `BUG`
   ou `RISCO`, e com que severidade?
   - atributos que impedem o `destroy` aprovados pela trava (`deletion_protection`,
     `enable_deletion_protection`, `prevent_destroy`, `skip_final_snapshot = false`,
     `recovery_window_in_days > 0`) (QA11-05);
   - custo fora dos limites atuais da trava: `ephemeral_storage`, Container Insights, exportação de
     logs do RDS, secrets extras, `storage_throughput` (QA11-04);
   - verificação de uma região só e de uma lista fechada de tipos (ELB clássico, EBS, Aurora, S3 com
     a tag) (QA11-09);
   - região trocada com o ambiente no ar (QA11-15);
   - a janela entre a guarda do agendado e o `init` (QA11-25);
   - o bootstrap só avisar quando há policy gerenciada anexada à role do CI (QA11-28);
   - o e-mail de exemplo do README aceito pela validação "estrita" (QA11-28).
6. **Momento.** Executar sobre a árvore de trabalho atual (fase não commitada, HEAD `f141590` mais as
   alterações), copiada para o rascunho, ou depois do commit?
7. **Backend S3 no Floci** (QA11-14): o QA escreve, só na cópia, um arquivo de configuração de
   backend com o endpoint do Floci e remove o bloco de backend do override gerado. Autoriza?
