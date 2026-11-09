# QA exploratório — F8: Entrega contínua da imagem e pendências da F7

## Cabeçalho

| Item | Valor |
|---|---|
| Data | 2026-11-09 (segunda-feira), das 04h20 às 05h UTC aproximadamente (01h20 às 02h Brasília) |
| Commit-base | `1799fceea12fe92c06e947ef2f6e78ad7834b0b9` (branch `f8-entrega-continua`). **A fase não está commitada**: o QA rodou sobre o working tree |
| `git status --short` no início | ` M .dockerignore`, ` M .github/workflows/ci.yml`, ` M README.md`, ` M docs/ESPECIFICACAO.md`, ` M src/FixedIncome.Api/FixedIncome.Api.http`, ` M src/FixedIncome.Api/Program.cs`, ` M src/FixedIncome.Application/UseCases/ImportIndexRatesUseCase.cs`, ` M src/FixedIncome.Infrastructure/Context/FixedIncomeDbContextFactory.cs`, ` M src/FixedIncome.Infrastructure/Providers/SgsNonJsonResponseHandler.cs`, ` M tests/FixedIncome.Application.Tests/UseCases/ImportIndexRatesUseCaseTests.cs`, ` M tests/FixedIncome.Infrastructure.Tests/Providers/SgsNonJsonResponseHandlerTests.cs`, `?? .github/workflows/release.yml`, `?? docs/QA-F8-ROTEIRO.md`, `?? docs/SPEC-F8-EXECUCAO.md`, `?? docs/VALIDACAO-F8.md`, `?? tests/FixedIncome.Api.Tests/HealthEndpointTests.cs`, `?? tests/FixedIncome.Infrastructure.Tests/Context/` |
| Ambiente | Docker Desktop 29.7.2. Imagem `qaf8/renda-fixa-api:wt` construída com `docker build` do working tree (sem `ASPNETCORE_ENVIRONMENT`, logo Production; log: `Hosting environment: Production`; usuário `appuser`, porta 8080, 96 MB). Bundle gerado em `scratchpad/qaf8/efbundle` com o comando do roteiro: 125.026.675 bytes (119 MiB). PostgreSQL 16 (`qaf8-db`, 25432 e `qaf8-dbx`, 25433), rede `qaf8-net`, APIs `qaf8-api-prod` (28080), `qaf8-api-x` (28081) e `qaf8-api-bc` (28082). O bundle foi executado no contêiner `mcr.microsoft.com/dotnet/runtime-deps:8.0` |
| Método | Caixa-preta com `curl` contra os contêineres `qaf8-*`. O código de `src/` só foi aberto depois de reproduzir o QA8-16e (para explicar o `dataInicial` observado). Os workflows, o README, a ESPECIFICACAO e a SPEC-F8 foram lidos (são o objeto do QA8-20 a QA8-22) |
| Decisões do Matheus aplicadas | 1: Banco Central autorizado; 2: imagens baixadas e removidas no fim; 3: QA8-19 `NÃO EXECUTADO`; 4: `zizmor` autorizado; 5: sem `docker pull` do GHCR do projeto; 6: foto do banco do desenvolvedor; 8: working tree; 9: os casos sem regra na spec contam como `RISCO` |
| Banco Central | Respondeu em todas as chamadas (cinco partidas da API com importação ligada, mais três `curl` diretos). Nenhuma falha do SGS no período |
| Banco do desenvolvedor | `select count(*) from holiday, index_rate, __EFMigrationsHistory` antes e depois: `139 / 1795 / 2` nas duas leituras. Os contêineres `fixedincome-api` e `fixedincome-db` seguiram de pé, sem reinício |

Veredito de severidade fica com o orquestrador e o Matheus. Nos achados, "sugestão" é só uma indicação.

## Imagem em Production

### QA8-01 — `/health` fora do caminho feliz: **OK**

Todas as requisições contra `http://localhost:28080`:

| Requisição | Resposta |
|---|---|
| `GET /health` | 200 `text/plain`, corpo `Healthy`, `Cache-Control: no-store, no-cache` |
| `HEAD /health` (`curl -I`) | 200 `text/plain`, sem corpo |
| `OPTIONS`, `PUT`, `DELETE`, `PATCH`, `TRACE` `/health` | 405 `application/json`, `Allow: GET, HEAD`, corpo `{"statusCode":405,"success":false,"message":"Método HTTP não permitido para este recurso.","data":null}` |
| `POST /health` sem `Content-Type` e com `application/json` | 405 no envelope (não 415) |
| `GET /health?x=1&y=%3Cscript%3E` | 200 `Healthy` (query ignorada) |
| `GET /health` com `Accept: application/json` | 200 `Healthy`, igual |
| `GET /health/ready`, `/health/live`, `/healthz` | 404 no envelope `{"statusCode":404,...,"message":"Recurso não encontrado."}` |
| `GET /health/`, `/HEALTH`, `/Health` | **200 `Healthy`**, idêntico a `/health` |

Observações:

- O roteiro escreve `HEAD` como `curl -X HEAD`, que fica esperando um corpo que nunca vem (travou o laço até o timeout). Usei `curl -I`. É defeito do roteiro, não da API.
- Os casos que a decisão 9 marcou como `RISCO` (`/health/` e `/HEALTH`) não divergiram de `/health`: respondem 200 igual. Não há o risco descrito (orquestrador com barra final não falharia).

### QA8-02 — Contrato de erro em Production: **OK**

| Requisição | Resposta |
|---|---|
| `GET /swagger`, `/swagger/v1/swagger.json`, `/swagger/index.html`, `/openapi/v1.json` | 404 no envelope |
| `GET /api/foo`, `/api`, `/api/`, `/index.html`, `/favicon.ico` | 404 no envelope |
| `PATCH /api/assets` | 405, `Allow: GET, POST`, envelope |
| `POST /api/assets` sem `Content-Type` e com `text/plain` | 415 `"Tipo de conteúdo não suportado. Envie o corpo como application/json."` |
| `POST /api/assets` com `{malformado` e com corpo vazio | 400 `"O corpo da requisição está ausente ou malformado."` |
| `GET /api/assets/nao-e-guid` | 400 `"O valor do campo 'id' é inválido."` |
| `GET /api/assets` | 200, sem `Location` (nenhum 307/308) |

