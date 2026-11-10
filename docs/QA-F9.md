# QA exploratório — F9: Qualidade (Testcontainers, NetArchTest e Stryker.NET), parte B

Relatório da execução com Docker (parte B). A parte A (QA9-11, QA9-13, QA9-16, QA9-17, QA9-15a) está em `docs/QA-F9-parteA.md`; o orquestrador junta os dois.

## Cabeçalho

| Item | Valor |
|---|---|
| Data | 2026-11-09, a partir de aproximadamente 21h15 UTC |
| Commit-base | `a741bf418681307ae7554fac2f9aab53e0e90556` (branch `f9-qualidade`) **mais a árvore de trabalho** (a fase não está commitada). `git status --short` no início: 49 linhas, 9 não rastreadas: `.github/workflows/mutation.yml`, `docs/QA-F9-ROTEIRO.md`, `docs/QA-F9-parteA.md`, `docs/SPEC-F9-EXECUCAO.md`, `docs/VALIDACAO-F9.md`, `tests/FixedIncome.Architecture.Tests/`, `tests/FixedIncome.Domain.Tests/stryker-config.json`, `tests/FixedIncome.Infrastructure.Tests/Integration/`, `tests/Shared/` |
| Ambiente | Windows 11, Git Bash e Windows PowerShell 5.1.26100. Docker 29.7.2 (Docker Desktop). `dotnet --list-sdks`: `8.0.403` e `8.0.427` (nenhum SDK 9 ou 10; sem `global.json`). Sem `DOCKER_HOST`, `TESTCONTAINERS*`, `ConnectionStrings*`, `ASPNETCORE*`, `DOTNET_ENV` no shell e sem `~/.testcontainers.properties` |
| Cópia | `.../scratchpad/qaf9/clone` (`$C`) e `clone2` (`$C2`), feitas com `git ls-files -co --exclude-standard` (204 arquivos, igual ao repositório), 0 pastas `bin/obj/StrykerOutput/TestResults` na origem; `git init` próprio na cópia |
| Estado antes | Nenhum contêiner. Volumes: 2 anônimos (`3feadb3e...`, `42842a23...`) e `renda-fixa-api_fixedincome-db-data`. Imagens: `cab1c9c07832acr.azurecr.io/renda-fixa-api:v1`, `ghcr.io/zizmorcore/zizmor:1.14.2`, `mcr.microsoft.com/dotnet/aspnet:8.0`, `mcr.microsoft.com/dotnet/sdk:8.0`, `postgres:16`, `postgres:16-alpine`, `renda-fixa-api-api:latest`, `rhysd/actionlint:1.7.7`, `testcontainers/ryuk:0.14.0`. Redes: `bridge`, `host`, `none` |
| Porta 8080 ocupada | Antes de começar, `netstat` mostra a **8080 em LISTENING** pelo processo `httpd` (PID 5252, Apache; `curl http://localhost:8080/health` devolve `404 Server: Apache`). Não é contêiner nem processo do QA; **não foi tocado**. A 5432 estava livre. Isso afeta o QA9-12 (ver lá) |
| Decisões do Matheus aplicadas | Docker Desktop pode ser parado de verdade (03b e c), sem reset; `docker compose` na cópia, projeto `qaf9` (QA9-07 e 12), sem `down -v`; QA9-15b autorizado; momento: árvore de trabalho atual; QA9-05b (Ctrl+C) não executado; casos sem regra na spec entram como RISCO |

Veredito de severidade fica com o orquestrador e o Matheus.

## QA9-01 — README do zero, na cópia limpa: **OK**

Comandos (Git Bash, `$C`): `dotnet tool restore`, `dotnet restore`, `dotnet build`, `dotnet test`, `tc`, `git status --short`. Depois, só os comandos `dotnet` repetidos no Windows PowerShell 5.1.

| Comando | Git Bash | PowerShell 5.1 |
|---|---|---|
| `dotnet tool restore` | 0,5 s (`dotnet-ef` 8.0.11 e `dotnet-stryker` 4.16.0 restaurados) | sem erro |
| `dotnet restore` | 4,4 s | já atualizado |
| `dotnet build` | 9,5 s, `0 Warning(s)`, `0 Error(s)`, nenhum `NU1` | `0 Warning(s)`, `0 Error(s)` |
| `dotnet test` (solution) | **34,2 s**: Domain 332, Application 112, Architecture 7, Infrastructure 148, Api 57 = **656 passados, 0 falhas, 0 ignorados** | 34,7 s (soma de todos os comandos), mesmos números |

- `git status --short` ao fim: vazio nas duas shells (a execução não gerou arquivo fora do `.gitignore`).
- Nenhuma imagem baixada (`docker image ls` antes e depois: sem diferença); todas já existiam.
- Ao fim do `dotnet test` (Git Bash e PowerShell), ainda havia **2 contêineres Ryuk** (`testcontainers/ryuk:0.14.0`, "Up 16 s" a "Up 18 s") e nenhum `postgres:16`; alguns segundos depois a lista com o rótulo `org.testcontainers=true` estava vazia. O tempo de vida do Ryuk (cerca de 10 s) é medido no QA9-04.
- O `dotnet test` da solution (34 s) ficou bem abaixo de 2 minutos; sem RISCO de tempo.

## QA9-02 — Projetos isolados e os que dispensam Docker: **OK**

