# QA exploratório — F9 (parte A, sem parar o Docker nem usar `docker compose`)

Relatório parcial. Cobre QA9-11, QA9-13, QA9-16, QA9-17 e QA9-15a. A parte B (Docker de verdade, Stryker, jornada do README, interrupção, concorrência, flaky, compose) é outra execução; o orquestrador junta os dois.

## Cabeçalho

| Item | Valor |
|---|---|
| Data | 2026-11-09 |
| Commit-base | `a741bf418681307ae7554fac2f9aab53e0e90556` (branch `f9-qualidade`) **mais a árvore de trabalho** (a fase não está commitada). `git status --short` no início: 48 linhas, 8 não rastreadas: `.github/workflows/mutation.yml`, `docs/QA-F9-ROTEIRO.md`, `docs/SPEC-F9-EXECUCAO.md`, `docs/VALIDACAO-F9.md`, `tests/FixedIncome.Architecture.Tests/`, `tests/FixedIncome.Domain.Tests/stryker-config.json`, `tests/FixedIncome.Infrastructure.Tests/Integration/`, `tests/Shared/` |
| Ambiente | Windows 11, Git Bash. Docker 29.7.2 (Docker Desktop, não parado nem pausado). `dotnet --list-sdks`: `8.0.403` e `8.0.427` (nenhum SDK 9 ou 10; sem `global.json`). Sem variáveis `DOCKER_HOST`, `TESTCONTAINERS*`, `ConnectionStrings*`, `ASPNETCORE*`, `DOTNET_ENV` no shell |
| Método | Leituras no repositório do Matheus (só leitura). Tudo que compila ou gera arquivo roda em cópias descartáveis no scratchpad (`.../scratchpad/qaf9/`), feitas com `git ls-files -co --exclude-standard`. Código de `src/` e `tests/` só aberto para explicar achado já reproduzido |
| Decisões do Matheus aplicadas | `zizmor` autorizado (QA9-11) sobre a árvore de trabalho atual; casos sem regra na spec entram como RISCO; Docker não parado nem pausado; sem `docker compose`; QA9-15a com `docker run --rm` efêmero e sem portas |

Veredito de severidade fica com o orquestrador e o Matheus.

## QA9-16 — O que precisa entrar no commit: **OK**

Comandos (no repositório do Matheus, só leitura):

```bash
git ls-files -o --exclude-standard
git check-ignore -v tests/FixedIncome.Domain.Tests/stryker-config.json tests/Shared/PostgreSqlFixture.cs \
  .github/workflows/mutation.yml tests/FixedIncome.Architecture.Tests/FixedIncome.Architecture.Tests.csproj
git ls-files -oi --exclude-standard | grep -vE '(^|/)(bin|obj|TestResults|StrykerOutput|graphify-out)/'
grep -n 'Shared' tests/*/*.csproj
```

Observado:

- Não rastreados que um `git add -A` incluiria (9): `.github/workflows/mutation.yml`, `docs/QA-F9-ROTEIRO.md`, `docs/SPEC-F9-EXECUCAO.md`, `docs/VALIDACAO-F9.md`, `tests/FixedIncome.Architecture.Tests/FixedIncome.Architecture.Tests.csproj`, `tests/FixedIncome.Architecture.Tests/LayerDependencyTests.cs`, `tests/FixedIncome.Domain.Tests/stryker-config.json`, `tests/FixedIncome.Infrastructure.Tests/Integration/IndexRatesImportIntegrationTests.cs`, `tests/Shared/PostgreSqlFixture.cs`.
- `git check-ignore -v` nos quatro caminhos: sem saída (`nenhum ignorado`).
- Ignorados fora de `bin/obj/TestResults/StrykerOutput/graphify-out`: só `.claude/settings.json.graphify-bak` (resíduo do graphify, sem relação com o build).
- `tests/Shared/PostgreSqlFixture.cs` é ligado por `Compile Include="..\Shared\PostgreSqlFixture.cs"` nos `.csproj` de Api.Tests (linha 26) e Infrastructure.Tests (linha 25).
- `FixedIncome.sln` está modificado (rastreado) e `.config/dotnet-tools.json` também; ambos entram por `git add -A`.

