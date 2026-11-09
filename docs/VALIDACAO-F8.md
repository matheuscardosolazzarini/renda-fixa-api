# Validação da F8 — Entrega contínua da imagem e pendências da F7

Verificação independente da F8, feita em 09/11/2026. Cada afirmação abaixo vem de comando
rodado ou arquivo lido nesta verificação; nenhum número de relatório de outro agente foi
reaproveitado sem conferência. Nenhum código foi alterado, nada foi commitado ou adicionado
ao índice, e o working tree terminou com as mesmas 15 entradas do início, mais este arquivo.

- Data: 2026-11-09
- Commit-base: `1799fce` (HEAD, o commit que adiou a nuvem; é anterior à fase). A árvore tinha
  alterações não commitadas: 15 entradas em `git status --short` (11 modificadas e 4 não
  rastreadas: `release.yml`, `docs/SPEC-F8-EXECUCAO.md`, `HealthEndpointTests.cs` e a pasta
  `tests/FixedIncome.Infrastructure.Tests/Context/`). Isso é esperado: a fase não foi commitada.
  `git reflog` mostra o commit `1799fce` como o último; `git stash list` vazio; `git diff --cached`
  vazio.
- Fontes: `docs/ROADMAP.md` (seção F8), `docs/SPEC-F8-EXECUCAO.md` (aprovada, com a Emenda do P6),
  `CLAUDE.md`.

## Ambiente da verificação

- Docker Desktop disponível. Os contêineres do desenvolvedor (`fixedincome-api` na 8080 e
  `fixedincome-db` na 5432) não foram tocados: continuam `Up` ao final.
- Os testes de ponta a ponta usaram nomes e portas próprios: rede `f8verif-net`, contêineres
  `f8verif-db` (PostgreSQL 16, porta 15432), `f8verif-api` (18080) e `f8verif-api2` (18081). Tudo
  removido ao final (contêineres, rede, imagem `f8verif/renda-fixa-api`, as imagens `postgres:16` e
  `runtime-deps:8.0` que precisei baixar, e a pasta `artifacts/`). `docker images` voltou ao conjunto
  que existia antes.
- `gh` não está instalado nesta máquina e o `release.yml` não está commitado: não há execução do
  Actions para consultar (ver "Itens abertos").
- Sem Python na máquina; os cálculos de cobertura foram feitos em PowerShell.

## Baseline e execução

| Verificação | Comando | Resultado |
|---|---|---|
| Build | `dotnet build --no-incremental` e `dotnet build --configuration Release` | `0 Warning(s)  0 Error(s)` nos dois |
| Testes | `dotnet test --no-build` | Domain 292, Application 109, Infrastructure 123, Api 54 = **578**, 0 falhas, 0 ignorados |
| Skip | `grep -rn "Skip" tests --include=*.cs` | nenhuma ocorrência |
| Pacotes | `git diff --stat -- '*.csproj' '*.json' docker-compose.yml Dockerfile` | vazio: nenhum pacote NuGet novo, `dotnet-tools.json`, `Dockerfile` e compose intactos |

Contagem de testes: **558 antes (linha de base informada na spec), 578 depois** (+20). Nenhuma
camada caiu: Domain 292 → 292, Application 105 → 109 (+4), Infrastructure 110 → 123 (+13), Api
51 → 54 (+3). Conferi a soma das novidades nos arquivos: Application 4 testes novos de P1;
Infrastructure 1 teste novo líquido no handler (um substituído por dois) + 4 casos novos na theory
+ 8 casos de `FixedIncomeDbContextFactoryTests` (4 fatos e 2 theories de 2 linhas); Api 3
testes de `HealthEndpointTests`.

## Cobertura

Método do CI (`ci.yml`): `dotnet build --configuration Release`, depois, um projeto por vez,
`dotnet test tests/FixedIncome.<X>.Tests --configuration Release --no-build --settings
coverlet.runsettings --collect:"XPlat Code Coverage"`. Cada linha usa o `<package>` do assembly da
própria camada no `coverage.cobertura.xml` do projeto de teste correspondente (`line-rate` e
`branch-rate`).

| Camada | Testes | Linha | Branch | README hoje (linha / branch) | Diverge? |
|---|---|---|---|---|---|
| Domain | 292 | **95,2%** | **98,1%** | 95,2% / 98,1% | não |
| Application | 109 | **92,5%** | **98,9%** | 92,5% / 98,9% | não |
| Infrastructure | 123 | **99,7%** | **98,3%** | 95,6% / 98,1% | sim |
| Api | 54 | **96,3%** | **89,1%** | 96,3% / 89,1% | não (só os testes) |

Valores brutos: Domain `0.9519 / 0.981`; Application `0.9245 / 0.9888`; Infrastructure `0.9971 /
0.9827`; Api `0.9631 / 0.8913`. Application fica em 92,45% e o README já arredonda para 92,5%.

