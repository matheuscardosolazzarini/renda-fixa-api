# Roteiro de QA — F8: Entrega contínua da imagem e pendências da F7

Roteiro exploratório para o agente `qa-exploratorio`. O relatório vai em `docs/QA-F8.md`.
**Status: aguardando aprovação do Matheus** (ver "Decisões antes de executar", no fim).

A `docs/VALIDACAO-F8.md` já provou: as pendências da F7 com reversão em cópia; `GET`, `HEAD` e
`POST /health`; smoke local completo (bundle aplicado num banco limpo, 2 migrations e 139
feriados, `/health` e `/api/assets` com 200); reprovação do smoke sem bundle (exit 22);
bundle sem a variável e só com `--connection` (exit 1); `actionlint` limpo; leitura linha a linha
do `release.yml`. Isso **não** se repete aqui.

Este roteiro cobre o que um usuário da F8 faz com a imagem e o bundle, fora do caminho feliz:
a imagem em Production sem cotações, o banco fora do ar, a ordem de subida, o bundle com conexão
errada, a fábrica de design-time no `dotnet ef` local, a regra nova do CDI com datas reais de
calendário, a leitura adversarial dos workflows e o README seguido do zero.

Regras herdadas do agente: caixa-preta primeiro (o código de `src/` só é aberto para explicar um
achado já reproduzido); cada achado traz comando, resposta obtida, resposta esperada e a fonte da
expectativa; nada é corrigido; nenhum `docker compose down -v`; dados de teste com prefixo `QA-`.

## Ambiente

### O que não pode ser tocado

Os contêineres `fixedincome-api` (porta 8080) e `fixedincome-db` (porta 5432) são do
desenvolvedor. Não pare, não reinicie, não reconstrua, não escreva neles e não use
`docker compose` neste roteiro (o Compose mexeria nesses contêineres). Toda a execução usa nomes,
rede e portas próprios, com o prefixo `qaf8`:

| Recurso | Nome | Porta no host |
|---|---|---|
| Rede | `qaf8-net` | — |
| PostgreSQL 16 principal | `qaf8-db` | 25432 |
| PostgreSQL 16 descartável (testes que derrubam o banco) | `qaf8-dbx` | 25433 |
| Imagem do working tree | `qaf8/renda-fixa-api:wt` | — |
| API em Production, importação desligada | `qaf8-api-prod` | 28080 |
| API para ordem de subida e banco fora | `qaf8-api-x` | 28081 |
| API com importação ligada (Banco Central) | `qaf8-api-bc` | 28082 |
| Proxy de interceptação (opcional, QA8-19) | `qaf8-mitm` | — |

### Preparação (Git Bash)

Os comandos estão em Git Bash. No PowerShell 5.1, `curl` é alias de `Invoke-WebRequest`; se usar
PowerShell, chame `curl.exe`.

0. Docker Desktop aberto. Confirme as portas livres e tire a foto do estado do desenvolvedor,
   só leitura, para conferir no fim que nada mudou:

   ```bash
   netstat -ano | grep -E ':(25432|25433|28080|28081|28082) ' || echo "portas livres"
   docker ps --format '{{.Names}} {{.Status}} {{.Ports}}' > "$QA/antes-ps.txt"
   docker images --format '{{.Repository}}:{{.Tag}}' | sort > "$QA/antes-images.txt"
   docker exec fixedincome-db psql -U fixedincome -d fixedincome -tAc \
     "select (select count(*) from holiday), (select count(*) from index_rate), (select count(*) from \"__EFMigrationsHistory\")" \
     > "$QA/antes-devdb.txt"
   ```

1. Variáveis da sessão (ajuste `QA` para o scratchpad da sessão que executar):

   ```bash
   export MSYS_NO_PATHCONV=1
   REPO=/c/Users/matheuss/Desktop/renda-fixa/renda-fixa-api
   QA=/c/Users/.../scratchpad/qaf8; mkdir -p "$QA"
   PG=QaF8Senha123            # senha propositalmente reconhecível, para procurar vazamento nos logs
   cs()  { echo "Host=qaf8-db;Port=5432;Database=$1;Username=qaf8;Password=$PG"; }
   sql() { docker exec qaf8-db psql -U qaf8 -d "$1" -tAc "$2"; }
   ```

2. Rede, banco e bancos lógicos:

   ```bash
   docker network create qaf8-net
   docker run -d --name qaf8-db --network qaf8-net -p 25432:5432 \
     -e POSTGRES_USER=qaf8 -e POSTGRES_PASSWORD=$PG -e POSTGRES_DB=qaf8_prod postgres:16
   until docker exec qaf8-db pg_isready -U qaf8 >/dev/null 2>&1; do sleep 1; done
   for db in qaf8_bundle qaf8_bundle2 qaf8_order qaf8_import qaf8_cal qaf8_efupd; do
     docker exec qaf8-db createdb -U qaf8 $db; done
   ```

3. Imagem do working tree, como o pipeline a constrói (sem `ASPNETCORE_ENVIRONMENT`, logo
   Production):

   ```bash
   cd "$REPO" && git rev-parse HEAD && git status --short > "$QA/git-status.txt"
   docker build -t qaf8/renda-fixa-api:wt \
     --label org.opencontainers.image.revision=$(git rev-parse HEAD) .
   ```

4. Bundle gerado **no scratchpad**, não na raiz do repositório:

   ```bash
   cd "$REPO" && dotnet tool restore && dotnet ef migrations bundle \
     --project src/FixedIncome.Infrastructure --startup-project src/FixedIncome.Api \
     --configuration Release --self-contained --target-runtime linux-x64 \
     --output "$(cygpath -w "$QA")\\efbundle" --force
   ```

   Modelo de execução do bundle (contêiner `runtime-deps`, como na `VALIDACAO-F8.md`):

   ```bash
   runbundle() { docker run --rm --network qaf8-net -v "$(cygpath -w "$QA"):/b" -w /b "$@"; }
   # uso: runbundle -e ConnectionStrings__DefaultConnection="$(cs qaf8_prod)" mcr.microsoft.com/dotnet/runtime-deps:8.0 ./efbundle
   ```

5. Aplique o bundle em `qaf8_prod` e suba a API de Production sem importação:

   ```bash
   runbundle -e ConnectionStrings__DefaultConnection="$(cs qaf8_prod)" mcr.microsoft.com/dotnet/runtime-deps:8.0 ./efbundle
   docker run -d --name qaf8-api-prod --network qaf8-net -p 28080:8080 \
     -e ConnectionStrings__DefaultConnection="$(cs qaf8_prod)" \
     -e IndexRatesImport__Enabled=false qaf8/renda-fixa-api:wt
   docker logs qaf8-api-prod 2>&1 | grep -i "hosting environment"   # esperado: Production
   ```