Nenhum arquivo necessário ao build ou ao CI está ignorado. A prova de que a cópia só com esses arquivos compila e passa é o QA9-01 (parte B); o QA9-15a abaixo já usou uma cópia assim.

## QA9-11 — `ci.yml`, `release.yml` e `mutation.yml`: **RISCO** (nenhum BUG)

Comandos:

```bash
docker run --rm -v "$(pwd -W):/repo" -w /repo rhysd/actionlint:1.7.7        # no repo, só leitura
docker run --rm -v "<cópia de .github>:/repo" -w /repo ghcr.io/zizmorcore/zizmor:1.14.2 --offline --persona=pedantic .github
```

- `actionlint` 1.7.7: saída vazia, **exit 0** (os três workflows, árvore de trabalho atual).
- `zizmor` 1.14.2 (autorizado), offline e `--persona=pedantic`, sobre cópia de `.github` no scratchpad, árvore de trabalho atual (HEAD `a741bf4` mais árvore de trabalho): `No findings to report. Good job!`, **exit 0**. As auditorias que exigem token da API do GitHub foram puladas (`ref-version-mismatch` e as demais), como na validação.

| # | Veredito | Evidência |
|---|---|---|
| a | **OK** | `release.yml`, job `ci`: `uses: ./.github/workflows/ci.yml`. É o mesmo arquivo, então o passo "Testes de Arquitetura" e os testes de integração (Infrastructure e Api) valem também para o release. Os testes só precisam do socket do Docker do runner; `contents: read` basta (nenhum passo faz push nem usa token). `bundle` e `release` encadeiam por `needs` e só rodam com o CI verde |
| b | **RISCO** (ordem) | `ci.yml`: Domain, Application, Infrastructure, Api e só então "Testes de Arquitetura" (último). Uma violação de camada só aparece depois dos dois passos com Docker, e uma falha de Docker (`toomanyrequests`, comentada no próprio arquivo) interrompe o job antes da arquitetura rodar (os passos de teste não têm `if: always()`). RISCO por decisão do Matheus (a spec não define a ordem) |
| c | **RISCO** (custo) | `paths-ignore` do `release.yml`: `docs/**` e `*.md`. O `*.md` do GitHub não atravessa `/`, então cobre só `.md` na raiz (README, CLAUDE.md); `docs/**` cobre a pasta. Um commit só em `tests/**`, `tests/FixedIncome.Domain.Tests/stryker-config.json` ou `.github/workflows/mutation.yml` não casa com padrão nenhum e gera release (CI + bundle + imagem + smoke) sem mudar a imagem. No sentido oposto, `ci.yml` não tem `paths-ignore`: commit só em `docs/` roda o CI (agora com Docker). Em todo push na `main` o CI roda duas vezes (pelo trigger `push` do `ci.yml` e chamado pelo `release.yml`). Custo, não erro; RISCO por decisão do Matheus |
| d | **OK** | `mutation.yml` referencia `tests/FixedIncome.Domain.Tests` (existe) e, por `dotnet stryker` no `working-directory`, o `stryker-config.json` ao lado (existe, não rastreado, não ignorado; entra em `git add -A`, ver QA9-16). A versão do Stryker vem de `.config/dotnet-tools.json` (rastreado). `StrykerOutput/` está no `.gitignore` linha 503 |
| e | **OK** (por leitura) | `mutation.yml` sem `concurrency`; o artefato `stryker-report` tem nome fixo com `overwrite: true`, e artefatos do upload-artifact v4 são por execução. Duas execuções seguidas não conflitam. Não executado no GitHub |
| f | **OK** (por leitura) | `upload-artifact` com `if: always()` e `path: tests/FixedIncome.Domain.Tests/StrykerOutput`; se o restore ou o build falhar, a pasta não existe, o padrão `if-no-files-found: warn` emite aviso e o job fica vermelho pelo passo anterior. Coerente com a spec. Não executado no GitHub |
| g | **RISCO** | `schedule` só dispara a partir da branch padrão. O README (seção Qualidade) diz "roda o mesmo comando toda segunda-feira às 06:00 UTC" sem avisar que isso só vale depois do merge na `main`. O cron `0 6 * * 1` bate com "06:00 UTC (03:00 em Brasília)" |
| h | **RISCO** (só execução real confirma) | Nenhum `global.json`; o `setup-dotnet` com `8.0.x` instala o 8, mas o `ubuntu-latest` traz SDKs mais novos e o `dotnet` sem `global.json` usa o mais novo. Nesta máquina há só `8.0.403` e `8.0.427`, então não observei o comportamento com SDK 9 ou 10. `dotnet-stryker` 4.16.0 e `dotnet-ef` 8.0.11 estão com `rollForward: false` |
| i | Informação | Smoke do `release.yml`: `postgres:16@sha256:f9a7b9eb...` (digest fixo); os testes usam `postgres:16` sem digest (decisão da SPEC-F9, linhas 117-120) |
| j | Informação | Pulls anônimos do Docker Hub por push na `main`: `ci.yml` direto (postgres:16 e Ryuk) + `ci.yml` chamado pelo release (idem) + `postgres:16` do smoke, cerca de cinco pulls em runners distintos. O `ci.yml` já comenta o `toomanyrequests` |