O log traz, na primeira requisição, `warn: HttpsRedirectionMiddleware ... Failed to determine the https port for redirect.` (observação 6 da validação, sem efeito: nenhuma resposta redireciona). Nenhum erro com detalhe de exceção no corpo.

### QA8-03 — Endpoints sem cotações: **OK** (com RISCO R-10)

Títulos `QA-F8 PRE12`, `QA-F8 CDI100`, `QA-F8 IPCA6` (CDB, 2026-01-01 a 2028-01-01), um aporte de R$ 1.000 em 2026-09-01 em cada, importação desligada, `index_rate` vazia.

| Requisição | Resposta |
|---|---|
| Projeção PreFixed 12%, `referenceDate=2026-10-01` | 200, `grossAmount 1009.49`, `taxRate 22.5`, `taxAmount 2.14`, `netAmount 1007.35`, `elapsedDays 30`, `businessDays 21` (confere: 1000 × 1,12^(21/252) = 1009,49) |
| Projeção PreFixed 12%, sem data | 200, `grossAmount 1020.90`, `elapsedDays 69`, `businessDays 46` (confere: 1000 × 1,12^(46/252) = 1020,90; 69 dias corridos de 2026-09-01 até a data de Brasília, 2026-11-09) |
| Projeção CDI 100%, com e sem data | 400 `"Cotação de CDI ausente para 2026-09-01."` (a primeira data sem cotação) |
| Projeção IPCA+6%, com e sem data | 400 `"Não há cotação de IPCA publicada para estimar a projeção."` |
| `GET /api/portfolio/summary`, com e sem `referenceDate` | 400 `"Cotação de CDI ausente para 2026-09-01."` |
| `GET /api/indexes/Cdi?from=2026-09-01&to=2026-10-01` e `Ipca` | 200 `data: []` |

Nenhum 500. A mensagem do resumo (R-10) não diz qual posição falhou nem sugere ligar a importação.

Bônus (não classificado): a execução foi às 04h26 UTC (01h26 em Brasília), as duas datas coincidem (2026-11-09), fora da janela do R-01 ao vivo. O `elapsedDays 69` não distingue UTC de Brasília nesta hora.

### QA8-04 — Configuração da imagem pelo ambiente: **OK**, com RISCO (R-05, R-17)

Com proxy morto (`HTTPS_PROXY=http://127.0.0.1:9`) em todos os casos.

| Caso | Resultado |
|---|---|
| a. Sem connection string | O processo **sai na partida** (`Exited (139)`, reproduzido duas vezes): `Unhandled exception. System.InvalidOperationException: ConnectionStrings:DefaultConnection não configurada. at Program.<Main>$ ... Program.cs:line 16`. `/health` não responde (porta fechada). Não sobe "saudável" |
| b. Senha errada | `/health` 200 (0,01 s); `GET /api/assets` e `POST /api/assets` 500 no envelope genérico `"Ocorreu um erro interno. Tente novamente mais tarde."`; o log traz `28P01: password authentication failed for user "qaf8"` e nenhuma connection string; o processo segue de pé |
| c. `IndexRatesImport__Enabled` | Importação **desligada** (nenhuma linha `Sending HTTP request ... sgs`, nenhuma falha) com `false`, `False`, `FALSE`, `0`, `no`, `off`, `1`, `yes` e vazio. **Ligada** (chamou o SGS, falhou no proxy morto: `terminou com falha`) com a variável ausente e com `true`. Nenhum aviso no log em nenhum dos casos |
| d. Parada | `docker stop qaf8-api-prod` em 0,77 s, `ExitCode 0`; `docker start` e `/health` 200 em seguida |

Vazamento: `grep -c QaF8Senha123` e `grep -ci 'password='` nos logs: 0 em todos os casos (também no log de 410 linhas de `qaf8-api-prod`).

- R-05: qualquer valor diferente de `true` (e da ausência) desliga a importação, silenciosamente. Quem escrever `=1` ou `=yes` para "ligar" fica sem importação e sem aviso.
- R-17: no caso a, o exit code é 139 (convencionalmente SIGSEGV), e não 134 ou 1; a mensagem é clara, mas vem junto da pilha. Um orquestrador que distinga códigos de saída verá "falha de memória". Observação, sem classificação de bug.

### Banco fora do ar

#### QA8-05 — Banco parado: **OK**, com RISCO (R-08)

`qaf8-dbx` migrado pelo bundle, API em `qaf8-api-x`; antes: `GET /api/assets` 200. Depois do `docker stop qaf8-dbx`:

| Requisição | Resposta |
|---|---|
| `GET /health` | 200 `Healthy` em 0,014 s |
| `GET /api/assets` | 500 em 0,11 s |
| `GET /api/positions`, `/api/portfolio/summary`, `/api/indexes/Cdi?...`, `POST /api/assets` | 500 em 3,3 a 4,0 s |

Todos os 500 no envelope genérico, sem pilha, host ou senha. Log: 5 `fail:` do `ExceptionHandlingMiddleware`, mais os do EF Core; `grep -c QaF8Senha123` = 0. Depois do `docker start qaf8-dbx`, `GET /api/assets` volta a 200 em 0,02 s **sem reiniciar a API**; o contêiner seguiu `Up`.

R-08: indisponibilidade de dependência vira 500, não 503 (decisão 9: fica com o Matheus).

#### QA8-06 — Banco congelado (`docker pause`): **OK**, com RISCO (R-09)