Meta da fase (Domain e Application acima de 80%): atendida.

Para o orquestrador atualizar a seção "Testes" do README: **578 testes**; tabela Domain 292 /
95,2% / 98,1%; Application 109 / 92,5% / 98,9%; Infrastructure 123 / 99,7% / 98,3%; Api 54 / 96,3% /
89,1%. A subida de Infrastructure (95,6% para 99,7%) vem da fábrica do contexto e do handler do SGS,
que agora têm teste para todos os desvios.

## Pendências da F7 — o teste falha sem a correção

Método: copiei o repositório (sem `.git`, `bin`, `obj`) para um diretório temporário da sessão,
troquei nessa cópia os quatro arquivos de produção pela versão do HEAD (`git show
HEAD:<arquivo>`), mantive os testes novos, compilei (0 avisos, 0 erros) e rodei `dotnet test`. O
working tree original não foi tocado. A cópia foi apagada ao final.

| Correção revertida | Testes que falham na cópia | Motivo |
|---|---|---|
| P1 `ImportIndexRatesUseCase` (busca do CDI a partir de `last + 1`) | 4 de Application: `Execucao_incremental_do_cdi_pula_feriado_e_fim_de_semana_ate_o_proximo_dia_util`, `Cdi_importado_na_sexta_nao_chama_o_provedor_no_domingo`, `Cdi_importado_na_sexta_busca_apenas_a_segunda_quando_hoje_e_a_segunda`, `Cdi_descartado_que_e_o_mais_recente_nao_e_buscado_nem_avisado_de_novo_na_execucao_seguinte` | A busca parte de `last + 1`, e não do próximo dia útil (a asserção sobre a data inicial ou sobre a ausência de chamada falha). O teste renomeado `..._parte_do_proximo_dia_util_apos_a_ultima_data_importada` (16/04 para 17/04) passa nos dois lados, como a spec prevê |
| P2 `SgsNonJsonResponseHandler` (JSON truncado) | 6 de Infrastructure: os 4 casos novos da theory (`[{"data":...,"valor":`, `{`, `[1,2`, `{"a":1}}`), `Json_truncado_em_todas_as_tentativas_propaga_a_falha_depois_de_esgotar_o_retry`, `Resposta_com_json_truncado_do_sgs_e_tratada_como_falha_transitoria` | O corpo começa com `{` ou `[` e passa sem retry (1 requisição, `JsonException`) |
| P5 `FixedIncomeDbContextFactory` (variável de ambiente) | 5 de Infrastructure: `Deve_usar_a_connection_string_da_variavel_de_ambiente_sem_ler_arquivo`, `Deve_priorizar_a_variavel_de_ambiente_sobre_o_arquivo`, `Deve_lancar_excecao_citando_as_duas_fontes_quando_nao_ha_variavel_nem_arquivo`, e as duas linhas de `Deve_lancar_excecao_quando_a_variavel_e_vazia_ou_so_espacos_e_nao_ha_arquivo` | A fábrica antiga ignora a variável e lê só o arquivo, sem a mensagem que cita as duas fontes |
| P3 `Program.cs` (`/health`) | 3 de Api: `Get_health_...`, `Head_health_retorna_200`, `Post_health_retorna_405_no_formato_api_response` | Sem o endpoint, as três asserções de status falham (não capturei o código recebido) |
| P3 só a restrição de verbo (retirei o `WithMetadata`) | 1 de Api: `Post_health_retorna_405_no_formato_api_response` | `Expected: MethodNotAllowed  Actual: OK` |

Total na cópia com os quatro arquivos revertidos: 4 + 11 + 3 = 18 falhas (11 em Infrastructure =
6 do handler + 5 da fábrica). Os testes que sobram verdes são os de comportamento que já valia.
Três casos da fábrica (`Deve_ler_o_appsettings_...`) passam nos dois lados por testarem o caminho
antigo, que continua valendo.

## `GET /health` com a API rodando

Imagem construída do working tree e executada em contêiner (`ASPNETCORE_ENVIRONMENT` não definido,
log: `Hosting environment: Production`), porta 18080:

| Chamada | Resultado |
|---|---|
| `GET /health` | `200`, corpo `Healthy` |
| `HEAD /health` | `HTTP/1.1 200 OK`, `Content-Type: text/plain` |
| `POST /health` | `HTTP/1.1 405`, `Allow: GET, HEAD`, corpo `{"statusCode":405,"success":false,"message":"Método HTTP não permitido para este recurso.","data":null}` |
| `GET /api/assets` (banco com o bundle aplicado) | `200`, `{"statusCode":200,"success":true,"message":null,"data":[]}` |

## Pipeline

### actionlint 1.7.7