Se o Docker não estiver disponível, todo o roteiro, exceto QA8-13, QA8-14, QA8-20 e QA8-22,
fica `NÃO EXECUTADO`.

## Cenários

Legenda: **BUG** quando a resposta contraria a spec, o README ou uma regra do agente (500 não
tratado, vazamento, comando documentado que não funciona); **RISCO** quando o comportamento é
defensável pela spec, mas surpreende o usuário ou depende de execução real para confirmar.

### Imagem em Production (`qaf8-api-prod`, porta 28080)

#### QA8-01 — `/health` fora do caminho feliz

Objetivo: o contrato do liveness além do que a validação testou (GET, HEAD e POST).

```bash
B=http://localhost:28080
for m in GET HEAD OPTIONS PUT DELETE PATCH TRACE; do
  printf "%-7s " $m; curl -s -o /dev/null -w '%{http_code} %{content_type}\n' -X $m $B/health; done
curl -si -X OPTIONS $B/health | grep -iE '^(HTTP|allow)'; curl -s -X OPTIONS $B/health; echo
curl -s -w ' %{http_code}\n' "$B/health?x=1&y=%3Cscript%3E"
curl -s -w ' %{http_code}\n' $B/health/          # barra final
curl -s -w ' %{http_code}\n' $B/HEALTH           # caixa
curl -s -w ' %{http_code}\n' $B/Health
curl -s -w ' %{http_code}\n' $B/health/ready      # F10, ainda não existe
curl -s -w ' %{http_code}\n' $B/health/live
curl -s -w ' %{http_code}\n' $B/healthz
curl -s -w ' %{http_code}\n' -H 'Accept: application/json' $B/health
curl -s -w ' %{http_code}\n' -X POST -d '{"a":1}' $B/health          # sem Content-Type: 405 ou 415?
curl -s -w ' %{http_code}\n' -X POST -H 'Content-Type: application/json' -d '{"a":1}' $B/health
curl -sI $B/health | grep -iE '^(HTTP|content-length|content-type)'
```

Esperado (SPEC-F8, "`/health` sem envelope", e ESPECIFICACAO, "só aceita GET e HEAD"): GET e
HEAD com 200 e `Healthy` em `text/plain` (HEAD sem corpo); qualquer outro verbo com 405 no
envelope `ApiResponse` e `Allow: GET, HEAD`; `/health/ready`, `/health/live` e `/healthz` com
404 no envelope; query string ignorada; `Accept: application/json` não muda a resposta.
A spec não diz nada sobre `/health/` e `/HEALTH`: registre o que sair.

- BUG: 500 em qualquer linha; 405 ou 404 vazio ou em `ProblemDetails`; POST com corpo
  respondendo 415 em vez de 405 (o recurso não aceita o verbo, o tipo do corpo é irrelevante);
  `OPTIONS` com 200 ou 204 (a spec restringe a GET e HEAD).
- RISCO: `/health/` ou `/HEALTH` respondendo diferente de `/health` (orquestrador configurado
  com barra final falharia), ou 404 sem envelope só nesses casos.

#### QA8-02 — Contrato de erro em Production

Objetivo: o envelope de 404, 405 e 415 (B-06 da F7) foi provado no Compose, em Development.
Aqui, na imagem que vai para produção.

```bash
curl -si $B/swagger | head -1; curl -s $B/swagger; echo
curl -s -w ' %{http_code}\n' $B/swagger/v1/swagger.json
curl -s -w ' %{http_code}\n' $B/swagger/index.html
curl -s -w ' %{http_code}\n' $B/api/foo
curl -si -X PATCH $B/api/assets | grep -iE '^(HTTP|allow)'
curl -s -w ' %{http_code}\n' -X POST -H 'Content-Type:' -d '{}' $B/api/assets
curl -s -w ' %{http_code}\n' -X POST -H 'Content-Type: text/plain' -d '{}' $B/api/assets
curl -s -w ' %{http_code}\n' -X POST -H 'Content-Type: application/json' -d '{malformado' $B/api/assets
curl -s -w ' %{http_code}\n' $B/api/assets/nao-e-guid
curl -si $B/api/assets | grep -iE '^(HTTP|location)'   # sem redirecionamento para https
```