| Medida | Resultado |
|---|---|
| `/health` com o banco congelado | 200 em 0,006 s |
| `/health` enquanto uma requisição de dados estava presa | 200 em 0,006 s |
| 1ª requisição de dados (`GET /api/assets`, conexão do pool) | 500 envelope genérico depois de **45,05 s** |
| 2ª requisição de dados (`GET /api/positions`, conexão nova) | 500 depois de **15,06 s** |
| Depois do `docker unpause` | `GET /api/assets` 200 em 0,04 s, sem reiniciar |

Nenhuma senha no log. R-09: a primeira requisição espera 45 s (acima dos 30 s do roteiro) antes de falhar.

### Ordem de subida

#### QA8-07 — Imagem antes do bundle, importação desligada: **OK**

Banco `qaf8_order` vazio: `/health` 200; `GET /api/assets`, `POST /api/assets`, `/api/portfolio/summary` e `/api/indexes/Cdi` respondem 500 no envelope genérico (o log traz `42P01: relation "fixed_income_asset" does not exist`, o corpo não cita tabela nem SQL). Depois de `./efbundle`, `GET /api/assets` volta a 200 **sem reiniciar a API**. `grep -c QaF8Senha123` no log: 0. O README não descreve o que o usuário vê se inverter a ordem (R-04).

#### QA8-08 — Imagem antes do bundle, importação ligada: **OK**, com RISCO (R-04)

- **a (proxy morto):** `fail: ImportIndexRatesUseCase ... 42P01: relation "index_rate" does not exist` e `Importação de cotações terminou com falha: Falha ao importar as cotações de Cdi e Ipca.`; `/health` 200. Depois do bundle: aporte em CDI criado e projeção 400 `"Cotação de CDI ausente para 2026-09-01."`. Após `docker restart`, a importação tenta de novo (o proxy morto faz falhar de novo, como esperado); API de pé; connection string ausente do log.
- **b (Banco Central):** mesma falha na partida (`42P01`), `/health` 200. Depois do bundle: projeção CDI 400. Após `docker restart qaf8-api-x`: log `Importação de cotações concluída: 1718 de CDI, 0 de CDI descartadas e 81 de IPCA.`; a mesma projeção (aporte de R$ 1.000 em 2026-09-01, `referenceDate=2026-10-01`) passa a 200: `grossAmount 1010.82`, `netAmount 1008.39`, `businessDays 21`.

R-04: a seção "Imagem publicada" do README não repete o aviso de reiniciar depois do bundle (a seção "Com Docker" tem). Quem sobe a imagem antes do bundle fica até 6 h sem cotações.

## Migration bundle

### QA8-09 — Execução repetida: **OK**

Banco `qaf8_bundle`: 1ª execução `Applying migration '20260818070004_InitialCreate'` e `'20261030222641_AddHolidaysAndIndexRates'`, exit 0. 2ª execução: exit 0, `No migrations were applied. The database is already up to date.` Depois: `count(*) from holiday` = 139, `count(distinct date)` = 139, 2 migrations no histórico. Saída sem a senha.

### QA8-10 — Fontes de conexão: **OK**, com RISCO (R-13)

| Caso | Resultado |
|---|---|
| a. Variável vazia | exit 1: `Unable to create a 'DbContext' ... 'Connection string não encontrada. Defina a variável de ambiente ConnectionStrings__DefaultConnection ou crie o arquivo appsettings.Development.json em /FixedIncome.Api/appsettings.Development.json.'` |
| b. Só espaços | idem |
| (extra) Variável ausente | exit 1; mesma mensagem, mais a linha `ConnectionStrings:DefaultConnection não configurada` |
| c. Variável → `qaf8_bundle2`, `--connection` → `qaf8_cal` | exit 0; **migrou só `qaf8_cal`** (2 migrations no histórico); `qaf8_bundle2` ficou com 0 tabelas. Cumpre a Emenda do P6 |
| d. `--connection ""` com variável válida | exit 1 com pilha: `System.ArgumentException: Host can't be null at Npgsql.NpgsqlConnectionStringBuilder.PostProcessAndValidate() ...` seguida de `Host can't be null`. Não usa a variável, não é silencioso, mas a mensagem não diz o motivo (R-13) |
| e. `--help` | lista `<MIGRATION>`, `--connection`, `--version`, `-h`, `-v`, `--no-color`, `--prefix-output`; nada de conexão ou senha |

As mensagens de a e b citam as duas fontes (variável e arquivo). O caminho citado é o do contêiner e a sugestão de criar `appsettings.Development.json` não serve a quem executa o bundle; é ruído, não erro.

### QA8-11 — Falhas de conexão: **OK**, com RISCO (R-13)

| Caso | Exit | Tempo | Primeira linha da saída | Senha na saída |
|---|---|---|---|---|
| a. Senha errada | 1 | 4 s | `Npgsql.PostgresException: 28P01: password authentication failed for user "qaf8"` | 0 |
| b. Host inexistente | 1 | 7 s | `SocketException ... Name or service not known` | 0 |
| c. Porta fechada | 1 | 4 s | `NpgsqlException: Failed to connect to 172.19.0.2:5999 ... Connection refused` | 0 |
| d. Malformada | 1 | 3 s | `ArgumentException: Format of the initialization string does not conform to specification starting at index 0.` | 0 |
| e. Senha errada com `--verbose` | 1 | 3 s | 57 linhas de diagnóstico do EF, `Opening connection to database 'qaf8_bundle2' on server 'tcp://qaf8-db:5432'` | 0 (`password` aparece 3 vezes, só na frase do PostgreSQL) |

O motivo está na primeira linha em todos os casos; a saída traz a pilha completa depois (comportamento padrão do EF; R-13).

### QA8-12 — Banco inexistente e migration alvo: **OK**, com RISCO (R-11, R-12)