`MSYS_NO_PATHCONV=1 docker run --rm -v "$(pwd -W):/repo" -w /repo rhysd/actionlint:1.7.7` sobre os
dois workflows: saída vazia, **exit 0** (`actionlint -version` confirma 1.7.7). Controle negativo:
em cópia temporária, troquei `needs: ci` por `needs: cii` e a ferramenta apontou
`job "release" needs job "cii" which does not exist` com exit 1.

### Leitura do `release.yml` e do `ci.yml`

| Propriedade exigida | Onde está | Resultado |
|---|---|---|
| Dispara em push na `main` | `release.yml:3-5`, `on.push.branches: [main]` | ok |
| `ci.yml` reutilizável sem mudar os passos | `ci.yml:8-9` ganha só `workflow_call:`; `git diff` do `ci.yml` tem 2 linhas | ok |
| Release só depois do CI verde | `release.yml:20-26`, job `ci` (`uses: ./.github/workflows/ci.yml`) e `release` com `needs: ci` | ok |
| Permissões mínimas | `contents: read` no topo (linha 8-9) e no job `ci`; `packages: write` só no job `release` (29-31) | ok |
| Só `GITHUB_TOKEN`, sem outro segredo ou variável | única ocorrência de `secrets.` é `secrets.GITHUB_TOKEN` (linha 92); nenhum `vars.` | ok |
| Token por `env` e `--password-stdin`, nunca interpolado no script | linhas 90-93; só `github.actor` entra na linha de comando | ok |
| Ações usadas | `actions/checkout@v4`, `actions/setup-dotnet@v4`, `actions/upload-artifact@v4`; build, login, push pela CLI do Docker | ok |
| Imagem `ghcr.io/matheuscardosolazzarini/renda-fixa-api`, tag do SHA completo | `env.IMAGE` (33) e `--tag "$IMAGE:${{ github.sha }}"` (99) | ok |
| Rótulos `source` e `revision` | linhas 100-101; conferi na imagem local: `org.opencontainers.image.source` e `...revision` presentes | ok |
| Bundle gerado antes do build da imagem e anexado | passos 61-86; nome `efbundle-${{ github.sha }}`, `retention-days: 30`, comando idêntico ao da spec | ok |
| Ordem: checkout, dotnet, bundle, upload, login, build, push do SHA, smoke, `latest` | leitura em sequência | ok |
| Smoke com `postgres:16` e `pg_isready` | linhas 37-50 (`--health-cmd "pg_isready -U smoke -d smoke"`), porta 5432 | ok |
| Smoke aplica o bundle sem `--connection`, pela variável | linhas 112-117 | ok |
| `docker pull` do GHCR antes do `docker run` | 124-125: `docker image rm` da tag local, depois `docker pull`, depois `docker run` | ok |
| `IndexRatesImport__Enabled=false` e `--network host` | 126-129 | ok |
| Espera `/health` até 60 s, exige 200 em `/health` e `/api/assets` com `curl --fail` | 131-148 | ok |
| Logs do contêiner em falha | 150-152, `if: failure()` | ok |
| `latest` só na `main` e só depois do smoke | 158-163: último passo, `if: github.ref == 'refs/heads/main'`, sem `always()`; o GitHub acrescenta `success()` implícito, então um passo de smoke com falha pula o passo | ok (leitura) |
| Senha do smoke literal e descartável | linha 35 e 40-43 | ok |
| `concurrency: { group: release, cancel-in-progress: false }` | 14-16 | ok |

### Reprodução local do bundle e do smoke

Mesmos comandos do workflow, com adaptações apenas de rede (`--network host` do runner vira rede
Docker própria; o host do banco vira o nome do contêiner).

| Passo | Comando e saída | Resultado |
|---|---|---|
| Gerar o bundle, na raiz, sem a variável | `dotnet ef migrations bundle --project src/FixedIncome.Infrastructure --startup-project src/FixedIncome.Api --configuration Release --self-contained --target-runtime linux-x64 --output artifacts/efbundle --force`: `Done. Migrations Bundle: ...\artifacts\efbundle`; 26 s; arquivo de **125.026.675 bytes (119 MB)** | gerado |
| Rodar sem variável, em `runtime-deps:8.0`, raiz montada em `/work` | `Connection string não encontrada. Defina a variável de ambiente ConnectionStrings__DefaultConnection ou crie o arquivo appsettings.Development.json em /FixedIncome.Api/...`, `exit=1` | falha com a mensagem das duas fontes |
| Rodar só com `--connection` | mesma mensagem, `exit=1` | confirma a Emenda do P6 |
| Rodar com `ConnectionStrings__DefaultConnection` | `Applying migration '20260818070004_InitialCreate'.`, `Applying migration '20261030222641_AddHolidaysAndIndexRates'.`, `Done.`, `exit=0` | aplicado |
| Esquema depois do bundle | `\dt`: `fixed_income_asset`, `holiday`, `index_rate`, `position`, `__EFMigrationsHistory`; `select count(*) from holiday` = **139**; 2 migrations no histórico | esquema completo e seed de feriados |
| Construir a imagem (com `artifacts/efbundle` presente na raiz) | `docker build` com os dois rótulos; 25 s; usuário `appuser` | ok; `.dockerignore` (`artifacts/`) mantém o bundle de 119 MB fora do contexto |
| Subir a imagem e esperar `/health` | `API respondeu 200 em /health apos 2s`; `Healthy`; `/api/assets -> 200` (o prefixo `C:/Program Files/Git` na saída é só a conversão de caminho do MSYS no `--write-out`) | passa, exit 0 |
| Caso de reprovação sem bundle | outro banco vazio (`smoke2`), mesma imagem; o mesmo script do passo "exigir 200" sob `bash -e`: `Healthy`, `/api/assets -> 500`, `curl: (22) The requested URL returned error: 500`, **exit 22**; log do contêiner: `fail: Microsoft.EntityFrameworkCore.Database.Command[20102]` | smoke reprova (e o `/health` sozinho passaria: por isso o `/api/assets` é necessário) |