Comandos: os do roteiro, em `$C` (Git Bash).

| Comando | Resultado |
|---|---|
| `DOCKER_HOST=tcp://127.0.0.1:1 dotnet test tests/FixedIncome.Domain.Tests` | `Passed! 332/332` |
| idem, `Application.Tests` | `Passed! 112/112` |
| idem, `Architecture.Tests` | `Passed! 7/7` |
| `cd tests/FixedIncome.Architecture.Tests && dotnet test` | `Passed! 7/7` (funciona a partir da pasta do projeto) |
| `dotnet test tests/FixedIncome.Infrastructure.Tests` (com Docker) | `Passed! 148/148`, 17,3 s no total (12 s de teste) |
| `dotnet test tests/FixedIncome.Api.Tests` | `Passed! 57/57`, 18,5 s no total (12 s de teste) |
| `--filter "FullyQualifiedName~HolidayRepositoryTests"` | `Passed! 5/5`, 12,1 s no total (a suíte inteira levou 17,3 s) |

- Com o filtro, monitorei `docker ps` por 25 s: **um `postgres:16` e um Ryuk**, nada mais. O filtro sobe o contêiner uma vez (o tempo do filtro é dominado por esse subir, ~12 s).
- Os três projetos "sem Docker" ficam verdes com o cliente Docker apontando para um endereço morto.
- Depois de cada execução de integração sobrou um Ryuk por alguns segundos ("Up 11 s" e "Up 12 s" na leitura imediata); some sozinho (medido no QA9-04).

## QA9-03 — Docker indisponível

### a. Simulado por `DOCKER_HOST` (sem parar o Docker Desktop): **RISCO**

Comandos: o laço do roteiro (`DOCKER_HOST=<h> dotnet test tests/FixedIncome.{Infrastructure,Api}.Tests --no-build`), mais variações. Saídas completas em `scratchpad/qaf9/03a-*.txt` e `03a2-*.txt`.

| `DOCKER_HOST` | Projeto | Exit | Resultado | Mensagem em português | Tempo |
|---|---|---|---|---|---|
| `tcp://127.0.0.1:1` | Infrastructure | **0** | `Passed! 148/148` | n/a | 17 s |
| `tcp://127.0.0.1:1` | Api | **0** | `Passed! 57/57` | n/a | 16 s |
| `npipe:////./pipe/qaf9_nao_existe` (forma do roteiro, 4 barras) | Infrastructure | 1 | `Failed! 58 falhas, 90 passados, 0 pulados` | **0 ocorrências** | 3 s |
| idem | Api | 1 | `Failed! 50 falhas, 7 passados, 0 pulados` | **0 ocorrências** | 2 s |
| `npipe://./pipe/qaf9_nao_existe` (2 barras, forma válida) | Api | 0 | `Passed! 57/57` | n/a | 25 s |
| `unix:///var/run/qaf9.sock` | Api | 0 | `Passed! 57/57` | n/a | 14 s |
| `tcp://10.255.255.1:2375` (endereço que não responde) | Api | 0 | `Passed! 57/57` | n/a | 34 s |

Observado:

1. **Um `DOCKER_HOST` inalcançável não simula "Docker fora"**: o Testcontainers testa o endpoint da variável, vê que não responde e usa o Docker padrão da máquina (npipe do Docker Desktop). Os testes passam. A simulação do roteiro não reproduz o cenário; o cenário real é o 03b. Para quem tem `DOCKER_HOST` apontando para um Docker remoto fora do ar, os testes passam no Docker local sem aviso (comportamento do Testcontainers, não do projeto).
2. **`DOCKER_HOST` malformado** (`npipe:////./pipe/...`, a forma do roteiro): falha de **50 testes da Api (os de integração)** e **58 da Infrastructure**, 0 pulados, exit 1, em 2 a 3 s, sem travar. Mas a **mensagem do README não aparece nenhuma vez** (`grep -c 'precisam do Docker'` = 0). A mensagem de erro é `System.TypeInitializationException : The type initializer for 'DotNet.Testcontainers.Configurations.TestcontainersSettings' threw an exception.` e, como causa, `System.InvalidOperationException : The endpoint is not a npipe URI.`, com ~1.150 a ~1.340 linhas de pilha por execução. O `PostgreSqlFixture` só converte `DockerUnavailableException` na mensagem em português; esta exceção é outra.
3. Os 7 testes de fábrica própria da Api passam e os 90 da Infrastructure que não usam banco passam; nenhum teste de integração foi pulado nem ficou verde em nenhuma variante.

**RISCO**: a promessa do README ("Sem Docker, esses testes falham com a mensagem ...") vale para o Docker parado (ver 03b), mas qualquer outra falha de inicialização do Testcontainers (como um `DOCKER_HOST` malformado) sai com a pilha em inglês e sem a orientação. A spec não define esse caso (decisão do Matheus). Sugestão de teste, se decidir cobrir: `PostgreSqlFixture_com_DOCKER_HOST_malformado_deve_falhar_com_mensagem_orientando_verificar_o_Docker`.

## QA9-08 — Variáveis de ambiente do desenvolvedor: **OK**

Comando: `env <VAR>=<valor> dotnet test --no-build` (solution inteira, em `$C`), uma variável por execução. Saídas em `scratchpad/qaf9/08-*.txt`.