- Banco inexistente: exit 0, o bundle **criou** `qaf8_nao_existe` (aparece em `psql -l`) e aplicou as 2 migrations. (R-11: erro de digitação no nome vira banco novo e vazio, e a API aponta para o certo.)
- `./efbundle 20260818070004_InitialCreate`: 1 linha no histórico; depois `./efbundle`: 2 linhas e 139 feriados. Chega ao mesmo esquema.
- `./efbundle MigracaoQueNaoExiste`: exit 1, `System.InvalidOperationException: The migration 'MigracaoQueNaoExiste' was not found.` mais a pilha.
- `./efbundle 0`: exit 0, `Reverting migration '20261030222641_AddHolidaysAndIndexRates'` e `'20260818070004_InitialCreate'`; `\dt` fica só com `__EFMigrationsHistory` (0 linhas). **Sem confirmação** (R-12), e o README não menciona que o bundle aceita migration alvo.

## Fábrica de design-time no `dotnet ef` local

### QA8-13 — Qual conexão o `dotnet ef` usa: **OK**

| Caso (bash, da raiz, salvo indicação) | Banco e servidor |
|---|---|
| Sem variável | `fixedincome` em `tcp://localhost:5432` (arquivo) |
| Variável com `Port=25432;Database=qaf8_prod` | `qaf8_prod` em `tcp://localhost:25432` |
| `ConnectionStrings__DefaultConnection=` (vazia) | volta ao arquivo: `fixedincome`, 5432 |
| Variável com três espaços | volta ao arquivo |
| `migrations list --no-connect` | lista as 2 migrations, "Pending status not shown" |
| `migrations list` com variável | lista as 2 migrations |
| De `src/` e de `src/FixedIncome.Infrastructure` (sem variável) | `fixedincome`, 5432, funciona |
| PowerShell 5.1 com `$env:...=` | `qaf8_prod`, 25432; depois de `Remove-Item Env:...`, `fixedincome`, 5432. `$env:X = ''` remove a variável (confirmado: `Test-Path` devolve `False`) |

A senha da variável não aparece na saída (0 ocorrências).

### QA8-14 — `database update` aponta para o banco certo: **OK**, com RISCO (R-14)

- Com a variável (25432, `qaf8_efupd`): aplicou as 2 migrations; `__EFMigrationsHistory` = 2, `holiday` = 139.
- Banco recriado, sem variável, só `--connection "Host=localhost;Port=25432;Database=qaf8_efupd;..."`: aplicou as 2 migrations em `qaf8_efupd` (histórico = 2). O `--connection` venceu o arquivo.
- Banco do desenvolvedor intacto (`139|1795|2` antes e depois).

R-14: a seção "Localmente, com o SDK" do README não avisa que a variável, se sobrar no terminal, passa a ter precedência sobre o `appsettings.Development.json` (o QA8-13 mostra a precedência).

## Importação do CDI no fluxo real

### QA8-15 — Carga inicial e execução seguinte: **OK**

Primeira execução (`qaf8_import`, sem cotações): `dataInicial=01/01/2020&dataFinal=09/11/2026` nas séries 12 e 433; `Importação de cotações concluída: 1718 de CDI, 0 de CDI descartadas e 81 de IPCA.` **N0 = 1718** cotações de CDI (2020-01-02 a 2026-11-05); IPCA 81 (2020-01 a 2026-09). **L0 = vazio** (nenhum dia útil, pelo calendário do banco, sem cotação). `dataFinal` é a data de Brasília.

Reinício: série 12 com `dataInicial=06/11/2026&dataFinal=09/11/2026` (próximo dia útil depois de 05/11), série 433 com `dataInicial=01/10/2026`; `concluída: 0 de CDI, 0 de CDI descartadas e 0 de IPCA`. Nenhum aviso de descarte.

### QA8-16 — Bordas de calendário: **OK** (a a d); e com **RISCO** (R-16)

Para cada linha apaguei as cotações de CDI posteriores à data e reiniciei `qaf8-api-bc`:

| Caso | Última cotação | `dataInicial` pedida (log) | Esperada | Cotações importadas | Depois |
|---|---|---|---|---|---|
| a. Carnaval 2026 | 2026-02-13 | `18/02/2026` | `18/02/2026` | 180 | 1718, lacunas vazias |
| b. Natal 2025 | 2025-12-24 | `26/12/2025` | `26/12/2025` | 215 | 1718, lacunas vazias |
| c. Réveillon | 2025-12-31 | `02/01/2026` | `02/01/2026` | 211 | 1718, lacunas vazias |
| d. Dia útil comum | 2026-11-05 | `06/11/2026` | `06/11/2026` | 0 | 1718, lacunas vazias |
| e. `from == hoje` | 2026-11-06 (sintética) | `08/11/2026` | `09/11/2026` (hoje) | 0 | ver abaixo |

Em todos os casos `0 de CDI descartadas` e nenhum aviso de descarte repetido.

**e.** Hoje é um dia útil (segunda-feira, 2026-11-09), então o caso se aplica. O SGS ainda não publicara o CDI de 06/11, então inseri em `qaf8_import` uma cotação **sintética** de 2026-11-06 (cópia do valor de 05/11), reiniciei e removi a linha em seguida (`qaf8_import` voltou a 1718). O log pediu `dataInicial=08/11/2026&dataFinal=09/11/2026`, e não `09/11/2026` (= hoje). Causa, depois de reproduzir (`SgsIndexRateProvider.cs:67-69`): o provedor recua um dia quando `from == to`, com o comentário "O SGS responde 400 quando dataInicial == dataFinal". Hoje esse 400 não se reproduz: `curl` direto em `bcdata.sgs.12` com `dataInicial=dataFinal=05/11/2026` devolveu 200 com a cotação, e com 09/11/2026 devolveu 404 `Value(s) not found` (janela sem dados), que a importação trata como vazio. O efeito é nulo (a janela inclui um domingo sem dados), mas a janela diverge da regra "partir do próximo dia útil" e a premissa do contorno não se confirma. Classificado como `RISCO`, não `BUG`; decisão do Matheus.

### QA8-17 — Hoje é fim de semana ou feriado: **NÃO EXECUTADO**