O `release.yml` não mudou na F9, mas foi incluído na varredura.

## QA9-13 — Coerência da documentação: **RISCO** (nenhum BUG)

Comandos: `grep` por números, versões e mensagem em `README.md`, `CLAUDE.md`, `docs/ESPECIFICACAO.md`, `docs/ROADMAP.md`, `docs/SPEC-F9-EXECUCAO.md`, `docs/VALIDACAO-F9.md` e nos `.csproj`; script de links do roteiro.

| Ponto | Resultado |
|---|---|
| Contagem de testes | **OK.** README linha 464 "656 testes"; tabela Domain 332 + Application 112 + Infrastructure 148 + Api 57 + Architecture 7 = 656. Igual a `VALIDACAO-F9.md` linhas 25 e 40. O comentário `<!-- cobertura F9: atualizar ... -->` não existe mais. A contagem real de Domain, Application e Architecture está no QA9-15a; Infrastructure e Api ficam para a parte B |
| Mutation score | **OK nos textos.** README linha 528 "99,33%" = `VALIDACAO-F9.md` "99,33%" (296/298). Não há mais "99,66%". "Partida 82,76%" (README linha 530): só o README a afirma; a `VALIDACAO-F9.md` não traz esse número. A medição nova do score é da parte B (QA9-09) |
| Versões | **OK.** `Testcontainers.PostgreSql 4.16.0` (Api.Tests e Infrastructure.Tests), `NetArchTest.Rules 1.3.2` (Architecture.Tests), `dotnet-stryker 4.16.0` (`.config/dotnet-tools.json`), iguais ao README e à validação. Ryuk `0.14.0` não aparece em documento algum (informação) |
| Docker | **RISCO** (baixo). A seção "Testes" (README 466) diz em negrito "Docker precisa estar em execução"; "Com Docker, sem instalar nada" (254) diz "Requer apenas Docker" (para subir a API) e "Localmente, com o SDK" (291) "Requer .NET 8 SDK e Docker". Sem contradição, mas a seção "Testes" não cita o SDK 8: quem só quer rodar testes junta duas seções |
| Comando do Stryker | **OK nos textos.** README (538-541) e `CLAUDE.md` (75-76) mandam `dotnet tool restore` e `dotnet stryker` em `tests/FixedIncome.Domain.Tests`. O comportamento em outra pasta é da parte B (QA9-09b) |
| Mensagem sem Docker | **OK.** README linhas 468-469 e `tests/Shared/PostgreSqlFixture.cs` linha 38: "Os testes de integração precisam do Docker em execução. Inicie o Docker e rode os testes de novo." Idênticas, com acentos. A mensagem em execução real é do QA9-03 (parte B) |
| Imagem | **RISCO** (baixo, informação). `ESPECIFICACAO.md` linha 260 diz `postgres:16`; `docker-compose.yml` linha 3 usa `postgres:16-alpine`. A diferença de collation está na SPEC-F9 (117-120), mas o README não a menciona |
| `CLAUDE.md` | **OK.** Linha 65 "InMemory até a F8" é histórico correto, seguida de "a partir da F9... Testcontainers"; linhas 75-76 "limite de score é 80%; abaixo disso o comando falha". A prova prática do `break` é do QA9-10 (parte B) |
| ROADMAP | Estado, sem classificar: critérios da F9 (linhas 176-180) todos desmarcados (`- [ ]`). A "linha 142" do roteiro é hoje o parágrafo "Não há item pendente do QA da F8. Os três critérios de pipeline acima continuam abertos..." na seção da F8 |
| ESPECIFICACAO, graphify | Ainda presente: linha 263 "graphify, com o grafo local em `graphify-out/`, fora do versionamento", contra `CLAUDE.md` linha 94 "A committed graphify-out/graph.json...". O `.gitignore` (487) ignora `graphify-out/`, o que bate com a ESPECIFICACAO; o `CLAUDE.md` traz a regra genérica do graphify. Só registro |
| ESPECIFICACAO, InMemory | Linha 290 (F3) "Testes com InMemory": histórico da fase F3; as linhas 260-262 trazem o v2. Sem contradição |
| Links | **OK.** O script do roteiro nos três arquivos não imprimiu nenhum `QUEBRADO`. Âncoras do README: `#testes` (linha 458), `#qualidade` (505) e `#indexadores-e-cotações` (71) existem |