| Caso | Variável | Exit | Resultado | Observação |
|---|---|---|---|---|
| a | `ConnectionStrings__DefaultConnection=Host=qaf9-nao-existe;Database=x;Username=x;Password=QaF9Senha` | 0 | 656 verdes (7/112/332/148/57), 0 pulados | `grep -c QaF9Senha` na saída = **0**. A variável não vence o teste |
| b | `ASPNETCORE_ENVIRONMENT=Production` | 0 | 656 verdes | Api 57/57. Os testes de API continuam verdes em Production |
| c | `DOTNET_ENVIRONMENT=Production` | 0 | 656 verdes | idem |
| d | `TESTCONTAINERS_RYUK_DISABLED=true` | 0 | 656 verdes | Ao fim e 20 s depois, **nenhum contêiner** com o rótulo do Testcontainers (sem Ryuk, o `Dispose` removeu o `postgres:16`) |
| e | `IndexRatesImport__Enabled=true` | 0 | 656 verdes | `grep -ci api.bcb.gov.br` na saída = 0 |

Sem contêiner nem banco sobrando em nenhum dos casos. Nenhum BUG nem RISCO.

## QA9-05 — Execução interrompida

### a. Morte forçada: **OK**

Comandos: os do roteiro em PowerShell (`Start-Process dotnet test tests/FixedIncome.Api.Tests --no-build`, 9 s de espera, `taskkill /PID <pid> /T /F`, `docker ps -a --filter label=org.testcontainers=true` a cada 5 s).

| Variante | Antes de matar (t=9 s) | Depois da morte | Resultado |
|---|---|---|---|
| Árvore inteira (`taskkill /T /F`; 4 processos finalizados: `dotnet`, `VSTest`, `testhost`) | `postgres:16` (Up 5 s) e Ryuk (Up 6 s) | t=5 s e t=10 s: os dois ainda de pé; **t=15 s: nenhum contêiner**, e nada até t=60 s | Ryuk removeu o `postgres:16` e saiu, em até 15 s |
| Só o `testhost` (`Get-Process testhost \| Stop-Process -Force`) | `postgres:16` (Up 4 s) e Ryuk (Up 6 s) | o `dotnet test` pai encerrou sozinho (`The active test run was aborted. Reason: Test host process crashed`, `Test Run Aborted`); t=5 s e t=10 s: contêineres de pé; **t=15 s: nenhum**, e nada até t=40 s | idem |

- Nenhum `testhost` órfão em nenhuma das variantes (`Get-Process testhost` vazio).
- A execução seguinte (`dotnet test tests/FixedIncome.Api.Tests --no-build`) depois de cada variante: `Passed! 57/57`, sem erro de arquivo travado nem de porta.
- Detalhe de saída (não é do projeto, é do VSTest): na variante do `testhost` morto, o log mostra `Test Run Aborted.` seguido de uma linha `Passed! ... Total: 7` (os 7 testes de fábrica própria que chegaram a rodar). Quem lê só a linha `Passed!` vê verde numa execução abortada; o `Test Run Aborted` está logo acima. Registro como informação.

### b. Ctrl+C manual: **NÃO EXECUTADO**

Decisão do Matheus: só a morte forçada.

## QA9-09 — `dotnet stryker` como o README manda, e fora dele: **RISCO**

Saídas em `scratchpad/qaf9/09a.txt`, `09b-*.txt`, `09d.txt`. O README manda `dotnet tool restore` na raiz e `dotnet stryker` em `tests/FixedIncome.Domain.Tests`.

**a. Caminho do README (`cd tests/FixedIncome.Domain.Tests && dotnet stryker`, com `DOCKER_HOST=tcp://127.0.0.1:1`): OK.** Exit 0, 130 s (`Time Elapsed 00:02:08`). 298 mutantes testados: 285 Killed, 11 Timeout, 2 Survived (19 CompileError e 90 Ignored fora da conta). Score impresso: **99,33 %**, igual ao README e à `VALIDACAO-F9.md` (o README vigente diz 99,33 %). Relatórios `mutation-report.html` e `mutation-report.json` em `StrykerOutput/<data>/reports/`. `git status --short` na raiz da cópia: vazio (`StrykerOutput/` ignorado, `.gitignore:503`). Nenhum contêiner Docker foi criado.

**b. Outras pastas** (cada uma com saída em `09b-*.txt`):

| Pasta e comando | Exit | O que aconteceu |
|---|---|---|
| raiz da cópia, `dotnet stryker` | **124** (meu `timeout 600` matou) | O Stryker acha `FixedIncome.sln` e entra no **modo solution**: "Found project ... Domain/Application/Infrastructure ... to mutate", **1.402 mutantes criados, 983 a testar**, rodando as **650 testes de todos os projetos**, incluindo os de integração (apareceram `postgres:16` e Ryuk no `docker ps` durante a execução). Depois de 10 min ainda estava testando ("Retrying the test session"). Nenhum aviso de que está fora do projeto e do `stryker-config.json`. Não vi o fim, então não sei o exit nem o score |
| `tests/`, `dotnet stryker` | 1 | Mensagem clara: `No .csproj or .fsproj file found, please check your project or solution directory at ...\tests` |
| raiz com `--config-file tests/FixedIncome.Domain.Tests/stryker-config.json` | **0** | Funciona, mas leva **9 min 45 s** (contra 2 min no caminho do README), pois roda os **650 testes da solution inteira** a cada mutante do Domain (incluindo os de integração, que sobem contêineres). Score 99,33 %. A frase do README "não precisa de Docker" só vale na pasta certa |
| `src/FixedIncome.Domain`, `dotnet stryker` | 1 | `can't be mutated because no test project references it. If this is a test project, ensure it has the property: <IsTestProject>true</IsTestProject>` e `Failed to analyze project builds. Stryker cannot continue.` A mensagem não diz para rodar em `tests/FixedIncome.Domain.Tests` |