A sessão caiu numa segunda-feira (2026-11-09), dia útil. O relógio do contêiner não é controlável sem ferramenta nova. Fica a cobertura do teste `Cdi_importado_na_sexta_nao_chama_o_provedor_no_domingo`. Janelas possíveis: 14 e 15/11/2026 (sábado e domingo) e 20/11/2026 (sexta, Consciência Negra).

### QA8-18 — Calendário vazio no banco: **OK**

`qaf8_cal` com `delete from holiday` (139 linhas), API de Production com proxy morto:

- Log: `FixedIncome.Domain.Common.DomainException: O calendário precisa de ao menos um feriado para definir o período coberto.` na importação de CDI, `Falha ao importar as cotações de Cdi.` e `de Ipca.`, `terminou com falha`. **Nenhuma requisição à série 12**; a série 433 foi tentada (4 vezes: o retry) e falhou no proxy. `/health` 200, `/api/assets` 200: API de pé.
- `POST /api/positions` com o calendário vazio: **400** `"O calendário precisa de ao menos um feriado para definir o período coberto."` (a mensagem diz que o calendário está vazio).
- Para cobrir a projeção, copiei os feriados de volta (`pg_dump` de `qaf8_prod`), criei um aporte PreFixed (projeção 200, `grossAmount 1007.97`), apaguei os feriados de novo e repeti: `projection` com e sem data e `portfolio/summary` devolvem 400 com a mesma mensagem. `/api/indexes/Cdi` segue 200 `[]`.

Nenhum 500 e nenhum cálculo como se não houvesse feriado.

### QA8-19 — JSON truncado do SGS ao vivo: **NÃO EXECUTADO**

Por decisão do Matheus: os testes automatizados cobrem o JSON truncado (`Resposta_com_json_truncado_do_sgs_e_tratada_como_falha_transitoria` e `Json_truncado_em_todas_as_tentativas_propaga_a_falha_depois_de_esgotar_o_retry`).

## Workflows

### QA8-20 — Leitura adversarial de `release.yml` e `ci.yml`: **RISCO** (nenhum BUG)

Lidos por inteiro; a regra do `latest` seguinte à do smoke foi conferida: o passo `latest` (linha 159-163) tem `if: github.ref == 'refs/heads/main'` sem status explícito, e o GitHub o trata como `success() && (...)`, então só roda com o smoke verde. `release.yml` só tem o gatilho `push` na `main`. Nada nele roda em PR.

| # | Veredito | Evidência |
|---|---|---|
| a. Re-run antigo | **RISCO** | Num re-run, `github.sha` e `github.ref` são os da execução original. Os passos 105 e 159-163 empurram a tag do SHA antigo e movem `latest` para ele depois de um smoke verde. Nada compara o SHA com o topo da `main`. Decisão 9: `RISCO`, o Matheus decide |
| b. Dois pushes seguidos | OK | `concurrency: release, cancel-in-progress: false` (14-16): A roda, B é descartado, C roda. O SHA de B nunca vira imagem; a SPEC-F8 documenta isso. O `ci.yml` autônomo roda nos três pushes (também por `push` na `main`), então cada merge roda o CI duas vezes (autônomo e chamado) |
| c. Push só de `docs/` ou README | **RISCO** | Sem `paths-ignore`: CI duas vezes, imagem nova, bundle de 119 MiB retido por 30 dias, por commit de documentação |
| d. PR de fork | OK | `release.yml` não tem `pull_request`. `ci.yml` aciona em PR com token somente leitura para fork e sem segredos |
| e. Credenciais persistidas | **RISCO** | `actions/checkout@v4` (linhas 53 e 16) sem `persist-credentials: false`. No job `release` (com `packages: write`), o token fica em `.git/config` durante `dotnet tool restore`, a geração do bundle (compila o projeto) e `docker build`; o `docker login` (93) deixa a credencial em `~/.docker/config.json` nos passos seguintes. `zizmor` aponta `artipacked` nos dois checkouts |
| f. Interpolação em `run:` | **RISCO** (baixo) | `${{ github.actor }}` (93, sem aspas), `${{ github.sha }}` e `${{ github.repository }}` entram direto no script. `sha` é hexadecimal; `repository` e `actor` têm alfabeto restrito (`[A-Za-z0-9-_./]`, mais `[bot]` no ator). Não vi vetor controlável por terceiro num push na `main`. Mesmo assim o `zizmor` classifica o ator como `error[template-injection]`; passar por `env:` e colocar aspas elimina o alerta |
| g. Laço de espera | **RISCO** (baixo) | `for i in $(seq 1 60)` com `--max-time 2` e `sleep 1`: ~60 s com "conexão recusada" imediata, até ~180 s se o `curl` ficar pendurado. A mensagem de falha diz "60s" (linha 140) |
| h. `coverage-report` sem `overwrite` | **pergunta aberta** | O `ci.yml` (46-51) usa `actions/upload-artifact@v4` com nome fixo e `if: always()`. Em "Re-run failed jobs" de uma execução cujo CI falhou, o upload provavelmente conflita com o artefato da tentativa anterior (o do bundle tem `overwrite: true`). Só a execução real confirma |
| i. Sem `workflow_dispatch` | **RISCO** (baixo) | Republicar `latest` depois de um smoke instável exige re-run (ver a) ou commit novo |
| j. SDK flutuante | **RISCO** (baixo) | `setup-dotnet` com `8.0.x`; Dockerfile com `sdk:8.0` e `aspnet:8.0`; `postgres:16`; `ubuntu-latest`; `actions/*@v4` por tag. Duas execuções do mesmo SHA podem gerar imagens diferentes. `zizmor`: `error[unpinned-images]` em `postgres:16` |
| k. `ci` chamado | OK | `permissions: contents: read` basta (`upload-artifact` usa o token do runtime) |
| l. Porta 8080 com `--network host` | OK (baixo) | Runner hospedado limpo; registrar apenas. O smoke ainda assim não distinguiria outro processo na 8080 |

Achados extras da leitura:

- **`ci.yml` sem bloco `permissions`.** Em PR de branch do mesmo repositório, o `GITHUB_TOKEN` usa o padrão do repositório (leitura e escrita, se assim estiver configurado). Quando chamado pelo `release.yml`, herda o teto `contents: read`. `zizmor`: `excessive-permissions` (dois alertas). **RISCO** (R-03).
- **O smoke não consome o artefato.** O passo "Smoke - aplicar bundle" (112-117) executa `./artifacts/efbundle` do disco do runner, depois do `chmod +x`; o comentário diz "com o mesmo bundle publicado como artefato". Os bytes são os mesmos, mas o artefato baixado (zip, sem bit de execução) não é exercitado.
- **Candidatas acumulam.** A tag do SHA é empurrada antes do smoke e nunca removida; o GHCR guarda uma imagem por commit da `main` (inclusive as reprovadas).

**`zizmor` 1.14.2** (`ghcr.io/zizmorcore/zizmor:1.14.2`, `--offline --persona=pedantic`, sobre uma cópia de `.github/workflows/`; auditorias que exigem token da API do GitHub foram puladas): 18 achados (3 corrigíveis automaticamente): 2 informativos, 10 baixos, 4 médios, 2 altos.

- `error[template-injection]` em `release.yml:93` (`github.actor`) e `help[template-injection]` em 99, 100, 101, 106, 124, 125, 129, 162 (`github.sha`, `github.repository`);
- `error[unpinned-images]` em `release.yml:39` (`postgres:16`);
- `warning[artipacked]` em `ci.yml:15` e `release.yml:52`;
- `warning[excessive-permissions]` em `ci.yml:1` e `ci.yml:12`;
- `help[undocumented-permissions]` em `release.yml:31` (`packages: write` sem comentário);
- `info[anonymous-definition]` nos jobs `build-and-test` e `release`.

A saída completa ficou em `scratchpad/qaf8/zizmor.txt`, fora do repositório.

## Documentação

### QA8-21 — README seguido do zero: **BUG** (baixa) e **RISCO**

Seções "Imagem publicada" e "Localmente, com o SDK". Trocas feitas em todos os comandos: nome do contêiner, porta do host, imagem, host, porta e banco do PostgreSQL, usuário e senha (recursos `qaf8`).