Esperado: `/swagger*` com 404 no envelope (Swagger só em Development, README "Rodando a imagem
publicada"); 404, 405 (com `Allow`), 415 e 400 no envelope, iguais aos do Compose; nenhum
`307`/`308` para HTTPS (o log avisa `Failed to determine the https port`, observação 6 da
validação).

- BUG: qualquer resposta de erro fora do envelope ou com detalhe de exceção em Production.
- RISCO: redirecionamento para HTTPS numa imagem que só escuta HTTP.

#### QA8-03 — Endpoints sem o seed de cotações

Objetivo: o que o usuário vê quando sobe a imagem publicada como o README manda (o bundle cria
o esquema e os 139 feriados, mas nenhuma cotação). A importação está desligada.

```bash
post() { curl -s -X POST -H 'Content-Type: application/json' -d "$2" $B$1; echo; }
post /api/assets '{"name":"QA-F8 PRE12","issuer":"QA","assetType":"Cdb","indexType":"PreFixed","rate":12,"issueDate":"2026-01-01","maturityDate":"2028-01-01"}'
post /api/assets '{"name":"QA-F8 CDI100","issuer":"QA","assetType":"Cdb","indexType":"Cdi","rate":100,"issueDate":"2026-01-01","maturityDate":"2028-01-01"}'
post /api/assets '{"name":"QA-F8 IPCA6","issuer":"QA","assetType":"Cdb","indexType":"Ipca","rate":6,"issueDate":"2026-01-01","maturityDate":"2028-01-01"}'
# um aporte de R$ 1.000 em 2026-09-01 em cada título (use os ids devolvidos)
post /api/positions '{"assetId":"<id>","investedAmount":1000,"applicationDate":"2026-09-01"}'
```

Para cada aporte: `GET /api/positions/{id}/projection?referenceDate=2026-10-01` e sem
`referenceDate`. Depois: `GET /api/portfolio/summary` com e sem data,
`GET /api/indexes/Cdi?from=2026-09-01&to=2026-10-01` e `GET /api/indexes/Ipca?...`.

Esperado (README, "Sem cotações importadas, a projeção de CDI e de IPCA+ falha; a de PreFixed não
depende delas"; RN-09):

- PreFixed: 200, com dias úteis contados pelo calendário do seed. Sem data, a referência é "hoje"
  em Brasília (ESPECIFICACAO, data padrão sem cotação de CDI): `elapsedDays` = dias corridos de
  2026-09-01 até a data de Brasília da execução.
- CDI: 400 `"Cotação de CDI ausente para 2026-09-01."` (a primeira data sem cotação), com e sem data.
- IPCA+: 400 com mensagem que diga que não há IPCA publicado.
- Resumo: 400 enquanto houver posição CDI ou IPCA+ (comportamento já conhecido da F7, QA-08).
  Anote a mensagem: ela diz qual posição ou o que fazer?
- Cotações: 200 com `data: []`.

- BUG: 500 em qualquer linha; PreFixed falhando por falta de cotação.
- RISCO: mensagem do resumo que não ajuda quem acabou de subir a imagem (não cita importação
  desligada nem a falta de cotações).
- Bônus, fora do escopo da F8: se a execução cair entre 21h e 24h de Brasília, a projeção
  PreFixed sem data é o cenário do "R-01 ao vivo" (pendência da F7). Registre `elapsedDays` e a
  hora UTC e de Brasília, sem classificar como achado da F8.

#### QA8-04 — Configuração da imagem pelo ambiente

Objetivo: erros de configuração que um usuário real comete ao rodar `docker run`.

Todos com proxy morto, para nenhum caso chamar o Banco Central:
`-e HTTPS_PROXY=http://127.0.0.1:9 -e HTTP_PROXY=http://127.0.0.1:9`.

| Caso | Comando (variações sobre `qaf8-api-x`, porta 28081) | Esperado |
|---|---|---|
| a. Sem connection string | `docker run -d --name qaf8-api-x --network qaf8-net -p 28081:8080 -e HTTPS_PROXY=http://127.0.0.1:9 qaf8/renda-fixa-api:wt` | Ou o processo sai na partida com mensagem clara, ou sobe e `/api/assets` devolve erro tratado. Registre qual. `/health` 200 se o processo subir |
| b. Senha errada | `-e ConnectionStrings__DefaultConnection="Host=qaf8-db;Port=5432;Database=qaf8_prod;Username=qaf8;Password=errada"` | `/health` 200; `/api/assets` 500 no envelope genérico; log sem a connection string |
| c. `IndexRatesImport__Enabled` = `false`, `False`, `0`, `no`, vazio, ausente | um `docker run` por valor, com a conexão de `qaf8_prod` | Registre, pelo log (`Sending HTTP request ... sgs.12` ou falha do proxy), em quais a importação roda. Ausente = ligada (`appsettings.json`) |
| d. Parada | `time docker stop qaf8-api-prod`; `docker inspect -f '{{.State.ExitCode}}' qaf8-api-prod`; depois `docker start qaf8-api-prod` | Para em menos de 10 s com exit 0 (não 137, que é morte forçada) |

Depois de cada caso: `docker logs qaf8-api-x 2>&1 | grep -c "$PG"` e
`grep -ciE 'password=|Password='` no log devem dar 0; `docker rm -f qaf8-api-x`.

- BUG: senha ou connection string no corpo da resposta ou no log; processo que cai depois de
  subir; 500 sem envelope.
- RISCO: no caso a, a API sobe "saudável" (`/health` 200) sem connection string nenhuma, e só o
  primeiro uso revela o erro; no caso c, valores como `0` ou `no` serem tratados de forma
  diferente de `false` sem aviso no log; no caso d, exit 137.

### Banco fora do ar

#### QA8-05 — Banco parado

Objetivo: o liveness não depende do banco (SPEC-F8) e a falha do banco é tratada, sem vazamento,
com recuperação sem reiniciar a API. Usa um banco próprio descartável para não interromper os
outros cenários.

```bash
docker run -d --name qaf8-dbx --network qaf8-net -p 25433:5432 -e POSTGRES_USER=qaf8 \
  -e POSTGRES_PASSWORD=$PG -e POSTGRES_DB=qaf8_x postgres:16
until docker exec qaf8-dbx pg_isready -U qaf8 >/dev/null 2>&1; do sleep 1; done
runbundle -e ConnectionStrings__DefaultConnection="Host=qaf8-dbx;Port=5432;Database=qaf8_x;Username=qaf8;Password=$PG" \
  mcr.microsoft.com/dotnet/runtime-deps:8.0 ./efbundle
docker run -d --name qaf8-api-x --network qaf8-net -p 28081:8080 -e IndexRatesImport__Enabled=false \
  -e ConnectionStrings__DefaultConnection="Host=qaf8-dbx;Port=5432;Database=qaf8_x;Username=qaf8;Password=$PG" \
  qaf8/renda-fixa-api:wt
X=http://localhost:28081
curl -s -w ' %{http_code}\n' $X/api/assets            # 200
docker stop qaf8-dbx
for u in /health /api/assets /api/positions /api/portfolio/summary "/api/indexes/Cdi?from=2026-01-01&to=2026-01-31"; do
  curl -s -w " %{http_code} %{time_total}s\n" "$X$u"; done
curl -s -X POST -H 'Content-Type: application/json' -w ' %{http_code}\n' \
  -d '{"name":"QA-F8 dbdown","issuer":"QA","assetType":"Cdb","indexType":"PreFixed","rate":10,"issueDate":"2026-01-01","maturityDate":"2027-01-01"}' $X/api/assets
docker logs qaf8-api-x 2>&1 | grep -c "$PG"
docker logs qaf8-api-x 2>&1 | grep -iE 'password|connection ?string' | head
docker start qaf8-dbx; until docker exec qaf8-dbx pg_isready -U qaf8 >/dev/null 2>&1; do sleep 1; done
curl -s -w ' %{http_code}\n' $X/api/assets            # recuperou sem reiniciar a API?
```

Esperado: `/health` 200 e rápido o tempo todo; os endpoints de dados com 500 no envelope genérico
(`"Ocorreu um erro interno. Tente novamente mais tarde."`), sem pilha, host ou senha; o log com a
exceção, mas sem a senha; depois do `docker start`, `/api/assets` 200 sem reiniciar a API.

- BUG: `/health` diferente de 200; corpo com detalhes da exceção; senha no log; a API precisa ser
  reiniciada para voltar; o processo cai.
- RISCO: 500 em vez de 503 para indisponibilidade de dependência (a spec não define; registrar).
  Pela regra do agente, todo 500 é registrado; aqui ele é o sinal que o smoke usa, então a
  classificação final é do Matheus.

#### QA8-06 — Banco congelado

Objetivo: banco que aceita a conexão TCP, mas não responde (o caso mais comum de banco "lento").

```bash
docker pause qaf8-dbx
curl -s -w " %{http_code} %{time_total}s\n" $X/health
curl -s -w " %{http_code} %{time_total}s\n" --max-time 120 $X/api/assets &
sleep 2; curl -s -w " /health durante a espera: %{http_code} %{time_total}s\n" $X/health; wait
docker unpause qaf8-dbx
curl -s -w " %{http_code} %{time_total}s\n" $X/api/assets
```

Esperado: `/health` responde em menos de 1 s mesmo com uma requisição de dados presa; a
requisição de dados termina com erro tratado (anote o tempo: o timeout padrão do Npgsql é de
15 s para conectar e 30 s por comando); depois do `unpause`, 200.

- BUG: `/health` travado enquanto o banco está congelado; resposta de dados que nunca termina
  (passa de 120 s).
- RISCO: espera acima de 30 s sem resposta.

Ao fim: `docker rm -f qaf8-api-x qaf8-dbx`.

### Ordem de subida

#### QA8-07 — Imagem antes do bundle, importação desligada

```bash
docker run -d --name qaf8-api-x --network qaf8-net -p 28081:8080 -e IndexRatesImport__Enabled=false \
  -e ConnectionStrings__DefaultConnection="$(cs qaf8_order)" qaf8/renda-fixa-api:wt
curl -s -w ' %{http_code}\n' $X/health
curl -s -w ' %{http_code}\n' $X/api/assets
curl -s -X POST -H 'Content-Type: application/json' -w ' %{http_code}\n' \
  -d '{"name":"QA-F8 order","issuer":"QA","assetType":"Cdb","indexType":"PreFixed","rate":10,"issueDate":"2026-01-01","maturityDate":"2027-01-01"}' $X/api/assets
runbundle -e ConnectionStrings__DefaultConnection="$(cs qaf8_order)" mcr.microsoft.com/dotnet/runtime-deps:8.0 ./efbundle
curl -s -w ' %{http_code}\n' $X/api/assets     # sem reiniciar
docker logs qaf8-api-x 2>&1 | grep -E 'fail:|42P01' | head
```

Esperado: antes do bundle, `/health` 200 e os endpoints de dados com 500 no envelope genérico
(é o sinal do smoke, `VALIDACAO-F8.md`); depois do bundle, 200 sem reiniciar a API.

- BUG: corpo do 500 com o nome da tabela ou SQL; API que precisa de reinício com a importação
  desligada.
- RISCO: o README ("Rodando a imagem publicada") manda aplicar o bundle antes, mas não diz o que
  o usuário vê se inverter a ordem.

#### QA8-08 — Imagem antes do bundle, importação ligada

Duas variantes. A **a** não depende de rede; a **b** chama o Banco Central (decisão 1).

a. Proxy morto: recrie o banco `qaf8_order` (`docker exec qaf8-db dropdb -U qaf8 qaf8_order &&
   docker exec qaf8-db createdb -U qaf8 qaf8_order`) e suba `qaf8-api-x` como no QA8-07, mas sem
   `IndexRatesImport__Enabled` e com `-e HTTPS_PROXY=http://127.0.0.1:9`.

b. Sem proxy, mesmo procedimento.

Em ambas: confira no log o `fail:` da importação e o `warn: ... terminou com falha`; `/health` 200;
aplique o bundle; projete um CDB CDI (400 por falta de cotação); `docker restart qaf8-api-x`; na
variante b, espere o log `Importação de cotações concluída` e projete de novo (200).

Esperado: a falha da importação na partida fica no log e a API continua de pé; depois do bundle,
só o reinício faz a importação rodar de novo antes de 6 h (README, seção "Com Docker", vale também
para a imagem).

- BUG: a API cai ou para de responder quando a importação falha; log com a connection string.
- RISCO: a seção "Imagem publicada" do README não repete o aviso de reiniciar depois do bundle,
  que a seção "Com Docker" tem; quem sobe a imagem antes do bundle fica até 6 h sem cotações.

### Migration bundle

Todos com `runbundle ... mcr.microsoft.com/dotnet/runtime-deps:8.0 ./efbundle [args]`. Registre
para cada caso: exit code (`echo $?`), as últimas linhas da saída e
`| grep -c "$PG"` sobre a saída completa.

#### QA8-09 — Execução repetida

```bash
runbundle -e ConnectionStrings__DefaultConnection="$(cs qaf8_bundle)" mcr.microsoft.com/dotnet/runtime-deps:8.0 ./efbundle; echo "exit=$?"
runbundle -e ConnectionStrings__DefaultConnection="$(cs qaf8_bundle)" mcr.microsoft.com/dotnet/runtime-deps:8.0 ./efbundle; echo "exit=$?"
sql qaf8_bundle 'select count(*) from holiday'
sql qaf8_bundle 'select count(*), count(distinct date) from holiday'   # confirme o nome da coluna com \d holiday
sql qaf8_bundle 'select "MigrationId" from "__EFMigrationsHistory"'
```

Esperado: segunda execução com exit 0 e `No migrations were applied. The database is already up
to date.`; 139 feriados e 2 migrations nas duas leituras.

- BUG: segunda execução com exit diferente de 0, feriados duplicados ou migration repetida.

#### QA8-10 — Fontes de conexão

| Caso | Argumentos | Esperado |
|---|---|---|
| a. Variável vazia | `-e ConnectionStrings__DefaultConnection=` | exit 1 com a mensagem das duas fontes (vazio conta como ausente, P5) |
| b. Variável só com espaços | `-e "ConnectionStrings__DefaultConnection=   "` | idem |
| c. Variável e `--connection` para bancos diferentes | variável → `qaf8_bundle2`; `./efbundle --connection "$(cs qaf8_cal)"` | migra só `qaf8_cal` (SPEC-F8, Emenda do P6: `--connection` sobrescreve). Confira as duas tabelas de histórico |
| d. `--connection` vazio com variável válida | variável → `qaf8_bundle2`; `./efbundle --connection ""` | registre: erro claro, ou usa a variável |
| e. Ajuda | `./efbundle --help` | lista as opções; nada de conexão |

Depois do caso c, `qaf8_cal` fica com o esquema completo (é usado no QA8-18) e `qaf8_bundle2`
deve continuar vazio.

- BUG: caso c migrando o banco da variável (a spec diz o contrário); mensagem que não cita as
  duas fontes.
- RISCO: caso d com comportamento silencioso.

#### QA8-11 — Falhas de conexão

| Caso | Connection string na variável |
|---|---|
| a. Senha errada | `Host=qaf8-db;Port=5432;Database=qaf8_bundle2;Username=qaf8;Password=Errada$PG` |
| b. Host inexistente | `Host=qaf8-nao-existe;Port=5432;Database=x;Username=qaf8;Password=$PG` |
| c. Porta fechada | `Host=qaf8-db;Port=5999;Database=x;Username=qaf8;Password=$PG` |
| d. Connection string malformada | `isto nao e uma connection string` |
| e. Senha errada com `--verbose` | caso a, mais `./efbundle --verbose` |

Esperado: exit diferente de 0, mensagem que deixe claro o motivo (autenticação, host, porta,
formato) e **nenhuma ocorrência da senha** na saída (`grep -c "$PG"` = 0, inclusive no
`--verbose`). Anote o tempo até falhar nos casos b e c.

- BUG: senha na saída de qualquer caso; exit 0 numa falha; exceção sem mensagem útil
  (por exemplo, só a pilha).
- RISCO: tempo de falha longo (acima de 30 s) sem aviso.

#### QA8-12 — Banco inexistente e migration alvo

```bash
runbundle -e ConnectionStrings__DefaultConnection="$(cs qaf8_nao_existe)" mcr.microsoft.com/dotnet/runtime-deps:8.0 ./efbundle; echo "exit=$?"
docker exec qaf8-db psql -U qaf8 -lqt | cut -d'|' -f1 | grep qaf8_nao_existe
# migration alvo: só a primeira, depois o resto
docker exec qaf8-db createdb -U qaf8 qaf8_step
runbundle -e ConnectionStrings__DefaultConnection="$(cs qaf8_step)" mcr.microsoft.com/dotnet/runtime-deps:8.0 ./efbundle 20260818070004_InitialCreate
sql qaf8_step 'select count(*) from "__EFMigrationsHistory"'      # 1
runbundle -e ConnectionStrings__DefaultConnection="$(cs qaf8_step)" mcr.microsoft.com/dotnet/runtime-deps:8.0 ./efbundle
sql qaf8_step 'select count(*) from "__EFMigrationsHistory"; select count(*) from holiday'   # 2 e 139
# reversão total (só neste banco descartável)
runbundle -e ConnectionStrings__DefaultConnection="$(cs qaf8_step)" mcr.microsoft.com/dotnet/runtime-deps:8.0 ./efbundle 0
sql qaf8_step '\dt'
```

Esperado: o banco inexistente é criado pelo bundle ou o bundle falha com mensagem clara; registre
qual. A migração em dois passos chega ao mesmo esquema. `./efbundle 0` desfaz tudo.

- RISCO: o bundle cria um banco novo quando o nome está errado (erro de digitação vira banco vazio
  e a API aponta para o certo); `./efbundle 0` apaga as tabelas sem confirmação, e o README não
  avisa que o bundle aceita migration alvo. Nenhum dos dois contraria a spec; são para decisão.

### Fábrica de design-time no `dotnet ef` local

Rode da raiz do repositório, com o SDK do host. Estes cenários compilam o projeto (escrevem em
`bin/` e `obj/`, ignorados pelo git). **Não rode `database update` sem a variável ou com ela
vazia**: o destino seria o banco do desenvolvedor.

#### QA8-13 — Qual conexão o `dotnet ef` usa

```bash
cd "$REPO"
EF="--project src/FixedIncome.Infrastructure --startup-project src/FixedIncome.Api"
dotnet ef dbcontext info $EF | grep -iE 'data source|database name'                      # sem variável: localhost:5432 / fixedincome
ConnectionStrings__DefaultConnection="Host=localhost;Port=25432;Database=qaf8_prod;Username=qaf8;Password=$PG" \
  dotnet ef dbcontext info $EF | grep -iE 'data source|database name'                    # 25432 / qaf8_prod
ConnectionStrings__DefaultConnection= dotnet ef dbcontext info $EF | grep -iE 'data source|database name'      # vazia: volta ao arquivo
ConnectionStrings__DefaultConnection="   " dotnet ef dbcontext info $EF | grep -iE 'data source|database name' # espaços: idem
dotnet ef migrations list $EF --no-connect
ConnectionStrings__DefaultConnection="Host=localhost;Port=25432;Database=qaf8_prod;Username=qaf8;Password=$PG" \
  dotnet ef migrations list $EF                                                          # 2 aplicadas
(cd src && dotnet ef dbcontext info --project FixedIncome.Infrastructure --startup-project FixedIncome.Api | grep -iE 'data source')
(cd src/FixedIncome.Infrastructure && dotnet ef dbcontext info --startup-project ../FixedIncome.Api | grep -iE 'data source')
```

Em PowerShell, repita o caso "com variável" (`$env:ConnectionStrings__DefaultConnection = '...'`,
depois `Remove-Item Env:ConnectionStrings__DefaultConnection`). Observe que `$env:X = ''` **remove**
a variável no PowerShell 5.1: o caso "vazia" só existe no bash.

Esperado (SPEC-F8, P5): variável definida e não vazia tem precedência; vazia ou só espaços cai no
`appsettings.Development.json`; os comandos funcionam da raiz e de subpastas.

- BUG: algum diretório de trabalho em que o `dotnet ef` local deixou de funcionar e funcionava
  antes da F8; variável vazia gerando erro em vez de cair no arquivo.
- RISCO: a senha da variável aparece na saída de algum comando.

#### QA8-14 — `database update` aponta para o banco certo

```bash
ConnectionStrings__DefaultConnection="Host=localhost;Port=25432;Database=qaf8_efupd;Username=qaf8;Password=$PG" \
  dotnet ef database update $EF
sql qaf8_efupd 'select count(*) from "__EFMigrationsHistory"; select count(*) from holiday'   # 2 e 139
docker exec qaf8-db dropdb -U qaf8 qaf8_efupd && docker exec qaf8-db createdb -U qaf8 qaf8_efupd
dotnet ef database update $EF --connection "Host=localhost;Port=25432;Database=qaf8_efupd;Username=qaf8;Password=$PG"
sql qaf8_efupd 'select count(*) from "__EFMigrationsHistory"'                                 # 2
```

No segundo comando não há variável, então a fábrica lê o arquivo (banco do desenvolvedor), e o
`--connection` precisa vencer. Se o banco do desenvolvedor fosse o alvo, a saída seria
`No migrations were applied` (ele já está atualizado) e `qaf8_efupd` ficaria vazio. Confira no fim
que `antes-devdb.txt` não mudou.

- BUG: `--connection` ignorado no `dotnet ef` local.
- RISCO: a seção "Localmente, com o SDK" do README não avisa que a variável
  `ConnectionStrings__DefaultConnection`, se estiver definida no terminal (por exemplo, sobrando
  de um teste da imagem), passa a ter precedência sobre o `appsettings.Development.json`, e o
  `database update` vai para outro banco sem aviso.

### Importação do CDI no fluxo real

QA8-15 a QA8-17 chamam o Banco Central (decisão 1). Todos usam `qaf8-api-bc` contra
`qaf8_import`. Cada reinício faz duas chamadas (séries 12 e 433), mais as repetições se o SGS
falhar. A janela pedida aparece no log do `HttpClient`:

```bash
apibc() { docker rm -f qaf8-api-bc >/dev/null 2>&1; docker run -d --name qaf8-api-bc --network qaf8-net \
  -p 28082:8080 -e ConnectionStrings__DefaultConnection="$(cs qaf8_import)" qaf8/renda-fixa-api:wt; }
janela() { docker logs qaf8-api-bc 2>&1 | grep -E 'Sending HTTP request GET .*sgs\.(12|433)|concluída|descartad|Falha|terminou com falha'; }
cdi() { sql qaf8_import "select count(*), min(reference_date), max(reference_date) from index_rate where index_type='Cdi'"; }
lacunas() { sql qaf8_import "select d::date from generate_series('2020-01-02'::date,(select max(reference_date) from index_rate where index_type='Cdi'),'1 day') d where extract(isodow from d)<6 and d::date not in (select date from holiday) except select reference_date from index_rate where index_type='Cdi' order by 1"; }
```

(Confirme o nome da coluna de data de `holiday` com `\d holiday` antes de usar `lacunas`.)

#### QA8-15 — Carga inicial e execução seguinte

```bash
runbundle -e ConnectionStrings__DefaultConnection="$(cs qaf8_import)" mcr.microsoft.com/dotnet/runtime-deps:8.0 ./efbundle
apibc; sleep 40; janela; cdi; lacunas      # anote N0 = contagem de CDI e L0 = lacunas
docker restart qaf8-api-bc; sleep 30; janela
```

Esperado: primeira execução com `dataInicial=01/01/2020`; `lacunas` vazio (ou só datas que o SGS
de fato não publicou: anote cada uma). No reinício, a busca de CDI parte do próximo dia útil após
a última cotação (ESPECIFICACAO, "Importação das cotações") e não repete aviso de descarte.

#### QA8-16 — Bordas de calendário com a regra do próximo dia útil

Para cada linha: apague as cotações de CDI posteriores à data, reinicie, leia a janela pedida e
confira que a série volta inteira.

| Caso | Última cotação deixada | `dataInicial` esperada | Por quê |
|---|---|---|---|
| a. Carnaval 2026 | 2026-02-13 (sexta) | 18/02/2026 | 16 e 17/02 são feriados ANBIMA; 14 e 15 são fim de semana |
| b. Natal 2025 | 2025-12-24 (quarta) | 26/12/2025 | 25/12 é feriado |
| c. Réveillon | 2025-12-31 (quarta) | 02/01/2026 | 01/01 é feriado |
| d. Dia útil comum | 2026-11-05 (quinta) | 06/11/2026 | caso de controle |
| e. Fronteira `from == hoje` | o dia útil anterior a hoje | hoje (`dataInicial` = `dataFinal`) | só se a execução cair num dia útil |

```bash
sql qaf8_import "delete from index_rate where index_type='Cdi' and reference_date > '2026-02-13'"
docker restart qaf8-api-bc; sleep 40; janela; cdi; lacunas
```

Esperado: a `dataInicial` da tabela; depois do reinício a contagem volta a N0 e `lacunas` volta a
L0. Nenhum dia útil fica sem cotação por causa do salto.

- BUG: `dataInicial` diferente da tabela; lacuna nova depois da reimportação; aviso de descarte
  repetido entre dois reinícios sem cotação nova.
- RISCO: `dataFinal` com a data UTC em vez da de Brasília, se a execução cair entre 21h e 24h
  (é o R-01, pendência da F7; só registrar).

#### QA8-17 — Hoje é fim de semana ou feriado

Só executável se a sessão cair num sábado, domingo ou feriado nacional (próximos: 14 e 15/11/2026,
sábado e domingo; 20/11/2026, sexta, Consciência Negra). Deixe a última cotação no último dia
útil antes de hoje e reinicie.

Esperado (SPEC-F8, "Numa sexta importada, a execução de sábado e a de domingo não chamam o
provedor"): nenhuma linha `Sending HTTP request ... sgs.12` no log; a série 433 é consultada ou
não conforme o IPCA do mês.

Fora dessas datas: `NÃO EXECUTADO` (o relógio do contêiner não é controlável sem ferramenta
nova); a regra tem o teste `Cdi_importado_na_sexta_nao_chama_o_provedor_no_domingo`.

#### QA8-18 — Calendário vazio no banco

Sem Banco Central: proxy morto. Usa `qaf8_cal` (migrado no QA8-10c).

```bash
sql qaf8_cal 'delete from holiday'
docker run -d --name qaf8-api-x --network qaf8-net -p 28081:8080 -e HTTPS_PROXY=http://127.0.0.1:9 \
  -e ConnectionStrings__DefaultConnection="$(cs qaf8_cal)" qaf8/renda-fixa-api:wt
sleep 40; docker logs qaf8-api-x 2>&1 | grep -E 'sgs\.(12|433)|Falha|terminou|calend'
curl -s -w ' %{http_code}\n' $X/health; curl -s -w ' %{http_code}\n' $X/api/assets
# PreFixed com o calendário vazio
curl -s -X POST -H 'Content-Type: application/json' \
  -d '{"name":"QA-F8 cal vazio","issuer":"QA","assetType":"Cdb","indexType":"PreFixed","rate":10,"issueDate":"2026-01-01","maturityDate":"2027-01-01"}' $X/api/assets
curl -s -X POST -H 'Content-Type: application/json' -d '{"assetId":"<id>","investedAmount":1000,"applicationDate":"2026-09-01"}' $X/api/positions
curl -s "$X/api/positions/<id>/projection?referenceDate=2026-10-01"; echo
curl -s "$X/api/portfolio/summary"; echo
docker rm -f qaf8-api-x
```

Esperado (SPEC-F8, "O calendário passa a ser carregado antes da busca"): a importação de CDI falha
**antes** de qualquer requisição à série 12 (nenhum `Sending HTTP request ... sgs.12`), com a
falha registrada no log; a série 433 é tentada (e falha no proxy morto); a API continua de pé.
O aporte e a projeção devolvem 400 com mensagem que diga que o calendário não cobre a data (RN-08:
data fora do calendário é erro, nunca suposição).

- BUG: 500 em qualquer endpoint; a API cai; a busca da série 12 acontece com o calendário vazio;
  PreFixed calculando como se não houvesse feriado.
- RISCO: a mensagem não indica que o calendário está vazio (o usuário só vê "fora do período").

#### QA8-19 — JSON truncado do SGS ao vivo (opcional)

O endereço do SGS é fixo em HTTPS (`https://api.bcb.gov.br/`), então um stub HTTP local não o
substitui sem alterar código. A única forma caixa-preta é um proxy que intercepta TLS. Só com a
decisão 3; caso contrário, `NÃO EXECUTADO`, com a referência aos testes
`Resposta_com_json_truncado_do_sgs_e_tratada_como_falha_transitoria` e
`Json_truncado_em_todas_as_tentativas_propaga_a_falha_depois_de_esgotar_o_retry`, que a validação
provou falharem sem a correção.

Esboço: contêiner `mitmproxy/mitmproxy` (versão fixada) em `qaf8-net` com o addon abaixo; o
certificado da CA do mitmproxy montado na API e apontado por `SSL_CERT_FILE`; a API com
`HTTPS_PROXY=http://qaf8-mitm:8080`. Antes, apague as cotações de CDI posteriores a 2026-10-01 para
a janela ter dados.

```python
# trunca.py — corta ao meio as N primeiras respostas da série 12
from mitmproxy import http
N = 1          # 1: espera retry e sucesso; 99: espera 4 tentativas e falha registrada
vistos = 0
def response(flow: http.HTTPFlow):
    global vistos
    if "bcdata.sgs.12" in flow.request.pretty_url and vistos < N:
        vistos += 1
        flow.response.text = flow.response.text[: len(flow.response.text) // 2]
```

Esperado (ESPECIFICACAO, "JSON cortado é falha transitória e entra no retry"): com `N = 1`, um
`OnRetry` no log e a importação concluída; com `N = 99`, 4 requisições à série 12, `Falha ao
importar as cotações de Cdi`, IPCA importado normalmente, API de pé.

### Workflows

#### QA8-20 — Leitura adversarial do `release.yml` e do `ci.yml`

Sem execução: leitura dos dois arquivos e da documentação do GitHub Actions. Para cada item,
registre `OK`, `RISCO` ou `BUG`, com a linha.

| # | Pergunta | O que conferir |
|---|---|---|
| a | Re-run de uma execução antiga, depois de uma mais nova já ter movido `latest` | O passo `latest` move a tag para o SHA antigo? Nada no workflow compara o SHA com o topo da `main`. Uma reexecução manual faz a versão aprovada andar para trás |
| b | Dois pushes seguidos (A em andamento, B e C na fila) | `concurrency` sem `cancel-in-progress`: B é descartado, C roda. A tag de B nunca existe. O CI standalone (`ci.yml`) roda para os três. Coerente com a SPEC-F8? |
| c | Push que só altera `docs/` ou `README.md` | Não há `paths-ignore`: CI duas vezes, imagem nova, bundle de ~119 MB com 30 dias de retenção (observação 1 da validação). Custo por commit de documentação |
| d | PR de fork | `pull_request` aciona só o `ci.yml`, com token de leitura; o `release.yml` não tem gatilho de PR. Confirme que nada do `release.yml` roda em PR |
| e | Credencial persistida pelo `actions/checkout@v4` | Sem `persist-credentials: false`, o `GITHUB_TOKEN` do job `release` (com `packages: write`) fica em `.git/config` durante `dotnet tool restore`, a geração do bundle (compila o projeto, executa targets de pacotes NuGet) e o `docker build`. E o `docker login` deixa a credencial em `~/.docker/config.json` para os passos seguintes. Avaliar como RISCO de cadeia de suprimentos |
| f | Interpolação de expressões em `run:` | `${{ github.actor }}`, `${{ github.sha }}` e `${{ github.repository }}` entram direto no script. Algum é controlável por terceiro num push na `main`? `github.actor` de bot tem `[bot]` (caractere de glob, sem aspas) |
| g | Laço de espera do smoke | 60 iterações de até 2 s (`--max-time 2`) mais 1 s: até ~180 s, mas a mensagem diz "60s" |
| h | Artefato `coverage-report` do `ci.yml` sem `overwrite` | Em "Re-run failed jobs" de uma execução do `release.yml`, o upload com o mesmo nome conflita? (o do bundle tem `overwrite: true`). Só a execução real confirma; registre como pergunta aberta |
| i | Sem `workflow_dispatch` | Como republicar `latest` depois de um smoke que falhou por instabilidade? Só re-run (ver a) ou commit novo |
| j | SDK flutuante | `setup-dotnet` com `8.0.x` gera o bundle, a imagem usa `sdk:8.0` e `aspnet:8.0` sem digest: duas execuções do mesmo SHA podem gerar imagens diferentes |
| k | `ci` chamado sem `secrets: inherit` e com `contents: read` | O CI chamado precisa de algo além disso? (upload de artefato usa o token do runtime, não `packages`) |
| l | Smoke na porta 8080 com `--network host` | Se a porta estiver ocupada no runner, o `curl` acertaria outro processo? (baixo; registrar) |

Opcional, se a decisão 4 autorizar: rodar uma ferramenta de auditoria de workflows
(`zizmor`, versão fixada, em contêiner) sobre `.github/workflows/` e anexar a saída.

- BUG: algo que permita mover `latest` sem smoke verde, publicar a partir de PR ou expor o token.
- RISCO: os demais (a, c, e, i, j são os candidatos).

### Documentação

#### QA8-21 — README seguido do zero

Siga as seções "Imagem publicada" e "Localmente, com o SDK" como um usuário, copiando cada bloco
literalmente, uma vez em **Git Bash** e uma vez em **Windows PowerShell 5.1**, contra os recursos
`qaf8` (troque só host, porta, banco e senha; anote cada troca). Registre, por comando, se roda
sem edição.

| Bloco do README | O que observar |
|---|---|
| `chmod +x efbundle` e `ConnectionStrings__DefaultConnection="..." ./efbundle` | Sintaxe só de bash, binário só Linux. Um usuário Windows sem WSL tem caminho documentado? (a validação usou `runtime-deps`, que o README não menciona) |
| `dotnet ef migrations bundle ... --output efbundle --force` | Gera um arquivo de ~119 MB na raiz. Rode `git check-ignore -v efbundle` e confira o `.dockerignore`: se o arquivo não for ignorado, ele aparece no `git status` e entra no contexto do `docker build`/`docker compose up --build`. **Não gere o arquivo na raiz**: confirme só pelos dois arquivos de ignore |
| `docker pull ghcr.io/matheuscardosolazzarini/renda-fixa-api:latest` | Antes da primeira execução do pipeline, e com o pacote privado: qual erro o usuário vê? O README avisa (decisão 5) |
| `docker run -d --name renda-fixa-api-ghcr -p 8080:8080 \ ...` | Em PowerShell, a continuação com `\` quebra (a seção "Com Docker" avisa "numa só linha"; esta não). Use a porta 28080 e a imagem `qaf8/renda-fixa-api:wt` no lugar |
| A mesma linha, contra o banco | Sem `IndexRatesImport__Enabled`, a imagem chama o Banco Central ao subir. O README da seção "Imagem publicada" diz isso? |
| "Se o serviço `api` do Compose estiver rodando ... `docker compose stop api`" | **Não execute** (pararia o contêiner do desenvolvedor). Só confira a afirmação |
| Bloco Linux (`--network host` e `localhost`) | Coerente com o smoke do pipeline. Leitura |
| "Localmente, com o SDK" | Falta o aviso do QA8-14 sobre a variável? |

- BUG: comando documentado que não funciona copiado e colado no shell que o README indica.
- RISCO: comando que só funciona em um dos dois shells sem o README dizer.

#### QA8-22 — Coerência entre README, ESPECIFICACAO, SPEC-F8 e ROADMAP

Procure contradição de nomes, números e comandos. Pontos de partida, a confirmar no texto vigente
na hora da execução:

- Contagem de testes: README diz 558; a validação mediu 578 (pendência do orquestrador; se já
  tiver sido atualizado, OK).
- Tamanho do bundle: SPEC-F8 diz "dezenas de MB"; a validação mediu 119 MB.
- ESPECIFICACAO, "F1 — Fundação": "health check respondendo", enquanto o parágrafo da F8 diz que o
  health check só foi entregue na F8.
- `IndexRatesImport:Enabled` (README, sintaxe de `appsettings`) contra `IndexRatesImport__Enabled`
  (pipeline, variável de ambiente): o README explica a tradução para quem roda a imagem?
- Porta: `.http` em 5124 (com a requisição nova de `/health`), README e imagem em 8080 (R-03 da F7).
- Lista de endpoints: o P8 pede `/health` "na lista de endpoints" do README; a validação notou que
  o README não tem lista (observação 4). Coerente?
- Comando do bundle: o do README, o do `release.yml` e o da SPEC-F8 diferem só no `--output`?
- Senha e nomes do smoke (`smoke`) na SPEC-F8, no `release.yml` e no README.
- ESPECIFICACAO, decisões técnicas: "graphify, com o grafo local em `graphify-out/`, fora do
  versionamento", contra o `CLAUDE.md` ("A committed graphify-out/graph.json"). Não é da F8; só
  registrar se confirmado.

- BUG (baixa): número ou comando contraditório que leve o usuário a erro.

## O que pode rodar em paralelo

1. **Sequencial no início:** Preparação, passos 0 a 5 (a imagem e o bundle são insumos de quase
   tudo). Não rode `docker build` e a geração do bundle ao mesmo tempo que QA8-13/14: todos
   compilam o repositório e disputam `obj/`.
2. **Bloco A, em paralelo entre si e com os blocos B, C e D:** QA8-01, QA8-02 e QA8-03
   (`qaf8-api-prod`); QA8-04 caso d por último dentro do bloco, porque para essa API.
3. **Bloco B, um de cada vez (usam `qaf8-api-x` e a porta 28081):** QA8-04 (a a c), QA8-05,
   QA8-06, QA8-07, QA8-08a, QA8-18. QA8-05 e QA8-06 usam `qaf8-dbx` e não afetam `qaf8-db`.
4. **Bloco C, bundle, em paralelo com A e D:** QA8-09 a QA8-12 (bancos lógicos próprios em
   `qaf8-db`). QA8-10c precisa vir antes de QA8-18.
5. **Bloco D, Banco Central, sequencial (`qaf8-api-bc`):** QA8-08b, QA8-15, QA8-16, QA8-17,
   QA8-19. QA8-15 antes de QA8-16.
6. **Bloco E, sem Docker, a qualquer momento:** QA8-13 e QA8-14 (depois da preparação, fora do
   horário de build), QA8-20, QA8-21 (as linhas de leitura), QA8-22.

## Fora deste roteiro

Só uma execução real do GitHub Actions comprova (itens abertos da `VALIDACAO-F8.md`):

- push da tag do SHA no GHCR e o `docker pull` real do registro;
- o artefato `efbundle-<sha>` anexado e o bundle baixado como zip, com o bit de execução perdido;
- o smoke no runner (`--network host`, service container) e a `latest` com o mesmo digest do SHA;
- um smoke que falha sem mover `latest`;
- os logs do Actions sem o token;
- o vínculo do pacote ao repositório pelo rótulo `source` e a visibilidade privada inicial;
- o comportamento real de re-run e da fila de `concurrency` (QA8-20 a, b, h só por leitura aqui).

Também fora: o R-01 ao vivo (pendência da F7, fora do escopo da F8; só bônus no QA8-03),
Testcontainers e `/health/ready` (F9 e F10), e qualquer teste de carga.

## Limpeza ao fim

```bash
docker rm -f qaf8-api-prod qaf8-api-x qaf8-api-bc qaf8-mitm qaf8-dbx qaf8-db 2>/dev/null
docker network rm qaf8-net
docker image rm qaf8/renda-fixa-api:wt
# só as imagens que não estavam em antes-images.txt:
docker images --format '{{.Repository}}:{{.Tag}}' | sort | comm -13 "$QA/antes-images.txt" - 
cd "$REPO" && git status --short | diff - "$QA/git-status.txt" && echo "working tree igual"
docker ps --format '{{.Names}} {{.Status}} {{.Ports}}' | grep fixedincome
docker exec fixedincome-db psql -U fixedincome -d fixedincome -tAc \
  "select (select count(*) from holiday), (select count(*) from index_rate), (select count(*) from \"__EFMigrationsHistory\")" \
  | diff - "$QA/antes-devdb.txt" && echo "banco do desenvolvedor igual"
```

Todos os dados `QA-` ficam nos bancos `qaf8_*`, que somem com o contêiner `qaf8-db`. O relatório
lista os ids criados mesmo assim. A contagem de `index_rate` do banco do desenvolvedor pode mudar
sozinha pelo job dele; se mudar, confira que só cresceu.

## Decisões antes de executar

1. **Chamadas ao Banco Central.** QA8-08b e QA8-15 a QA8-17 fazem cerca de 10 a 15 reinícios da
   API com importação ligada, duas séries por reinício (mais repetições se o SGS falhar). Autoriza?
   Sem autorização, esses cenários ficam `NÃO EXECUTADO` e sobram QA8-08a e QA8-18 (proxy morto).
2. **Docker Desktop aberto** e autorização para baixar `postgres:16` e
   `mcr.microsoft.com/dotnet/runtime-deps:8.0` (a validação removeu as duas), removidas no fim.
3. **QA8-19 com mitmproxy**: baixa a imagem `mitmproxy/mitmproxy`, intercepta o TLS do Banco
   Central só no contêiner de teste. Executar ou deixar `NÃO EXECUTADO`?
4. **Auditoria automática dos workflows** (`zizmor` em contêiner) no QA8-20: executar ou ficar só
   na leitura?
5. **`docker pull` do GHCR** no QA8-21 (só leitura, sem login): autoriza a chamada?
6. **Leitura do banco do desenvolvedor**: a foto de antes e depois (três `count(*)` em
   `fixedincome-db`) é só leitura. Autoriza, ou o QA confia só no `docker ps`?
7. **Janela de execução**: rodar entre 21h e 24h de Brasília dá de brinde o R-01 ao vivo
   (QA8-03); rodar num sábado, domingo ou em 20/11 permite o QA8-17. Há preferência?
8. **Momento**: executar sobre o working tree atual (fase não commitada, como a validação) ou
   depois do commit, para o relatório citar um SHA que exista?
9. **Classificação de casos sem regra na spec**, para o QA não decidir sozinho: `/health/` e
   `/HEALTH`; 500 contra 503 com o banco fora; a imagem subindo "saudável" sem connection string;
   o bundle criando banco com nome errado; `./efbundle 0` sem confirmação; re-run antigo movendo
   `latest` para trás. Contam como `BUG` ou `RISCO`?