**c. Sem `dotnet tool restore`: parcialmente executado.** O `dotnet tool list` na segunda cópia (nunca restaurada) já lista `dotnet-stryker 4.16.0`, porque o pacote está no cache global de NuGet do usuário desta máquina; `dotnet stryker` roda sem restaurar. Reproduzir uma máquina sem o pacote exigiria limpar o cache de NuGet do Matheus: **NÃO EXECUTADO**.

**d. Execução repetida: OK.** Mesma pasta, segundo `dotnet stryker`: exit 0, 122 s, **99,33 %** de novo, pasta datada nova (`2026-11-09.18-56-21` ao lado da `18-32-14`). `du -sh StrykerOutput` = 2,3 MB por execução (4,5 MB com duas).

**e. Contexto do `docker build`:** `git check-ignore -v` → `.gitignore:503:StrykerOutput/`. O `.dockerignore` **não** cita `StrykerOutput` (só `**/bin`, `**/obj`, `.git`, `.vs`, `.vscode`, `**/TestResults`, `**/*.user`, `artifacts/`, `efbundle`). O `Dockerfile` só copia `src/`, então o efeito é só o tamanho do contexto (2 a 3 MB por execução, mais a pasta `StrykerOutput/` da raiz quando se roda da raiz).

Classificação:

- **RISCO R9-01**: `dotnet stryker` fora de `tests/FixedIncome.Domain.Tests` não falha, nem avisa: da raiz, vira uma execução da solution inteira, com Docker, que passou de 10 min (e que não usa o `break` do arquivo, que fica na pasta do Domain). Com `--config-file` funciona, mas em 9 min 45 s. O README e o `CLAUDE.md` mandam rodar na pasta certa; a spec não define o comportamento nas outras. Sugestão de teste, se o Matheus quiser protegê-lo: nenhum teste automatizado cabe aqui; seria um `stryker-config.json` na raiz ou um aviso no README.
- **RISCO R9-02**: `StrykerOutput/` fora do `.dockerignore` (só tamanho do contexto) e pastas datadas acumulando sem o README sugerir limpeza.

## QA9-10 — O limite de 80% reprova de verdade: **OK**

Sempre em `tests/FixedIncome.Domain.Tests` da cópia. Saídas em `10a*.txt`, `10b.txt`, `10c.txt`.

| Caso | Comando | Exit | Mensagem | Relatório |
|---|---|---|---|---|
| a1 | `dotnet stryker --break-at 100` | 1, em 1 s | `Threshold low must be more than or equal to threshold break. Current low: 80, break: 100.` (erro de configuração, não roda) | não |
| a2 | `--break-at 100 --threshold-low 100 --threshold-high 100` (o `--break-at` só aceita inteiro: `99.5` dá `Expected type to be integer`) | **2**, 110 s | `Final mutation score is below threshold break. Crashing...` e `The mutation score is lower than the configured break threshold of 100%.`, com o score impresso antes (99,33 %) | **sim** (`mutation-report.html` e `.json` em `StrykerOutput/2026-11-09.19-00-48/reports/`) |
| b | `"break": 95` no `stryker-config.json` (sed do roteiro) | 1, em 1 s | `Threshold low must be more than or equal to threshold break. Current low: 80, break: 95.` | não |
| c | tirar `Entities/Position{,Cdi,Iof,Ipca}*Tests.cs` (4 arquivos) | **2**, 77 s | `The final mutation score is 74.16 %`, `Final mutation score is below threshold break. Crashing...`, `The mutation score is lower than the configured break threshold of 80%.` | **sim** (`StrykerOutput/2026-11-09.19-03-02/reports/`) |

Os arquivos do caso c e o `stryker-config.json` do caso b foram **devolvidos** (`git status --short` da cópia vazio depois; `ls Entities` volta a ter os 7 arquivos). O limite de 80% reprova com exit diferente de 0, a mensagem diz o limite e o score é impresso na linha acima; o relatório sai mesmo quando reprova (é o que o `mutation.yml` publica com `if: always()`).

## QA9-07 — Convivência com o `docker compose`: **OK**

Comandos: os do roteiro, em `$C`, projeto `qaf9` (`docker compose -p qaf9 up -d db`; antes, `docker ps -a --filter name=fixedincome` vazio e a 5432 livre). Contêiner `fixedincome-db` (`postgres:16-alpine`) de pé na 5432 durante os casos a e b. Bancos do Compose antes: `fixedincome postgres template0 template1`.