| Bloco | Git Bash | PowerShell 5.1 |
|---|---|---|
| `chmod +x efbundle` e `ConnectionStrings__DefaultConnection="..." ./efbundle` | Não rodei literalmente (o bundle está no scratchpad); equivalente com `runtime-deps` funcionou (QA8-09) | **Não roda**: `chmod` e `VAR=valor comando` não existem. O README diz "Linux ou WSL", mas não dá caminho para Windows sem WSL e não menciona `runtime-deps` |
| `dotnet ef migrations bundle ... --output efbundle --force` | Gerei o equivalente no scratchpad (125.026.675 bytes). **`git check-ignore -v efbundle`: não ignorado**; `.dockerignore` só tem `artifacts/`. Na raiz, o arquivo apareceria em `git status` (R-01) | idem |
| `docker pull ghcr.io/.../renda-fixa-api:latest` | **NÃO EXECUTADO** (decisão 5: o pacote ainda não existe no GHCR) | idem |
| `docker run -d --name renda-fixa-api-ghcr -p 8080:8080 \ -e ... imagem` | **Roda** com `host.docker.internal:25432` (trocas: nome `qaf8-api-x`, porta 28081, imagem local). `/health` 200, `/api/assets` 200. **Sem `IndexRatesImport__Enabled`, a imagem chamou o Banco Central ao subir**: log `Sending HTTP request GET ...sgs.12...` e `concluída: 1718 de CDI, 0 de CDI descartadas e 81 de IPCA`. A seção não avisa disso (R-04) | **Quebra**: `docker: invalid reference format` (a `\` vira o nome da imagem) e depois `-e : O termo '-e' não é reconhecido ...`. A seção "Com Docker" avisa "numa só linha"; esta não (R-06) |
| "Se o serviço `api` do Compose estiver rodando ... `docker compose stop api`" | **Não executado** (pararia `fixedincome-api`). A afirmação confere: `docker ps` mostra `fixedincome-api` com `0.0.0.0:8080->8080/tcp` | — |
| Bloco Linux (`--network host`, `localhost`) | Leitura: coerente com o smoke do pipeline | — |
| "Localmente, com o SDK" | `docker compose up -d db` **não executado** (o Compose mexeria nos contêineres do desenvolvedor); `dotnet tool restore` e `dotnet ef database update` equivalentes funcionaram (QA8-13 e QA8-14). O bloco não avisa sobre a variável de ambiente (R-14) | idem |
| "Com Docker, sem instalar nada" (`--network container:fixedincome-db`) | **NÃO EXECUTADO**: apontaria o `dotnet ef database update` para o banco do desenvolvedor | idem |

### QA8-22 — Coerência entre README, ESPECIFICACAO, SPEC-F8 e ROADMAP: **BUG** (baixa)

| Ponto | Resultado |
|---|---|
| Contagem de testes | **BUG (baixa)**, B-01. README (linhas 379-386): "558 testes" e Application 105, Infrastructure 110, Api 51. `dotnet test` da solution agora: Domain 292, Application **109**, Infrastructure **123**, Api **54** = **578**, todos passando |
| Tamanho do bundle | **BUG (baixa)**, B-03. SPEC-F8 (linha 124): "dezenas de MB"; medido 125.026.675 bytes (119 MiB) |
| "F1 — Fundação" com "health check respondendo" | **BUG (baixa)**, B-02. ESPECIFICACAO linha 283 contradiz as linhas 212-213 ("A F1 previa um health check; ele só foi entregue na F8") |
| `IndexRatesImport:Enabled` contra `IndexRatesImport__Enabled` | **RISCO** (R-04). README (78 e 306) só usa a sintaxe de `appsettings`; a seção da imagem não explica a tradução para variável de ambiente. O ROADMAP e a SPEC-F8 usam as duas |
| Porta | **RISCO** (R-07). `.http` em 5124 (a requisição `GET /health` está na linha 137), README e imagem em 8080 (R-03 da F7, persiste) |
| Lista de endpoints | OK. O README não tem lista; o `GET /health` está descrito no parágrafo da linha 24 ("à parte dessa contagem" de dez endpoints) |
| Comando do bundle | OK. README, `release.yml` e SPEC-F8 só diferem no `--output` (`efbundle` contra `artifacts/efbundle`), como o README diz |
| Senha e nomes do smoke | OK. `smoke`/`smoke`/`smoke` no `release.yml`; a SPEC-F8 diz "senha literal no YAML"; o README não cita a senha |
| graphify "fora do versionamento" (ESPECIFICACAO, linha 263) contra o `CLAUDE.md` | Confirmado, fora da F8. `git ls-files graphify-out` = 0 arquivos e `.gitignore:487` ignora a pasta, então a ESPECIFICACAO está certa; a regra do `CLAUDE.md` sobre "A committed graphify-out/graph.json" descreve o contrário |

## Achados

Classificação sugerida; a decisão final é do Matheus. Não há achado de severidade alta ou média entre os `BUG`.

### BUG

#### B-01 — README conta 558 testes; a solução tem 578 (sugestão: baixa)

- Onde: `README.md`, seção "Testes", linhas 379-386.
- Passos: `dotnet test --configuration Release` na raiz.
- Esperado: totais iguais ao README (a validação F8 já tinha medido 578).
- Obtido: Domain 292, Application 109, Infrastructure 123, Api 54 (578); o README diz 292, 105, 110, 51 (558).
- Teste sugerido: não é de código; atualizar o README (ou a cobertura por camada) ao fechar a fase.

#### B-02 — ESPECIFICACAO contradiz a si mesma sobre o health check da F1 (sugestão: baixa)

- Onde: `docs/ESPECIFICACAO.md`, linha 283 ("Compose com PostgreSQL, health check respondendo.") contra as linhas 212-213 ("A F1 previa um health check; ele só foi entregue na F8").
- Esperado: um texto só. Obtido: dois.
- Teste sugerido: não é de código.

#### B-03 — SPEC-F8 diz "dezenas de MB" para o bundle de 119 MiB (sugestão: baixa)

- Onde: `docs/SPEC-F8-EXECUCAO.md`, linha 124.
- Esperado: ordem de grandeza correta (centenas de MB com 30 dias de retenção, por merge). Obtido: "dezenas de MB"; medido 125.026.675 bytes.
- Teste sugerido: não é de código.

### RISCO

Em ordem de relevância sugerida:

- **R-01 (média). `efbundle` na raiz não é ignorado.** O README manda gerar o bundle com `--output efbundle` na pasta atual. `git check-ignore -v efbundle` não casa com nada e o `.dockerignore` só tem `artifacts/`. O arquivo (119 MiB) apareceria em `git status` e poderia ser commitado por engano. O `Dockerfile` só copia `src/`, então não entra na imagem. Sugestão: `efbundle` no `.gitignore` e no `.dockerignore`.
- **R-02 (média). Re-run antigo move `latest` para trás** (QA8-20 a; decisão 9). Sugestão: comparar `github.sha` com o topo da `main` antes do passo `latest`, ou um `concurrency` com verificação.
- **R-03 (média/baixa). Cadeia de suprimentos nos workflows** (QA8-20 e): `persist-credentials: false` ausente nos dois checkouts; `ci.yml` sem `permissions`; ação e imagens flutuantes (j). O job `release` tem `packages: write` durante `dotnet tool restore` e a geração do bundle.
- **R-04 (baixa). Seção "Imagem publicada" incompleta:** não diz que a imagem importa cotações do Banco Central ao subir (observado: 2 chamadas por partida), não traduz `IndexRatesImport:Enabled` para `IndexRatesImport__Enabled`, não manda reiniciar a API depois do bundle (a seção "Com Docker" manda) e não descreve o que se vê com a ordem invertida (500 em `/api/*` até aplicar o bundle; QA8-07 e QA8-08).
- **R-05 (baixa). `IndexRatesImport__Enabled` só liga com `true` ou ausente;** `0`, `no`, `off`, `1`, `yes` e vazio desligam, sem nenhuma linha no log. Sugestão: logar `Importação de cotações desligada` na partida.
- **R-06 (baixa). Blocos da seção "Imagem publicada" só funcionam em bash** (`\` de continuação, `VAR=valor ./efbundle`, `chmod`), sem aviso, e não há caminho para Windows sem WSL (a validação usou `runtime-deps`, que o README não cita).
- **R-07 (baixa). `.http` na porta 5124; README e imagem na 8080** (R-03 da F7, persiste).
- **R-08 (baixa). Banco fora do ar responde 500, não 503** (decisão 9). O 500 é o sinal que o smoke usa.
- **R-09 (baixa). Banco congelado:** a primeira requisição de dados leva 45 s para falhar (depois, 15 s); `/health` não é afetado.
- **R-10 (baixa). Mensagem do resumo sem cotações** (`"Cotação de CDI ausente para 2026-09-01."`) não diz qual posição falhou nem que a importação pode estar desligada.
- **R-11 (baixa). O bundle cria o banco quando o nome está errado** (`qaf8_nao_existe` foi criado, exit 0). Decisão 9.
- **R-12 (baixa). `./efbundle 0` reverte tudo sem confirmação,** e o README não menciona a migration alvo. Decisão 9.
- **R-13 (baixa). Saída do bundle com pilha completa;** `--connection ""` termina com `Host can't be null` e não diz que a opção veio vazia.
- **R-14 (baixa). README "Localmente, com o SDK" não avisa** que `ConnectionStrings__DefaultConnection` no terminal tem precedência sobre o `appsettings.Development.json` (demonstrado no QA8-13).
- **R-15 (baixa). Pipeline:** o CI roda duas vezes por merge; sem `paths-ignore`; sem `workflow_dispatch`; laço de espera de ~180 s com mensagem "60s"; o smoke não exercita o artefato baixado; tags de candidatas acumulam no GHCR; `coverage-report` sem `overwrite` pode conflitar no re-run (pergunta aberta).
- **R-16 (baixa). `from == hoje`:** o provedor pede `dataInicial` um dia antes (QA8-16e: `08/11/2026`), com a premissa do "400 do SGS" que hoje não se reproduz.
- **R-17 (baixa). Sem connection string a imagem sai com exit 139** e pilha (QA8-04a). Não sobe saudável, o que era o risco temido.

### Pergunta aberta

- Só uma execução real do Actions confirma o conflito do `coverage-report` em re-run (QA8-20 h).

## Dados criados durante o QA

Tudo está nos bancos `qaf8_*` do contêiner `qaf8-db`, que foi removido junto com os dados. Ids dos títulos `QA-F8` (prefixo `QA-`), por banco lógico:

| Banco | Título | Id |
|---|---|---|
| `qaf8_prod` | `QA-F8 PRE12` | `c07f7678-c13b-45cd-a9d4-ee634c36cb5d` |
| `qaf8_prod` | `QA-F8 CDI100` | `bccde832-a55e-4ce4-bf57-5a5aa8544472` |
| `qaf8_prod` | `QA-F8 IPCA6` | `ba322e58-b0aa-4bf9-8853-b92dfc1790f5` |
| `qaf8_order` | `QA-F8 08b CDI` | `4478cf62-7bee-424f-bdb3-5228ab4e3239` |
| `qaf8_cal` | `QA-F8 cal vazio` | `480d6f86-3259-452e-8a3e-783b1d2f6c78` |
| `qaf8_order` (apagado antes do 08b) | `QA-F8 08a CDI` | `2970cdfd-68aa-48bf-a21e-55f64d6052f5` |

Aportes citados: `70051349-215c-4bba-9bcc-ae2f100e33aa` (PreFixed), `85b5ccd3-87c7-45c0-99a3-467b1cd9c6aa` (CDI), `c57e08e1-84fc-41ff-bcf5-69abdb563deb` (IPCA+), `959f2ff4-178a-457e-a4a5-7e547e03c8a7` (08b), `e4a9d42d-c478-4e07-aa86-d0483ef5b3fa` (calendário). A cotação sintética de CDI de 2026-11-06 em `qaf8_import` foi removida na hora.

## Estado final do ambiente

- Removidos: contêineres `qaf8-api-prod`, `qaf8-api-x`, `qaf8-api-bc`, `qaf8-dbx` e `qaf8-db`; rede `qaf8-net`; imagem `qaf8/renda-fixa-api:wt`; os dois volumes anônimos criados pelos PostgreSQL `qaf8` (um terceiro volume anônimo, de 03h19 UTC, é anterior à sessão e foi mantido). Nenhum contêiner `qaf8-mitm` foi criado.
- Imagens baixadas por este QA e removidas: `postgres:16`, `mcr.microsoft.com/dotnet/runtime-deps:8.0` e `ghcr.io/zizmorcore/zizmor:1.14.2`. `comm` contra `antes-images.txt`: sem diferença.
- `fixedincome-api` (8080) e `fixedincome-db` (5432, `healthy`) intactos, mesmo `docker ps`; banco do desenvolvedor `139 | 1795 | 2` antes e depois.
- Working tree: `git status --short` igual ao do início (a única diferença é este arquivo, `docs/QA-F8.md`). Nenhum `efbundle` na raiz; nada de `git add`, `commit`, `push`, `docker login` ou `docker push`. `dotnet test` e `dotnet ef` escreveram só em `bin/` e `obj/`, ignorados pelo git.
- Portas 25432, 25433, 28080, 28081 e 28082 livres.

## Resumo

| Roteiro | Veredito | Achados |
|---|---|---|
| QA8-01 | OK | nenhum (`/health/` e `/HEALTH` respondem 200, iguais a `/health`) |
| QA8-02 | OK | nenhum |
| QA8-03 | OK | R-10 |
| QA8-04 | OK, com RISCO | R-05, R-17 |
| QA8-05 | OK, com RISCO | R-08 |
| QA8-06 | OK, com RISCO | R-09 |
| QA8-07 | OK, com RISCO | R-04 |
| QA8-08 | OK, com RISCO | R-04 |
| QA8-09 | OK | nenhum |
| QA8-10 | OK, com RISCO | R-13 |
| QA8-11 | OK, com RISCO | R-13 |
| QA8-12 | OK, com RISCO | R-11, R-12 |
| QA8-13 | OK | nenhum |
| QA8-14 | OK, com RISCO | R-14 |
| QA8-15 | OK | nenhum |
| QA8-16 | OK (a a d), RISCO (e) | R-16 |
| QA8-17 | NÃO EXECUTADO | hoje é segunda-feira; relógio do contêiner não controlável |
| QA8-18 | OK | nenhum |
| QA8-19 | NÃO EXECUTADO | por decisão do Matheus: os testes automatizados cobrem o JSON truncado |
| QA8-20 | RISCO | R-02, R-03, R-15 (a, c, e, f, g, i, j); h em aberto |
| QA8-21 | BUG (baixa) e RISCO | R-01, R-04, R-06, R-07, R-14; `docker pull` do GHCR e blocos que tocam o contêiner do desenvolvedor `NÃO EXECUTADO` |
| QA8-22 | BUG (baixa) | B-01, B-02, B-03 |

Nenhum 500 fora do esperado (banco fora, senha errada e bundle ainda não aplicado), nenhuma senha em corpo de resposta, em log ou em saída de bundle.

**Veredito: SEM BUGS BLOQUEANTES.** Três `BUG` de severidade baixa, todos de documentação, e dezessete `RISCO` para decisão do Matheus (R-01 a R-03 são os de maior peso).