O que a reprodução local não cobre: login e push ao GHCR, o `docker pull` real do registro, o
`--network host` no runner, o service container do Actions e o bundle executado direto no runner
(aqui foi num contêiner `runtime-deps`, que é o ambiente que a spec prescreve para o P6).

## Segredos

- `grep` nos workflows: a única referência a segredo é `secrets.GITHUB_TOKEN` (`release.yml:92`);
  `vars.` não aparece. A senha `smoke` em `SMOKE_CONNECTION` e no service container é literal
  descartável, como a spec permite.
- Varredura do repositório (arquivos rastreados e não rastreados, fora de `graphify-out` e `.claude`)
  por `ghp_`, `github_pat_`, `AKIA...`, chaves privadas, `xox[bp]-`, `client_secret`, `AccountKey=`,
  `SharedAccessKey`: zero ocorrências.
- Senhas literais de conexão: as de desenvolvimento que já existiam (`docker-compose.yml`,
  `appsettings.Development.json`: `fixedincome`) e as de teste (`teste`, `p`). Nenhuma é da fase. O
  README ganhou duas linhas com `Password=fixedincome`, a mesma senha local do Compose.
- Nenhum arquivo `.csproj`, `dotnet-tools.json`, `Dockerfile` ou `docker-compose.yml` foi alterado.

## README (afirmações verificáveis)

| Afirmação | Evidência | Resultado |
|---|---|---|
| Badge do `release.yml` ao lado do CI | `README.md:3`; os dois caminhos `.../actions/workflows/ci.yml` e `release.yml` existem | ok (o badge só renderiza depois da primeira execução) |
| "Dez endpoints" | `grep -E "^\s*\[Http(Get|Post|Put|Delete|Patch)" src/FixedIncome.Api/Controllers` conta 10; `/health` fica fora, como o texto diz | ok |
| `/health`: 200 com `Healthy`, não consulta o banco | verificado por curl e pela leitura do `Program.cs` | ok |
| Imagem publicada roda em Production, sem Swagger | log `Hosting environment: Production`; `Program.cs:119` só ativa Swagger em `IsDevelopment()` | ok |
| Artefato `efbundle-<sha>`, 30 dias, exige login, vem em zip, `chmod +x` | `release.yml:80-86`; comportamento conhecido do `upload-artifact@v4` | ok (leitura) |
| Bundle roda com `ConnectionStrings__DefaultConnection`, `--connection` opcional | executado acima | ok |
| Comando do bundle com `--output efbundle` e `dotnet tool restore` | mesmo comando do workflow, só trocando a saída | ok |
| `docker compose stop api` libera a 8080 | contêiner do Compose usa a 8080 (`docker ps`) | ok |
| Ordem do pipeline (artefato, SHA, smoke, `latest`) | `release.yml` | ok |
| Pacote nasce privado, torna-se público manualmente | comportamento do GHCR, igual à spec | não verificável aqui |
| Motivo do adiamento da nuvem | bate com `ROADMAP.md`, "Adiado: publicação em nuvem com Terraform" | ok |
| Seção "Testes": 558 testes e tabela antiga | medido: **578**; tabela nova acima | desatualizada de propósito; o orquestrador atualiza |
| Árvore de pastas lista `FixedIncome.Api.Tests` | `README.md:177` | ok |
| `GET /health` na lista de endpoints | o README não tem lista de endpoints; o `/health` está no parágrafo da linha 24. A tabela de endpoints de `docs/ESPECIFICACAO.md` tem a linha | ok (observação 4) |

## Critérios do ROADMAP (seção F8)