| Caso | Resultado |
|---|---|
| a. `dotnet test --no-build` com o Compose ocupando a 5432 | 656 verdes (7/112/332/148/57), 0 pulados, 29,5 s |
| b. idem, com `ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=fixedincome;Username=fixedincome;Password=fixedincome"` apontando para o Compose | 656 verdes, 0 pulados |
| Estado do banco do Compose depois de a e b | `select datname from pg_database` **idêntico** ao de antes (`diff` vazio: nenhum `test_*`, nenhum `template_fixedincome`); `\dt` em `fixedincome`: `Did not find any relations` (nenhuma tabela, nenhuma migration aplicada) |
| c. Compose parado; `dotnet test tests/FixedIncome.Api.Tests --no-build` em segundo plano; 8 s depois `docker compose -p qaf9 up -d db` | durante: `postgres:16` (o do Testcontainers, porta aleatória), Ryuk e `fixedincome-db` `Exited`; o Compose sobe normalmente na 5432; `Passed! 57/57`; bancos do Compose idênticos aos de antes |

Ao fim: `docker compose -p qaf9 stop` (contêiner `fixedincome-db` `Exited (0)`; rede `qaf9_default` e volume `qaf9_fixedincome-db-data` mantidos para o QA9-12).

Intercorrência do QA (não é achado): na primeira execução do caso a, o Domain.Tests mostrou **254** testes em vez de 332. Era o binário velho: a Stryker do caso QA9-10 c compilou a cópia sem os 4 arquivos `Position*Tests.cs`, e eu rodei com `--no-build` sem recompilar depois de devolver os arquivos. Rodei `dotnet build` (0 warning) e repeti a e b; os números acima são da repetição. O `dotnet test` sem `--no-build` não tem esse problema.

## QA9-12 — A API pelo caminho do usuário final: **OK** (com RISCO de ambiente)

Comandos: os do roteiro, projeto `qaf9`, a partir de `$C`. Nenhum contêiner `fixedincome-*` existia; a 5432 estava livre, **mas a 8080 estava ocupada pelo Apache (`httpd`, PID 5252) desde antes do QA**.

- `docker compose -p qaf9 up --build -d`: exit 0, 32 s (build da imagem `qaf9-api` incluso). **O Compose subiu a API na 8080 mesmo com o Apache escutando lá**: o Windows deixou os dois escutarem em `0.0.0.0:8080`. Resultado prático: `curl http://localhost:8080/health` (que resolve para `::1`) respondeu `200`, `Server: Kestrel`; `curl http://127.0.0.1:8080/health` respondeu `404`, `Server: Apache`. Ou seja, quem acessa pelo IPv4 caiu no Apache, sem erro de porta. Todas as chamadas abaixo foram feitas por `localhost` e conferi `Server: Kestrel`. **RISCO R9-03 (ambiente)**: o README não diz isso, mas é da máquina do Matheus (um Apache na 8080), não do projeto.
- Migration: o `docker run ... -v "$(pwd -W):/src"` do roteiro escreveria `bin/obj` Linux na cópia `$C` (e estragaria os `--no-build` seguintes). Rodei o **mesmo comando** numa cópia à parte (`scratchpad/qaf9/clone-linux`, mesmo conteúdo): `dotnet tool restore && dotnet ef database update ...` com `--network container:fixedincome-db`, exit 0, **1 min 25 s** (o README avisa "alguns minutos"). Aplicou `20260818070004_InitialCreate` e `20261030222641_AddHolidaysAndIndexRates`, `Done.`
- `docker compose -p qaf9 restart api`; `/health` voltou. O log da API mostra a importação do Banco Central: `1719 de CDI, 0 de CDI descartadas e 81 de IPCA`.

| Requisição | Resposta |
|---|---|
| `GET /health` | `Healthy` 200 |
| `GET /api/assets` | 200, `data: []` |
| `POST /api/assets` (`QA-F9 PRE12`, CDB PreFixed 12%, 2026-01-01 a 2028-01-01) | **201**, id `bdab7bf3-c92e-489b-be6c-ea17ffc14ee2` |
| `POST /api/positions` (R$ 1.000, 2026-09-01) | **201**, id `097d1bfa-85ac-497d-8e5f-7c6ff2f20311` |
| `GET /api/positions/{id}/projection?referenceDate=2026-10-01` | 200: `grossAmount 1009.49`, `grossYield 9.49`, `taxRate 22.5`, `taxAmount 2.14`, `netAmount 1007.35`, `elapsedDays 30`, `businessDays 21`, `iofRate 0` (**idêntico ao QA-F8**, QA8-03) |
| `GET /api/portfolio/summary?referenceDate=2026-10-01` | 200: `totalGrossAmount 1009.49`, `totalNetAmount 1007.35`, `positionCount 1`, `byAssetType` Cdb 100% |
| `GET /api/indexes/Cdi?from=2026-09-01&to=2026-09-10` | 200, cotações de CDI importadas |
| `GET /swagger/index.html` | 200 |

Nenhum 500; nenhuma diferença de comportamento em relação à F8. Ao fim: `docker compose -p qaf9 down` (sem `-v`): contêineres e rede `qaf9_default` removidos; **sobraram o volume `qaf9_fixedincome-db-data` e a imagem `qaf9-api:latest`** (para o Matheus remover; há também as linhas de dados `QA-` dentro desse volume).

## QA9-04 — Limpeza depois de uma execução normal: **OK**