Nenhum número, comando ou versão contraditório que leve o usuário a erro.

## QA9-17 — Regras de camada diante de violações novas: **RISCO** (nenhum BUG)

Método: seis cópias novas (`arq-a` a `arq-f`, feitas com `git ls-files -co --exclude-standard`), cada uma com uma classe nova de poucas linhas (nenhum código existente alterado), e `dotnet test tests/FixedIncome.Architecture.Tests` (Debug, SDK 8.0.427, Windows). Sem Docker. As cópias foram apagadas ao fim.

| Caso | Violação inserida | Resultado | Classificação |
|---|---|---|---|
| a | `FixedIncome.Api.Controllers.QaAController` com campo `FixedIncome.Infrastructure.Repositories.FixedIncomeAssetRepository` (repositório concreto) | **Vermelho**: `Failed: 1, Passed: 6, Total: 7`, teste `Controllers_nao_dependem_de_infrastructure_nem_do_ef_core`, mensagem `Controllers viola a regra de dependência. Tipos: FixedIncome.Api.Controllers.QaAController`; exit 1 | **OK** (esperado) |
| b | `FixedIncome.Api.Endpoints.QaDbAccess` com campo `FixedIncomeDbContext`, e `QaBController` (em `Controllers`) com campo `QaDbAccess` | **Verde**: `Passed: 7`, exit 0. O controller só depende de um tipo da própria Api; a regra olha só `FixedIncome.Api.Controllers` e só dependências diretas do tipo | **RISCO** (decisão do Matheus: sem regra na spec). Contorna o espírito do `CLAUDE.md` ("Controller ... não contém regra de negócio") |
| c | `QaCController` com campo `Npgsql.NpgsqlConnection` | **Verde**: `Passed: 7`, exit 0 (o `Npgsql` não está na lista da regra de controllers: só `FixedIncome.Infrastructure` e `Microsoft.EntityFrameworkCore`) | **RISCO** (idem) |
| d | `FixedIncome.Application.QaHttp` com campo `Microsoft.AspNetCore.Http.HttpContext?`, com `FrameworkReference Microsoft.AspNetCore.App` no `.csproj` da Application | **Vermelho**: `Failed: 1, Passed: 6`, mensagem `Application viola a regra de dependência. Tipos: FixedIncome.Application.QaHttp`; exit 1 | **OK** (esperado). Nota: a primeira tentativa falhou na compilação por defeito do meu `sed` (dois `FrameworkReference`, NETSDK1087); refeita com uma só e o resultado acima é o da segunda |
| e | `FixedIncome.Domain.QaHttpClientUser` com campo `System.Net.Http.HttpClient` | **Verde**: `Passed: 7`, exit 0. `System.Net.Http` casa com a lista branca `System.*` do teste `Domain_referencia_apenas_assemblies_do_framework` e não está na lista proibida | **RISCO** (decisão do Matheus: coerente com a spec, mas não com o espírito de "Domain puro") |
| f | `ProjectReference` de `FixedIncome.Domain` para `FixedIncome.Application` | `dotnet test` e `dotnet build` **falham antes dos testes**: `error MSB4006: There is a circular dependency in the target dependency graph involving target "_GenerateRestoreProjectPathWalk"` (em `FixedIncome.Domain.csproj`); exit 1 | **OK**: o build acusa o ciclo. A "limitação" registrada no teste (ProjectReference sem uso não aparece no metadata) é inofensiva para Application, Infrastructure e Api, porque todas referenciam o Domain e qualquer referência de volta forma ciclo. Só ficaria sem alarme uma referência do Domain a projeto novo que não dependa dele. Observado só para Domain → Application |