| # | Critério | Evidência | Resultado |
|---|---|---|---|
| 1 | As duas pendências da F7 resolvidas, cada uma com teste escrito antes da correção | Seção "Pendências da F7": revertendo só a correção numa cópia, 4 testes de P1 e 6 de P2 falham. O teste escrito antes da correção não é verificável em si (nada foi commitado); a prova é que ele falha sem a correção e passa com ela | **ATENDIDO** |
| 2 | `GET /health` responde 200, com teste | `HealthEndpointTests` (GET, HEAD, POST 405) verde; curl no contêiner: 200 `Healthy`, HEAD 200, POST 405 com envelope. Sem a restrição de verbo, o teste do POST falha | **ATENDIDO** |
| 3 | Um merge na `main` publica no GHCR uma imagem com a tag do SHA, sem passo manual | Leitura do `release.yml`: gatilho, `needs: ci`, `packages: write`, login pelo `GITHUB_TOKEN`, `docker push` da tag do SHA; actionlint limpo. Não há execução real | **ABERTO** (ver "Itens abertos"); a configuração está correta por leitura |
| 4 | O smoke aplica o bundle num PostgreSQL limpo, a imagem responde 200 em `/health` e `/api/assets`; um smoke que falha marca a execução como falha e não move `latest` | Reproduzido localmente: bundle aplicado (2 migrations, 139 feriados), 200 nos dois endpoints, exit 0; sem bundle `/api/assets` dá 500 e o passo sai com 22. Por leitura, `latest` é o último passo e não tem `always()`. A execução num runner real não existe | **ATENDIDO LOCALMENTE; ABERTO no runner** |
| 5 | Nenhum segredo novo no repositório nem nos logs do Actions (só o `GITHUB_TOKEN`) | Repositório e workflows: ver "Segredos"; nenhum segredo novo. Logs do Actions: não existem ainda | **ATENDIDO no repositório; ABERTO nos logs** |
| 6 | README com o badge, as instruções para rodar a imagem e o motivo do adiamento da nuvem | Seção "README"; `README.md:3`, seção "Imagem publicada", seção "Por que a nuvem está adiada". Falta só atualizar a contagem e a tabela de cobertura da seção "Testes" (tarefa do orquestrador) | **ATENDIDO** quanto ao conteúdo; pendência editorial da contagem |

## Critérios de aceite da spec (1 a 9)

| # | Critério | Resultado |
|---|---|---|
| 1 | Duas pendências resolvidas, cada uma com teste antes da correção | **ATENDIDO** (critério 1 do ROADMAP) |
| 2 | `GET /health` responde 200, com teste | **ATENDIDO** |
| 3 | `release.yml` publica a imagem com a tag do SHA em push na `main`, só com o `GITHUB_TOKEN`, depois do CI verde | Configuração conferida por leitura e actionlint. **ABERTO** até a primeira execução real |
| 4 | Bundle gerado e anexado como artefato | Gera (119 MB) e roda; o passo `upload-artifact@v4` está correto por leitura. O artefato anexado a uma execução real não existe: **ABERTO** |
| 5 | Smoke aplica o bundle em banco limpo, 200 em `/health` e `/api/assets`; smoke que falha reprova e não move `latest` | **ATENDIDO LOCALMENTE; ABERTO no runner** |
| 6 | Nenhum segredo novo no repositório nem nos logs | **ATENDIDO** no repositório; logs **ABERTO** |
| 7 | README com badge, instruções de execução da imagem e motivo do adiamento | **ATENDIDO** |
| 8 | `dotnet build` sem warning e `dotnet test` verde, contagem igual ou maior que 558 | 0 avisos; **578** testes, 0 falhas, 0 ignorados |
| 9 | Nenhum commit foi criado | HEAD continua em `1799fce`; `git diff --cached` vazio; `git stash list` vazio; nenhum arquivo adicionado ao índice | **ATENDIDO** |

## Regras de negócio da fase e o teste que cobre cada uma