Comando: `dotnet test --no-build` na solução em segundo plano e um monitor que a cada ~1 s lista `docker ps -a --filter label=org.testcontainers=true` e, em cada `postgres:16`, conta os bancos `test_*` e o `template_fixedincome` (`psql` no `pg_database`). Saída em `scratchpad/qaf9/04b-monitor.txt` (a primeira tentativa, `04-monitor.txt`, amostrou devagar demais porque o `docker exec` em cada passada levava vários segundos; descartada).

| t (s) | Observado |
|---|---|
| 9 a 11 | 2 Ryuk (`Created`, depois `Up`) |
| 14 a 29 | **2 `postgres:16`** (um por assembly de integração, Infrastructure e Api) e 2 Ryuk. Bancos `test_*` por contêiner: **0 ou 1** (nunca mais que 1 em 8 amostras), `template_fixedincome`: 1 |
| 30 | `dotnet test` termina (656 verdes); os dois `postgres:16` já removidos |
| 31 a 39 | 2 Ryuk ainda de pé |
| 40 | 1 Ryuk |
| 41 | nenhum contêiner |

- O Ryuk sobra por **cerca de 10 s** depois do fim da execução e sai sozinho (RISCO esperado do desenho do Ryuk, registrado; sem efeito).
- O roteiro esperava "quatro no total" contando `postgres:16` e Ryuk; são dois assemblies de integração, logo 2 `postgres:16` + 2 Ryuk.
- Os bancos `test_*` não se acumulam (um por vez por contêiner, testes da coleção em série). Depois de 15 s: 0 contêineres do Testcontainers, nenhum volume novo além de `qaf9_fixedincome-db-data` (do QA9-12), nenhuma rede nova.

## QA9-06 — Duas execuções ao mesmo tempo: **OK**, com RISCO de tempo

Comandos: `dotnet build -v q` em `$C` e `$C2`; depois `dotnet test --no-build` nas duas ao mesmo tempo (monitor de `docker ps` a cada ~1 s); depois a variação, duas execuções de `Infrastructure.Tests` na mesma pasta (`$C`) ao mesmo tempo. Saídas em `06-*.txt`.

- **Duas pastas:** as duas verdes, **656 passados, 0 falhas, 0 pulados** cada (Api 57, Infrastructure 148, Domain 332, Application 112, Architecture 7). Durante: até **4 `postgres:16`** com portas de host diferentes (54423, 54424, 54428, 54429) e **3 a 4 Ryuk** (portas 54413, 54414, 54422, 54425; um por processo). Nenhuma falha por porta em uso, banco de outra execução ou contêiner removido pelo Ryuk da outra. Ao fim, nenhum contêiner do Testcontainers sobrando (conferido depois do Ryuk sair).
- **Mesma pasta, Infrastructure duas vezes:** as duas `Passed! 148/148`, 58 s cada. Sem erro de arquivo travado em `TestResults`.
- **RISCO de tempo (anotado, como o roteiro pede):** isoladas, a solução leva 34 s (Api 12 a 16 s, Infrastructure 12 a 19 s). Duas ao mesmo tempo levaram **134 s e 132 s** (cerca de 4 vezes, não 2) e o `Api.Tests` mediu **1 min 31 s e 1 min 38 s** (o teto citado na SPEC-F9 é 60 s para `Api.Tests`); o `Infrastructure.Tests`, duas vezes na mesma pasta, 58 s cada (contra 12 a 19 s isolado). Dois contêineres `postgres:16` por execução em disputa de CPU e disco explicam a diferença; é custo, não erro.
- Intercorrência: o `dotnet build -v q` da cópia `$C2` imprimiu `1 Warning(s)`. Um `dotnet build --no-incremental` posterior na mesma cópia deu `0 Warning(s)` e não reproduzi; **não sei qual era o aviso** (o filtro do roteiro só imprime o total). Registro como observação, sem classificação.

## QA9-14 — Três execuções seguidas e sob carga: **OK**

Comandos: o roteiro, com uma diferença: o Stryker de carga rodou numa **terceira cópia** (`clone3`, compilada), porque o Stryker e o `dotnet test` da mesma cópia disputariam `obj/`; a suíte completa de carga rodou em `$C2`. Saídas em `14-sem-carga.txt`, `14-com-carga.txt`, `14-c2-carga.txt`, `14-stryker-carga.txt`. `/usr/bin/time` substituído por medição com `date`.

| Rodada | Sem carga: Infrastructure (total / teste) | Sem carga: Api | Com carga: Infrastructure | Com carga: Api |
|---|---|---|---|---|
| 1 | 21 s / 18 s, 148 verdes | 23 s / 17 s, 57 verdes | 49 s / 42 s, 148 verdes | 31 s / 24 s, 57 verdes |
| 2 | 17 s / 14 s, 148 verdes | 17 s / 14 s, 57 verdes | 32 s / 27 s, 148 verdes | 32 s / 24 s, 57 verdes |
| 3 | 16 s / 14 s, 148 verdes | 17 s / 14 s, 57 verdes | 37 s / 32 s, 148 verdes | 31 s / 26 s, 57 verdes |