Nota sobre a primeira rodada do caso f: um `sed` meu mal escapado produziu um caminho inexistente (`..FixedIncome.Application...`), o que deu verde com aviso de projeto inexistente; descartei esse resultado e refiz o caso com o caminho correto, que é o da tabela.

Nenhum BUG: a e d (os dois que o roteiro marca como obrigatoriamente vermelhos) ficaram vermelhos e nomeiam o tipo. b, c e e são RISCO conforme a decisão do Matheus.

Sugestão de teste, só se o Matheus quiser fechar o RISCO b/c: `Tipos_fora_de_controllers_da_api_nao_dependem_de_infrastructure_nem_do_ef_core` (todo tipo de `FixedIncome.Api`, exceto o composition root), e incluir `Npgsql` na lista dos controllers.

## QA9-15a — Domain, Application e Architecture em Linux (`sdk:8.0`), 4 fusos: **OK**

Comando (cópia `clone-linux`, feita com `git ls-files -co --exclude-standard`, 0 pastas `bin/obj`; contêiner efêmero `docker run --rm`, sem portas, sem socket do Docker):

```bash
for tz in UTC America/Sao_Paulo Pacific/Kiritimati America/Los_Angeles; do
  docker run --rm -e TZ=$tz -v "$(cygpath -w "$C3"):/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 sh -c \
    'date; dotnet --version; dotnet test tests/FixedIncome.Domain.Tests -v q; dotnet test tests/FixedIncome.Application.Tests -v q; dotnet test tests/FixedIncome.Architecture.Tests -v q'
done
```

SDK do contêiner: `8.0.427`. O `date` de cada contêiner confirma o fuso aplicado (`UTC`, `-03`, `+14` com data já em 10/11, `PST`).

| Fuso | Domain | Application | Architecture | exit |
|---|---|---|---|---|
| UTC | 332 passaram | 112 passaram | 7 passaram | 0 |
| America/Sao_Paulo | 332 passaram | 112 passaram | 7 passaram | 0 |
| Pacific/Kiritimati (+14) | 332 passaram | 112 passaram | 7 passaram | 0 |
| America/Los_Angeles | 332 passaram | 112 passaram | 7 passaram | 0 |