| Regra | Teste |
|---|---|
| CDI: a busca incremental parte do próximo dia útil (pula feriado, fim de semana e Tiradentes) | `ImportIndexRatesUseCaseTests.Execucao_incremental_do_cdi_pula_feriado_e_fim_de_semana_ate_o_proximo_dia_util` (17/04 para 22/04), `Execucao_incremental_do_cdi_parte_do_proximo_dia_util_apos_a_ultima_data_importada` (16/04 para 17/04, renomeado) |
| CDI: `from > today` não chama o provedor (sexta importada, domingo) | `Cdi_importado_na_sexta_nao_chama_o_provedor_no_domingo` |
| CDI: fronteira `from == today` | `Cdi_importado_na_sexta_busca_apenas_a_segunda_quando_hoje_e_a_segunda` (28/04 a 28/04) |
| CDI: cotação descartada e mais recente não é pedida nem avisada de novo | `Cdi_descartado_que_e_o_mais_recente_nao_e_buscado_nem_avisado_de_novo_na_execucao_seguinte` (1 importada, 1 descartada, 1 aviso; segunda execução parte de 22/04) |
| Primeira execução continua em 2020-01-01 | testes preexistentes de `ImportIndexRatesUseCaseTests` (passam) |
| SGS: JSON truncado é falha transitória e entra no retry (2 requisições) | `SgsNonJsonResponseHandlerTests.Resposta_com_json_truncado_do_sgs_e_tratada_como_falha_transitoria` |
| SGS: JSON truncado em todas as tentativas propaga `HttpRequestException` depois de 4 requisições | `Json_truncado_em_todas_as_tentativas_propaga_a_falha_depois_de_esgotar_o_retry` |
| SGS: corpo inválido (`[{"data":...,"valor":`, `{`, `[1,2`, `{"a":1}}`, vazio, texto, HTML) | theory `Corpo_que_nao_e_json_com_status_de_sucesso_lanca_http_request_exception` |
| SGS: corpo válido segue passando; status de erro passa sem alteração | testes preexistentes do mesmo arquivo (passam) |
| `GET /health`: 200 `Healthy`, só GET e HEAD, 405 no envelope | `HealthEndpointTests` (3 testes) |
| Fábrica de design-time: variável de ambiente tem precedência; sem ela, o `appsettings.Development.json`; sem nenhuma, exceção citando as duas fontes; vazio e só espaços contam como ausente | `FixedIncomeDbContextFactoryTests` (8 casos; classe e coleção próprias, `DisableParallelization`, variável e diretório restaurados no `Dispose`) |

## Definição de pronto (itens verificáveis aqui)

| Item | Evidência | Resultado |
|---|---|---|
| Build sem warnings | `0 Warning(s)  0 Error(s)` | atendido |
| `dotnet test` verde, sem Skip, contagem não cai | 578 contra 558 | atendido |
| Sem mock library | testes novos lidos: stubs `private sealed` já existentes, nenhuma biblioteca nova; nenhum `.csproj` alterado | atendido |
| AAA com seções separadas e nomes de comportamento | testes novos lidos | atendido |
| Domain sem referência nova | `FixedIncome.Domain.csproj` não aparece no diff | atendido |
| `dotnet ef` local continua funcionando | `dotnet ef dbcontext info` da raiz, sem variável: `Database name: fixedincome`, `Data source: tcp://localhost:5432` (lê o `appsettings.Development.json`; sem conexão ao banco) | atendido |
| `ESPECIFICACAO.md` atualizada | tabela de endpoints (`/health`), parágrafo da F1/F8, exceção ao envelope, regras novas da importação | atendido |
| Cobertura Domain e Application acima de 80% | 95,2% e 92,5% | atendido |
| PR, commit e CI no GitHub | fora do alcance do verificador | pendente do Matheus |

## Observações (não bloqueiam)

1. **Tamanho do bundle.** 125 MB por merge, com retenção de 30 dias. A spec fala em "dezenas de MB".
   Em repositório privado no plano gratuito (500 MB de armazenamento de Actions), quatro merges
   em 30 dias já estouram a cota; em repositório público o armazenamento de artefatos não é cobrado.
   Se o repositório for privado, vale reduzir a retenção ou ligar a compressão do bundle (`dotnet ef`
   não oferece; seria `PublishTrimmed`, fora de escopo).
2. **O smoke executa o arquivo gerado no workspace, não o baixado do artefato.** O comentário do
   passo diz "o mesmo bundle publicado como artefato", o que vale para o conteúdo, não para o
   caminho de ida e volta do artefato (o `chmod +x` simula a perda do bit). Aceitável pela spec.
3. **`overwrite: true` no `upload-artifact`** não está na spec. É inofensivo (permite reexecutar o
   mesmo SHA) e evita conflito de nome em re-run.
4. **README sem lista de endpoints.** O `GET /health` aparece só no parágrafo da linha 24; a tabela
   de endpoints que o passo P8 menciona está em `docs/ESPECIFICACAO.md` e já tem a linha.
5. **Limpeza do contêiner de smoke.** `docker rm --force api` com `if: always()` sai com código 1 se
   o contêiner nunca foi criado (por exemplo, se o `docker pull` falhar). O job já estaria falho e
   `latest` já estaria pulado, então não muda o resultado; só soma um passo vermelho.
6. **`Failed to determine the https port for redirect`** aparece no log de Production a cada
   subida; preexistente e fora do escopo.

## Itens abertos (dependem de uma execução real do GitHub Actions)

Não podem ser comprovados localmente e **não estão marcados como atendidos**:

| Critério | Evidência que falta |
|---|---|
| Merge na `main` publica no GHCR a imagem com a tag do SHA, sem passo manual | ID e link da execução do `release.yml` na `main` com o job `release` verde, e a tag `ghcr.io/matheuscardosolazzarini/renda-fixa-api:<sha completo>` visível em Packages (ou `docker manifest inspect`) |
| `efbundle-<sha>` anexado como artefato | A mesma execução listando o artefato `efbundle-<sha>` |
| Smoke no runner aprova e move `latest`; smoke que falha reprova e não move `latest` | A execução verde mostrando `latest` com o mesmo digest do SHA; e, para o lado negativo, uma execução em que o smoke falha de propósito (por exemplo na branch da fase, incluída temporariamente em `on.push.branches`, decisão do Matheus) com `latest` inalterado. Observação: `if: github.ref == 'refs/heads/main'` impede que a branch mova `latest` |
| Nenhum segredo nos logs do Actions | Leitura dos logs da execução real (o `docker login` com `--password-stdin` deve aparecer sem o token) |
| Pacote do GHCR vinculado ao repositório | A página do pacote mostrando o repositório de origem (rótulo `source`) |
| Torná-lo público (aviso do README) | Ação manual do Matheus nas configurações do pacote |

Também fica com o Matheus: commitar `docs/SPEC-F8-EXECUCAO.md` e a fase, abrir o PR e conferir o
CI. A terceira pendência da F7 ("R-01 ao vivo") está fora de escopo da F8 e continua aberta no
ROADMAP.

## Veredito

**APROVADA**, com os itens abertos acima.

Tudo o que podia ser provado sem uma execução real do GitHub foi provado: build com 0 avisos,
578 testes verdes (558 antes), cobertura acima da meta, as duas pendências da F7 com teste que
falha sem a correção, `/health` por curl (GET e HEAD 200, POST 405 com envelope), actionlint 1.7.7
limpo, `release.yml` conferido ponto a ponto, bundle gerado e aplicado num PostgreSQL 16 limpo
(2 migrations, 139 feriados), imagem respondendo 200 em `/health` e `/api/assets`, smoke sem bundle
reprovando com exit 22, nenhum segredo novo e nenhum commit criado.

Os critérios abaixo ficam abertos até a primeira execução real, com o link ou ID dela:

- [ ] Um merge na `main` publica no GHCR uma imagem com a tag do SHA (critério 3 do ROADMAP)
- [ ] O smoke no runner aprova e move `latest`, e um smoke que falha reprova sem mover `latest` (critério 4, parte do runner)
- [ ] Nenhum segredo nos logs do Actions (critério 5, parte dos logs)

Critérios que este arquivo aprova (o orquestrador pode marcar):

- [x] As duas pendências herdadas da F7 resolvidas, cada uma com teste que falha sem a correção
- [x] `GET /health` responde 200, com teste
- [x] README com o badge, as instruções para rodar a imagem e o motivo do adiamento da nuvem (atualizar contagem e cobertura da seção "Testes")

Pendência do orquestrador (atendida): a seção "Testes" do README foi atualizada. Os números desta
verificação (578 testes) foram superados pelas correções pós-QA: ver a seção "Correções pós-QA"
no fim deste arquivo, que traz a contagem e a cobertura finais (591 testes).

Contagem de testes na verificação: **558 antes, 578 depois** (Domain 292 → 292, Application 105 →
109, Infrastructure 110 → 123, Api 51 → 54).

## Correções pós-QA (09/11/2026)

Registro do orquestrador, sobre o working tree final. A decisão do Matheus foi que nenhum item do
`docs/QA-F8.md` fica pendente: cada risco foi corrigido (com teste, quando é código) ou aceito com o
motivo no ROADMAP, seção "Riscos do QA da F8". A emenda está em `docs/SPEC-F8-EXECUCAO.md`.

### Verificação do estado final (feita pelo orquestrador)

| Verificação | Resultado |
|---|---|
| `dotnet build --no-incremental` | 0 warnings, 0 erros |
| `dotnet test` | **591** verdes, 0 falhas (Domain 292, Application 112, Infrastructure 130, Api 57); eram 578 na verificação e 558 antes da fase |
| `actionlint` 1.7.7 sobre `.github/workflows` | exit 0, sem saída |
| `zizmor` 1.14.2 (`--offline --persona=pedantic`) sobre `.github` | **0 achados** (eram 18: 2 altos, 4 médios, 10 baixos, 2 informativos) |
| Comando PowerShell do README para o bundle | reproduzido contra um PostgreSQL 16 descartável (só a porta trocada): as 2 migrations aplicadas, exit 0, 139 feriados |
| Docker | contêineres `fixedincome-api` e `fixedincome-db` do desenvolvedor intactos; imagens baixadas e contêiner de teste removidos |

Cobertura por camada (método do CI, linha / branch): Domain 95,2% / 98,1%; Application 92,6% /
98,9%; Infrastructure 99,7% / 98,4%; Api 96,4% / 91,3%. O README foi atualizado com esses números.

### Correções, item a item