- **12 de 12 execuções verdes, 0 `[FAIL]`**, sem carga e com carga (Stryker rodando numa cópia e uma suíte completa noutra: a suíte de carga também terminou verde, 656 testes; o Stryker fechou em 99,33 %).
- `Api.Tests` abaixo do teto de 60 s da SPEC-F9 em todas as rodadas (máximo 26 s de teste, 32 s totais, sob carga).
- Nenhuma falha dos testes de espera por tempo (`Job_de_importacao_inteiro_grava_as_cotacoes_...`, `Upserts_concorrentes_...`); por isso não rodei os 10 isolados do roteiro. Sem `40P01` nem violação de índice único.
- Nenhum contêiner do Testcontainers sobrando ao fim.

## QA9-15b — Suíte completa em Linux: **OK**

A parte a (fusos) está na parte A. Comando (autorizado): `docker run --rm -v //var/run/docker.sock:/var/run/docker.sock -e TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal -v "<cópia clone-linux>:/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 dotnet test`, numa cópia à parte (`scratchpad/qaf9/clone-linux`, conteúdo igual ao de `$C`).

Resultado: exit 0, 219 s no total (inclui restore e build no contêiner). Domain 332, Application 112, Architecture 7, Infrastructure 148 (`1 m 3 s`), Api 57 (`56 s`): **656 verdes, 0 falhas, 0 pulados**. Os testes de integração subiram `postgres:16` e Ryuk no Docker do host e conectaram por `host.docker.internal`. Depois de 15 s, nenhum contêiner (o `--rm` removeu o do SDK; o Ryuk e os `postgres:16` já tinham saído).

Informação: em Linux dentro do contêiner, `Api.Tests` levou 56 s de teste (perto do teto de 60 s da SPEC-F9) e `Infrastructure.Tests` 1 min 3 s, contra 12 a 20 s no Windows sem contêiner aninhado. Na execução única; não repeti.

## QA9-03 (continuação) — Docker Desktop parado de verdade

### b. Docker Desktop parado: **OK** (RISCO de ruído)

Antes: nenhum contêiner (`docker ps -a` vazio; os do Matheus não existiam). Comando de parada: `docker desktop stop` (CLI oficial do Docker Desktop, equivalente a "Quit"; sem reset nem limpeza): `Stopping Docker Desktop`, exit 0, 15 s. `docker info`/`docker ps` passaram a falhar: `failed to connect to the docker API at npipe:////./pipe/dockerDesktopLinuxEngine ... The system cannot find the file specified`. Em seguida, **sem `DOCKER_HOST`**, `dotnet test --no-build` nos dois projetos (saídas em `03b-Infrastructure.txt` e `03b-Api.txt`):

| Projeto | Exit | Resultado | Tempo até falhar | Mensagem do README |
|---|---|---|---|---|
| Infrastructure | 1 | `Failed! 58 falhas, 90 passados, 0 pulados` | 13 s (11 s de teste) | **58 ocorrências**, uma por teste que usa banco |
| Api | 1 | `Failed! 50 falhas, 7 passados, 0 pulados` | 12 s (10 s de teste) | **50 ocorrências**, uma por teste de integração |

- Bate com o esperado da validação (58 e 50 falhando, 90 e 7 passando). **Nenhum teste de integração foi pulado nem ficou verde**; o processo não travou.
- A mensagem em português é a primeira linha de cada `Error Message:` (`System.InvalidOperationException : Os testes de integração precisam do Docker em execução. Inicie o Docker e rode os testes de novo.`), idêntica ao texto do README, com acentos, seguida da causa `DockerUnavailableException ... Docker is either not running or misconfigured`. A frase completa foi conferida com `grep -F` contra as duas saídas e contra o `PostgreSqlFixture.cs`, e contra o README com as quebras de linha juntadas (o README a quebra em duas linhas): casa caractere a caractere (acentos incluídos).
- **RISCO de ruído (baixo):** a saída tem 2.059 linhas (Api) e 2.387 (Infrastructure): 50 e 58 repetições da mensagem, cada uma com a pilha. A primeira mensagem útil só aparece na linha 17 da saída da Api, depois da lista de `[FAIL]`. A spec não define o formato da saída; decisão do Matheus.
- Docker Desktop reaberto com `docker desktop start` (12 s) e `docker info` respondeu em 17 s: `Server Version: 29.7.2`, `Containers: 0`. Volumes, imagens e redes idênticos aos de antes (exceto os do `qaf9`, listados no fim). `dotnet test tests/FixedIncome.Api.Tests --no-build` depois da volta: `Passed! 57/57`.

### c. Engine pausada (menu "Pause"): **NÃO EXECUTADO**

O Docker Desktop 29.7.2 não tem comando de pausa na linha de comando (`docker desktop --help` lista só `diagnose, disable, enable, engine, kubernetes, logs, restart, start, status, stop, update, version`) e eu não automatizo o clique no menu da bandeja. Não simulei com outro artifício para não afirmar o que não observei. Se o Matheus quiser, o caso fica para ele: pausar pelo menu e rodar `dotnet test tests/FixedIncome.Api.Tests --no-build`, para ver se falha com a mesma mensagem ou fica esperando.

## Dados criados durante o QA

- Banco do projeto `qaf9` (volume `qaf9_fixedincome-db-data`, contêiner já removido): título `QA-F9 PRE12`, id `bdab7bf3-c92e-489b-be6c-ea17ffc14ee2`; aporte `097d1bfa-85ac-497d-8e5f-7c6ff2f20311`; cotações de CDI e IPCA importadas do Banco Central. O banco do Matheus (`renda-fixa-api_fixedincome-db-data`) não foi tocado; os contêineres dele não existiam durante o QA.
- Nenhum outro dado `QA-`. Os testes automatizados usam bancos próprios dentro de contêineres descartáveis.