Em todas: 0 falhas, 0 ignorados, nenhum `[FAIL]`, nenhum aviso (`warning` ou `NU1` ausentes dos quatro logs). Os totais 332, 112 e 7 batem com o README e com a `VALIDACAO-F9.md`.

Complemento (não pedido pelo roteiro, sem Docker dentro do contêiner): `dotnet build` de `tests/FixedIncome.Infrastructure.Tests` e `tests/FixedIncome.Api.Tests` na mesma cópia Linux, para exercitar o `Compile Include="..\Shared\PostgreSqlFixture.cs"` com `\` fora do Windows: `Build succeeded`, `0 Warning(s)`, `0 Error(s)` nos dois. Os testes dessas duas camadas em Linux (QA9-15b) não foram executados.

## Estado do ambiente ao fim

- `docker ps -a` igual ao de antes (`antes-ps.txt`): nenhum contêiner novo, nenhum volume novo, nenhuma imagem nova. Nenhum contêiner do Testcontainers foi criado nesta parte.
- Repositório do Matheus: único arquivo criado é este relatório (e `docs/QA-F9-ROTEIRO.md`, que já existia). Nenhum `build`, `test` ou `compose` rodou nele.
- Cópias descartáveis (`arq-*`, `clone-linux`, `gh`) apagadas do scratchpad.
- Dados `QA-`: nenhum criado nesta parte.

## Achados da parte A

### BUG

Nenhum.

### RISCO

| Id | Origem | Descrição |
|---|---|---|
| A-R1 | QA9-17 b | A regra 3 só olha `FixedIncome.Api.Controllers`: um controller que chama uma classe de outro namespace da Api que usa o `FixedIncomeDbContext` passa verde |
| A-R2 | QA9-17 c | `Npgsql` fora da lista de dependências proibidas dos controllers (verde com `NpgsqlConnection`) |
| A-R3 | QA9-17 e | `System.Net.Http.HttpClient` no Domain passa verde (lista branca `System.*`) |
| A-R4 | QA9-11 b | "Testes de Arquitetura" é o último passo do `ci.yml`: violação de camada só aparece depois de Infrastructure e Api, e uma falha de Docker esconde o resultado |
| A-R5 | QA9-11 c | Commit só em `tests/**`, `stryker-config.json` ou `mutation.yml` gera release completo sem mudar a imagem; `ci.yml` roda duas vezes por push na `main` |
| A-R6 | QA9-11 g | README não avisa que o `schedule` do `mutation.yml` só vale depois do merge na branch padrão |
| A-R7 | QA9-11 h | Sem `global.json`, o SDK que o runner escolhe (mais novo que o 8) não foi observado; só execução real confirma |
| A-R8 | QA9-13 | A seção "Testes" do README não cita o SDK 8; o README não explica `postgres:16` (testes) contra `postgres:16-alpine` (compose) |
| A-R9 | QA9-13 | `ESPECIFICACAO.md` linha 263 ("graphify ... fora do versionamento") contra `CLAUDE.md` linha 94 ("A committed graphify-out/graph.json"); só registro |

### Informação (sem classificação)

- ROADMAP: os cinco critérios da F9 (linhas 176-180) continuam desmarcados.
- README "partida 82,76%": só o README sustenta o número.
- `StrykerOutput` não está no `.dockerignore` (sem regra na spec; RISCO só se o Matheus decidir; o `.gitignore` o cobre).
- Ryuk `0.14.0` não aparece em documento algum.
- `actionlint` 1.7.7 e `zizmor` 1.14.2 (offline, pedantic): limpos, árvore de trabalho com HEAD `a741bf4`.

## Veredito da parte A

**SEM BUGS BLOQUEANTES** nos roteiros desta parte (QA9-11, QA9-13, QA9-16, QA9-17, QA9-15a). Fica para a parte B tudo que depende de rodar os testes de integração, o Stryker e o `docker compose`.