| Item | Correção | Evidência |
|---|---|---|
| R-01 | `efbundle` no `.gitignore` e no `.dockerignore` | `git check-ignore -v efbundle` casa com o `.gitignore` |
| R-02 | Passo `latest_gate`: `latest` só se o ref for a `main` e `GITHUB_SHA` ainda for o topo da `main` (`gh api`); senão `::warning::` e `move=false`. Vale para `workflow_dispatch` | Leitura do `release.yml`; `actionlint` limpo. **Não executado no runner** (ver abaixo) |
| R-03 | `persist-credentials: false` nos 3 checkouts; `permissions: contents: read` no `ci.yml`; `github.actor`, `github.sha` e `github.repository` em `run` só via `env:` e com aspas; ações fixadas por SHA de commit e `postgres:16` por digest, com a versão em comentário; `dependabot.yml` para `github-actions` e `docker`; bundle num job próprio com `contents: read`, `packages: write` só no job que publica | `zizmor`: 18 → 0 achados; SHAs conferidos com `git ls-remote` pelo implementador |
| R-04, R-06, R-11, R-12, R-13, R-14 | README: tradução para `IndexRatesImport__Enabled`, importação ao subir, ordem de subida, equivalentes em PowerShell, migration alvo e `./efbundle 0`, como ler o erro do bundle, precedência da variável, nome do banco | Releitura do README; o comando PowerShell foi executado (tabela acima) |
| R-05 | Aviso de partida quando a importação está desligada, com o valor recebido: `Importação de cotações desligada (IndexRatesImport:Enabled = '<valor>'). Somente 'true' liga a importação.` | 4 testes novos (Theory de Infrastructure com `false`, `0`, `yes`, vazio e nulo, e o caso ligado sem aviso; 2 ponta a ponta na Api). Falharam antes da correção |
| R-07 | `.http` com as duas portas (5124 e 8080) e instrução para trocar | Releitura do arquivo; variável única mantida |
| R-10 | Resumo da carteira informa posição (id e título) e lembra que a importação pode estar desligada; a projeção individual ganha o lembrete | 3 testes novos de Application; os 2 de mensagem falharam antes da correção |
| R-15 | `paths-ignore: docs/**, *.md`; `workflow_dispatch` com a trava do R-02; laço de espera com prazo real de 60 s; `overwrite: true` no `coverage-report`; `retention-days: 7` no bundle | `actionlint` e `zizmor` limpos; leitura do `release.yml` |
| R-15 (smoke) | O bundle virou job próprio e o smoke aplica o **artefato baixado**, não o arquivo do workspace. Estava na lista de aceitos; foi corrigido de fato | Leitura do `release.yml` |
| R-16 | Só o comentário do provedor, com o que o QA observou. Comportamento intacto | Diff do `SgsIndexRateProvider.cs` |
| R-17 | Sem connection string, a API sai na partida com mensagem clara, exit 1 e sem pilha (vazio ou só espaços também) | `MissingConnectionStringStartupTests` inicia o `FixedIncome.Api.dll` como processo; falhou antes da correção (`Expected: 1, Actual: -532462766`); conferido à mão |
| B-01 a B-03 | Contagem e cobertura do README, texto da F1 na ESPECIFICACAO e tamanho do bundle na spec | Releitura |

Todos os testes de código desta seção foram escritos antes da correção e falharam pelo motivo
certo, conforme o relatório do `implementador`.

### Aceitos, com o motivo no ROADMAP

R-08 e R-09 (tratados no `/health/ready` da F10), R-11 (comportamento do EF, documentado no
README), R-16 (efeito nulo, contorno mantido), CI duas vezes por merge (decisão da spec), tags de
candidatas acumuladas no GHCR (limpeza quando o volume importar) e o digest do `postgres` do smoke,
que o Dependabot não atualiza (troca manual, dita em comentário).

### Limites desta verificação

- O `release.yml` nunca rodou no GitHub. O que só uma execução real prova continua aberto: a tag do
  SHA no GHCR, o smoke no runner movendo `latest` (e reprovando sem movê-la), a ausência de segredo
  nos logs, o `gh api ... commits/main` com o `GITHUB_TOKEN` no runner, e o `overwrite` do
  `coverage-report` num re-run.
- O `zizmor` rodou offline: as auditorias que exigem token da API do GitHub (`impostor-commit`,
  `ref-confusion`, `known-vulnerable-actions`, `stale-action-refs`, `ref-version-mismatch`) foram
  puladas. Os SHAs das ações foram conferidos só por `git ls-remote`.
- A lembrança do R-10 depende do prefixo da mensagem de domínio ("Cotação de CDI ausente"). Os
  testes de Application cobrem com o domínio real, mas renomear essa mensagem no domínio quebra o
  lembrete, e esses testes são o alarme.

Veredito mantido: **APROVADA**, com os três critérios do pipeline abertos até a primeira execução
real.