## Estado final do ambiente (limpeza ao fim)

| Item | Antes | Depois | Observação |
|---|---|---|---|
| Contêineres (`docker ps -a`) | nenhum | **nenhum** | `docker compose -p qaf9 down` sem `-v`; nenhum do Testcontainers sobrando |
| Volumes | `3feadb3e...`, `42842a23...` (anônimos), `renda-fixa-api_fixedincome-db-data` | os mesmos **mais `qaf9_fixedincome-db-data`** | O Matheus remove com `docker volume rm qaf9_fixedincome-db-data` |
| Imagens | as 9 da foto inicial | as mesmas **mais `qaf9-api:latest`** | O Matheus remove com `docker image rm qaf9-api`. (Cache de build do BuildKit do `up --build` não é listado como imagem; não medi) |
| Redes | `bridge`, `host`, `none` | as mesmas | `qaf9_default` removida pelo `down` |
| Repositório do Matheus | `git status --short` de 48 linhas | igual, **mais `docs/QA-F9.md`** | Nada compilado nem alterado nele |
| Docker Desktop | rodando | **rodando** (parado e reaberto no QA9-03b; `docker info` OK) | |
| Porta 8080 | Apache (`httpd`, PID 5252) | igual | Não tocado |
| Cópias no scratchpad | | `qaf9/clone`, `clone2`, `clone3`, `clone-linux` | Podem ser apagadas pelo Matheus ou pelo orquestrador (são do QA) |

## Resumo

| Roteiro | Veredito | Achados |
|---|---|---|
| Preparação, QA9-01 | OK | nenhum (656 verdes, 0 warnings, 34 s, `git status` vazio) |
| QA9-02 | OK | nenhum |
| QA9-03a | RISCO | R9-04 (`DOCKER_HOST` inalcançável não simula Docker fora; malformado: 108 falhas sem a mensagem em português) |
| QA9-03b | OK, com RISCO de ruído | R9-05 (2.000 linhas de pilha, 50 a 58 repetições) |
| QA9-03c | NÃO EXECUTADO | sem CLI de pausa |
| QA9-04 | OK | nenhum (Ryuk sobra ~10 s) |
| QA9-05a | OK | nenhum; 05b NÃO EXECUTADO por decisão |
| QA9-06 | OK, com RISCO de tempo | R9-06 (execuções paralelas ~4x mais lentas; Api 1 m 31 s a 1 m 38 s) |
| QA9-07 | OK | nenhum |
| QA9-08 | OK | nenhum |
| QA9-09 | RISCO | R9-01, R9-02 (c parcialmente NÃO EXECUTADO) |
| QA9-10 | OK | nenhum |
| QA9-12 | OK, com RISCO de ambiente | R9-03 |
| QA9-14 | OK | nenhum |
| QA9-15b | OK | nenhum |

## Lista de bugs

**Nenhum BUG.** Sem teste vermelho na cópia limpa, sem 500, sem contêiner, volume ou banco sobrando por causa dos testes, sem teste que escreva no banco do Compose, sem flaky em 12 execuções.

## Riscos (decisão do Matheus; a spec não define)

- **R9-01** `dotnet stryker` fora de `tests/FixedIncome.Domain.Tests`: da raiz vira a solução inteira (983 mutantes, 650 testes com Docker, mais de 10 min, sem usar o `break`); com `--config-file` roda em 9 min 45 s. De `tests/` e de `src/FixedIncome.Domain` falha com mensagem que não diz onde rodar.
- **R9-02** `StrykerOutput/` fora do `.dockerignore` (2,3 MB por execução no contexto do `docker build`; efeito só no tamanho) e sem aviso de limpeza no README.
- **R9-03** Porta 8080 da máquina do Matheus com um Apache escutando em IPv4: o Compose sobe a API sem erro, mas `127.0.0.1:8080` cai no Apache e `localhost` (IPv6) no Kestrel. Ambiente, não projeto.
- **R9-04** `PostgreSqlFixture` só converte `DockerUnavailableException`: outra falha de inicialização do Testcontainers (como `DOCKER_HOST` malformado) sai com a pilha em inglês e sem a mensagem do README. Um `DOCKER_HOST` inalcançável cai no Docker padrão sem aviso.
- **R9-05** Com o Docker parado, a mensagem correta aparece, mas repetida 50 e 58 vezes, cada vez com pilha (2.000 linhas).
- **R9-06** Duas execuções da solução ao mesmo tempo levam ~4x mais; `Api.Tests` passa de 60 s (1 m 31 s a 1 m 38 s). Em Linux aninhado (QA9-15b), 56 s de teste.
- Herdados da parte A: ordem do Architecture no `ci.yml`, `paths-ignore`, `schedule` só na `main`, SDK do runner (ver `QA-F9-parteA.md`).

## NÃO EXECUTADO

- QA9-03c (pausa da engine): sem comando de pausa na CLI do Docker Desktop.
- QA9-05b (Ctrl+C manual): decisão do Matheus.
- QA9-09c (sem o pacote do Stryker no cache de NuGet): exigiria limpar o cache do Matheus.

**Veredito: SEM BUGS BLOQUEANTES.**
