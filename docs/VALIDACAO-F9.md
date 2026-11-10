# Validação da F9 — Qualidade: Testcontainers, NetArchTest e Stryker.NET

Verificação independente da F9, feita em 09/11/2026. Cada afirmação abaixo vem de comando rodado
ou arquivo lido nesta verificação; as exceções (a prova do P5 e a execução com a ordem invertida
do P5b) estão marcadas como **relato** e dizem de quem vem. Nenhum código foi alterado, nada foi
commitado nem adicionado ao índice. O único arquivo criado é este.

- Data: 2026-11-09
- Commit-base: `a741bf4` (HEAD da branch `f9-qualidade`, o merge do PR #8, anterior à fase). A
  árvore tinha alterações não commitadas, esperado porque a fase ainda não foi commitada:
  **46 entradas** em `git status --short` antes deste arquivo (40 modificadas e 6 não rastreadas:
  `.github/workflows/mutation.yml`, `docs/SPEC-F9-EXECUCAO.md`, `tests/FixedIncome.Architecture.Tests/`,
  `tests/FixedIncome.Domain.Tests/stryker-config.json`, `tests/FixedIncome.Infrastructure.Tests/Integration/`
  e `tests/Shared/`). `git diff --cached` vazio e `git stash list` vazio. Com este arquivo são 47.
- Fontes: `docs/ROADMAP.md` (seção F9, 5 critérios), `docs/SPEC-F9-EXECUCAO.md` (aprovada em
  09/11/2026), `CLAUDE.md`.
- Ambiente: Windows 11, .NET SDK 8.0.427, Docker 29.7.2 (API 1.55). `gh` não foi usado: o
  `ci.yml` e o `mutation.yml` não têm execução no GitHub ainda.

## Baseline e execução

| Verificação | Comando | Resultado |
|---|---|---|
| Build | `dotnet build --no-incremental`; e `dotnet build --configuration Release` | `0 Warning(s)  0 Error(s)` nos dois |
| Testes | `dotnet test --no-build` (solution) | Domain 332, Application 112, Architecture 7, Infrastructure 148, Api 57 = **656**, 0 falhas, 0 ignorados |
| Skip | `grep -rn "Skip" tests --include=*.cs` | uma ocorrência, o comentário `// Nada de Skip: ...` em `tests/Shared/PostgreSqlFixture.cs:36`; nenhum `Skip =`, nenhum `Trait(`, nenhum `continue-on-error` fora de um comentário do `ci.yml` |
| Pacotes | `git diff` dos `.csproj` e `dotnet list ... package` | `Testcontainers.PostgreSql 4.16.0` entra em Infrastructure.Tests e Api.Tests; `Microsoft.EntityFrameworkCore.InMemory` sai dos dois; `NetArchTest.Rules 1.3.2` só em Architecture.Tests; `FixedIncome.Domain.csproj` sem alteração (0 linhas de diff, zero `PackageReference`) |
| Ferramenta | `.config/dotnet-tools.json` | `dotnet-stryker 4.16.0` ao lado do `dotnet-ef 8.0.11`, versão fixa, nenhum `.csproj` o referencia |
| Código de produção | `git diff --stat -- src` | 5 arquivos, **14 inserções e 1 remoção**, todas comentário `// Stryker disable` (ver "Lista de desabilitações"); nenhuma regra, entidade, caso de uso, controller, repositório ou migration mudou |

Contagem de testes: **591 antes (linha de base), 656 depois** (+65). Nenhum projeto caiu:

| Projeto | Antes | Depois | Diferença |
|---|---|---|---|
| Domain | 292 | 332 | +40 (testes que matam mutantes) |
| Application | 112 | 112 | 0 |
| Architecture (novo) | 0 | 7 | +7 |
| Infrastructure | 130 | 148 | +18 (job de importação contra o banco, restrições do PostgreSQL) |
| Api | 57 | 57 | 0 (migraram para o PostgreSQL sem mudar de asserção) |
| **Total** | **591** | **656** | **+65** |

A conta bate com o esperado informado (656; Domain 332, Application 112, Architecture 7,
Infrastructure 148, Api 57).

Tempos medidos (a spec fixa o teto de 60 s para o Api.Tests):

| Medida | Tempo |
|---|---|
| `dotnet test --no-build` na solution, tudo junto (os projetos rodam em paralelo e disputam o Docker) | 62 s no relógio; Api.Tests 42 s e Infrastructure.Tests 44 s (duração impressa por projeto) |
| Api.Tests sozinho, com cobertura, Release | duração impressa 16 s (26 s no relógio com a subida do test host e do contêiner) |
| Infrastructure.Tests sozinho, com cobertura, Release | duração impressa 17 s (27 s no relógio) |
| Cada classe de integração sozinha | 11 a 14 s no relógio, dos quais cerca de 10 s são subir o contêiner |
| Stryker no Domain | 2 min 14 s (`Time Elapsed 00:02:14`) |

O teto de 60 s do Api.Tests está atendido nos dois cenários (42 s no pior).

## Cobertura

Método do `ci.yml`: `dotnet build --configuration Release`, depois um projeto por vez, `dotnet test
tests/FixedIncome.<X>.Tests --configuration Release --no-build --settings coverlet.runsettings
--collect:"XPlat Code Coverage" --results-directory <pasta>`. Cada linha usa o `<package>` do assembly
da própria camada no `coverage.cobertura.xml` do projeto de teste correspondente.

| Camada | Testes | Linha | Branch | Antes (VALIDACAO-F8, pós-QA) |
|---|---|---|---|---|
| Domain | 332 | **96,3%** | **100%** | 95,2% / 98,1% |
| Application | 112 | **92,6%** | **98,9%** | 92,6% / 98,9% |
| Infrastructure | 148 | **99,7%** | **98,4%** | 99,7% / 98,4% |
| Api | 57 | **96,4%** | **91,3%** | 96,4% / 91,3% |
| Architecture | 7 | sem cobertura (o projeto só avalia metadata; o `ci.yml` o roda sem coletor) | | |

Valores brutos do XML: Domain `0.9632 / 1`; Application `0.9259 / 0.9888`; Infrastructure `0.9972 /
0.9838`; Api `0.9644 / 0.9130`. Meta (Domain e Application acima de 80%): **atendida**.

Para o orquestrador atualizar o README: **656 testes**; tabela Domain 332 / 96,3% / 100%;
Application 112 / 92,6% / 98,9%; Infrastructure 148 / 99,7% / 98,4%; Api 57 / 96,4% / 91,3%;
Architecture 7 (sem cobertura). Mutation score do Domain: **99,33%**.

## Critério 1 — Testes de integração rodam com `dotnet test`, sem PostgreSQL instalado

| Verificação | Evidência | Resultado |
|---|---|---|
| Nenhum contêiner antes dos testes | `docker ps -a` vazio (sem `fixedincome-db`, sem `fixedincome-api`) | ok |
| Nada escutando na 5432 | `netstat -ano \| grep ":5432"` sem saída; nenhum processo `postgres`; `psql` não está no PATH | ok |
| Serviço do Windows | existe um serviço `postgresql-x64-17` na máquina, com `Status = Stopped` | **ressalva**: há um PostgreSQL instalado, mas parado; o teste não o usa |
| Testes não apontam para banco fixo | `grep -rniE "localhost:5432\|Host=localhost\|UseInMemoryDatabase\|Microsoft.EntityFrameworkCore.InMemory" tests` (`.cs`, `.csproj`, `.json`, fora de `bin` e `obj`) | zero ocorrências |
| `dotnet test` verde | 656 testes, 0 falhas | ok |
| O contêiner é do Testcontainers | `docker ps` durante a execução: dois `postgres:16` em portas aleatórias (`0.0.0.0:54216->5432`, `0.0.0.0:54217->5432`, nomes `magical_kirch` e `elegant_thompson`) e dois `testcontainers/ryuk:0.14.0` (portas 62086 e 62087). Dois porque cada assembly (Infrastructure.Tests e Api.Tests) sobe o seu | ok |
| Limpeza | `docker ps -a` ao fim: vazio (postgres removidos pelo Dispose da fixture, Ryuk pelo encerramento) | ok |
| Banco por teste, a partir do modelo com as migrations | `tests/Shared/PostgreSqlFixture.cs` lido: `CREATE DATABASE "template_fixedincome"`, `Database.MigrateAsync()`, `NpgsqlConnection.ClearAllPools()` só no setup, `CREATE DATABASE "test_<guid:N>" TEMPLATE ...`, `DROP DATABASE ... WITH (FORCE)` precedido de `ClearPool` da conexão do teste; imagem `postgres:16`; `[CollectionDefinition("PostgreSQL")]` | ok |
| Todas as classes de integração usam a coleção | `grep -rl '[Collection("PostgreSQL")]'`: 16 arquivos (10 de Api e 6 de Infrastructure) | ok |
| Sem Docker o teste falha, não é ignorado | leitura de `PostgreSqlFixture.InitializeAsync`: `DockerUnavailableException` vira `InvalidOperationException("Os testes de integração precisam do Docker em execução. Inicie o Docker e rode os testes de novo.")`; nenhum `Skip` | ok por leitura (ver "Itens abertos", ii) |
| Lado do CI | `ci.yml` lido: `dotnet test` de Infrastructure e Api no `ubuntu-latest` (que traz Docker), comentário sobre `toomanyrequests`; passo "Testes de Arquitetura" depois; `actionlint` e `zizmor` limpos (abaixo) | **ABERTO**: só se fecha com a execução real do `ci.yml` num PR |

Resultado do critério 1 do ROADMAP: **ATENDIDO localmente; ABERTO no CI**.

## Critério 2 — Violar uma regra de arquitetura quebra o build

Leitura de `tests/FixedIncome.Architecture.Tests/LayerDependencyTests.cs` (NetArchTest.Rules 1.3.2):

| Teste | O que verifica |
|---|---|
| `Domain_nao_depende_de_outras_camadas_nem_de_pacotes_de_infraestrutura` | `HaveDependencyOnAny` de Application, Infrastructure, Api, `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore`, `Microsoft.Extensions`, `Npgsql`; a mensagem lista os tipos que violam |
| `Domain_referencia_apenas_assemblies_do_framework` | `GetReferencedAssemblies()` do Domain só pode ter `System*`, `netstandard`, `mscorlib` (lista branca); limitação registrada no próprio teste (ProjectReference sem uso é omitida do metadata) |
| `Application_nao_depende_de_infrastructure_api_nem_de_pacotes_web_e_de_dados` | Infrastructure, Api, EF Core, AspNetCore, Npgsql |
| `Controllers_nao_dependem_de_infrastructure_nem_do_ef_core` | tipos em `FixedIncome.Api.Controllers` sem dependência de Infrastructure nem de EF Core |
| `Assembly_do_domain_tem_tipos_para_avaliar` | conjunto não vazio |
| `Assembly_da_application_tem_tipos_para_avaliar` | conjunto não vazio |
| `Namespace_dos_controllers_tem_tipos_para_avaliar` | conjunto não vazio (NetArchTest passa em silêncio com filtro que não casa) |

`dotnet test tests/FixedIncome.Architecture.Tests --no-build -v n`: os 7 `Passed`
(`Passed!  - Failed: 0, Passed: 7, Skipped: 0, Total: 7`). A solution (`FixedIncome.sln:26`)
inclui o projeto; o `ci.yml` o roda (`dotnet test tests/FixedIncome.Architecture.Tests`).
`dotnet list package`: só `NetArchTest.Rules 1.3.2` e os pacotes padrão de teste.

**Evidência da prova (relato do implementador, P5; eu não repeti, porque a regra desta verificação
é não alterar arquivos fora deste):**

- (a) com `PackageReference Microsoft.EntityFrameworkCore 8.0.11` temporário no `Domain.csproj` e
  uma classe `TempViolation` com campo `DbContext` no Domain:
  `[FAIL] Domain_references_only_framework_assemblies — O Domain referencia assemblies fora do framework: Microsoft.EntityFrameworkCore`;
  `[FAIL] Domain_does_not_depend_on_other_layers_or_infrastructure_packages — Domain viola a regra de dependência. Tipos: FixedIncome.Domain.TempViolation`;
  `Failed: 2, Passed: 5`.
- (c) com um campo `FixedIncomeDbContext` em `AssetsController`:
  `Controllers viola a regra de dependência. Tipos: FixedIncome.Api.Controllers.AssetsController`;
  `Failed: 1, Passed: 6`.
- Árvore restaurada. (Os testes foram renomeados para português depois da prova.)

Conferência feita aqui: o `git diff --stat -- src` mostra só as 14 linhas de comentário do Stryker,
e `git diff` de `FixedIncome.Domain.csproj` e `AssetsController` não mostra resíduo da prova (nem o
`PackageReference` temporário nem o campo do `DbContext`). A reprodução independente em
`git clone --local` no scratchpad é do orquestrador e será anexada por ele.

Resultado: **ATENDIDO** quanto à existência e ao desenho das regras e ao estado da árvore;
a prova do "quebra o build" está como **relato**, com a reprodução do orquestrador pendente.

## Critérios 3 e 4 — Stryker.NET: score do Domain e relatório

`cd tests/FixedIncome.Domain.Tests; dotnet tool restore; dotnet stryker` (Stryker.NET 4.16.0,
`stryker-config.json`: projeto `FixedIncome.Domain.csproj`, relatórios `html` e `json`,
`thresholds` `high 90 / low 80 / break 80`). Saída: exit 0, `Time Elapsed 00:02:14`,
**`The final mutation score is 99.33 %`**.

Totais por status, do `mutation-report.json` (`StrykerOutput/2026-11-09.14-15-53/reports/`):

| Status | Mutantes |
|---|---|
| Killed | 285 |
| Timeout (conta como morto) | 11 |
| Survived | 2 |
| NoCoverage | 0 |
| Ignored | 90 (69 do filtro padrão "Removed by block already covered" e **21** por comentário `Stryker disable`) |
| CompileError (fora do score) | 19 |
| **Total gerado** | **407** |

Conta: (285 + 11) / (285 + 11 + 2) = 296 / 298 = **99,33%**, igual ao número impresso. "298 total
mutants will be tested" no log. Piso do critério (80%): atendido com folga.

**Sobreviventes (2), aceitos sem `disable`** (ambos em `DecimalMath.Ln`, a redução do argumento
para [0,5; 2]):

| Linha | Mutante | Por que sobrevive |
|---|---|---|
| `DecimalMath.cs:71` | `x >= 2m` no lugar de `x > 2m` | em `x == 2` a redução divide por 2 e põe `k = 1`; sem ela o `atanh` converge igual; os dois caminhos só diferem no ruído da 28ª casa decimal |
| `DecimalMath.cs:77` | `x <= 0.5m` no lugar de `x < 0.5m` | simétrico do anterior, em `x == 0.5` |

**Relatório como arquivo.** Em `tests/FixedIncome.Domain.Tests/StrykerOutput/2026-11-09.14-15-53/reports/`
existem `mutation-report.html` (1.264.202 bytes) e `mutation-report.json` (1.034.861 bytes). É a
pasta `StrykerOutput` que o `mutation.yml` publica. `StrykerOutput/` está no `.gitignore`.

**`mutation.yml` lido** (`.github/workflows/mutation.yml`):

| Propriedade exigida | Onde está | Resultado |
|---|---|---|
| Semanal e manual | `on.schedule: cron "0 6 * * 1"` (segunda, 06:00 UTC) e `workflow_dispatch` | ok |
| Permissões mínimas | `permissions: contents: read` no topo; nenhum `write` | ok |
| `persist-credentials: false` | checkout, com comentário do motivo | ok |
| Ações fixadas por SHA | `actions/checkout@11d5960a...` (v4.4.0), `actions/setup-dotnet@67a3573c...` (v4.3.1), `actions/upload-artifact@ea165f8d...` (v4.6.2); os mesmos SHAs do `ci.yml` | ok |
| Roda `dotnet tool restore` e `dotnet stryker` em `tests/FixedIncome.Domain.Tests` | passos "Restaurar ferramentas locais" e "Stryker no Domain" (`working-directory`) | ok |
| Limite que reprova o passo | `break: 80` no `stryker-config.json` (exit diferente de zero abaixo do limite) | ok |
| Upload mesmo com score baixo | passo "Publicar relatório de mutação" com `if: always()` | ok |
| Artefato `stryker-report`, caminho certo, nome fixo | `path: tests/FixedIncome.Domain.Tests/StrykerOutput`, `name: stryker-report`, `overwrite: true`, `retention-days: 30` | ok |
| Timeout explícito | `timeout-minutes: 30` | ok |
| Sem segredo, sem gatilho de branch | única credencial é o `GITHUB_TOKEN` implícito; nenhum `secrets.` | ok |

Resultado: critério 3 do ROADMAP (mutation score >= 80%) **ATENDIDO** (99,33%). Critério 4
(relatório salvo como artifact do workflow): relatório gerado localmente e workflow correto por
leitura; **ABERTO** até a execução real do `mutation.yml` no GitHub (o Matheus a dispara manualmente
depois do merge).

### Lista de desabilitações (`// Stryker disable`)

Extraída de `git diff -- src`: **8 comentários em 5 arquivos** (mais 1 bloco inline, no
`IofTable`), que silenciam **21 mutantes**. Os outros **69** ignorados são do filtro padrão do
Stryker ("Removed by block already covered filter"). Para cada um, o motivo escrito no código:

| # | Arquivo:linha | Mutador | Mutantes | Motivo escrito |
|---|---|---|---|---|
| 1 | `DecimalMath.cs:22-23` | Logical, Block | 2 | atalho de otimização: `Pow(x, 0)` e `Pow(1, y)` seguem pelo caminho geral (`IntegerPow` ou `exp(y * ln 1) = exp(0)`) e também dão exatamente 1m |
| 2 | `DecimalMath.cs:56` | Assignment (`>>>=`, `<<=`) | 2 | `>>>=` é equivalente (n é `long` não negativo, `>>=` e `>>>=` são idênticos); o `<<=` é silenciado junto, por efeito colateral do `disable once` na linha |
| 3 | `DecimalMath.cs:64` | Equality (`<= 0`, `> 0`) | 2 | com expoente 0 (só chega aqui vindo de `Exp`, com n = 0) `result` é 1m e `1m / 1m = result`; os dois ramos coincidem |
| 4 | `FixedIncomeAsset.cs:15` | String | 1 | valor inicial de `Name` sempre sobrescrito pelo construtor e, no EF, pela coluna (IsRequired) |
| 5 | `FixedIncomeAsset.cs:17` | String | 1 | idem, para `Issuer` |
| 6 | `FixedIncomeAsset.cs:97` | String (`""`, `$""`) | 2 | `RateLimit` tem escala 0, então `ToString("0")` e `ToString("")` produzem o mesmo texto |
| 7 | `Holiday.cs:11` | String | 1 | valor inicial de `Name` sempre sobrescrito (construtor e coluna) |
| 8 | `Position.cs:36` | String (`""`, `$""`) | 2 | `InvestedAmountLimit` tem escala 0, mesma razão do item 6 |
| 9 | `Position.cs:76` | Equality (`>=`, `<`) | 2 | com `referenceDate == MaturityDate` os dois ramos resultam na mesma data |
| 10 | `Position.cs:106` | Equality (`>=`, `<`) | 2 | com `grossYield == 0` o ramo executado dá `iofAmount = 0.00m` e `taxAmount = 0.00m`, numericamente iguais aos 0m iniciais; só a escala difere |
| 11 | `Position.cs:174` | Equality (`<=`, `>`) | 2 | o mês do último IPCA publicado tem cotação, então `GetIpca` nunca devolve nulo para ele e `month.Key == lastPublished.Month` não chega a esta comparação |
| 12 | `IofTable.cs:26` (bloco inline `/* ... */`) | Equality (`>=`, `<`) | 2 | `Rates[^1]` (dia 30) vale 0m, então `>=` só difere de `>` no dia 30, onde as duas saídas são 0m |
| | | | **21** | |

A conta (2+2+2+1+1+2+1+2+2+2+2+2) fecha em 21, e o relatório do Stryker lista exatamente 21
ignorados com motivo diferente do filtro.

**Segunda lista — ignorados por efeito colateral, não equivalentes, mortos antes (auditoria do
especialista).** Nos itens 2, 3, 6, 8, 9, 10, 11 e 12 o `disable once` cobre a linha inteira e
silencia também um mutante que **não é equivalente**. São 8 dos 21. Os outros **13** são
equivalentes de fato. Os 8, com o teste que os mata (nomes conferidos nos arquivos de teste):

| Mutante silenciado por tabela | Quem o mata | Teste (existe no arquivo) |
|---|---|---|
| `DecimalMath.cs:56` `n <<= 1` | `n` nunca zera, o quadrado estoura o decimal e a potência lança `DomainException` | `DecimalMathTests.Potencia_de_expoente_inteiro_negativo_e_o_inverso`, `DecimalMathTests.Potencia_com_base_fora_do_intervalo_de_reducao_devolve_a_raiz_exata`, e os de Position (`PositionTests`, `PositionIpcaProjectionTests`) |
| `DecimalMath.cs:64` `exponent > 0` | 1,10² viraria 1/1,21 | `DecimalMathTests.Potencia_de_expoente_inteiro_e_exata` |
| `FixedIncomeAsset.cs:97` `$""` (mensagem vazia) | a mensagem do limite deixaria de ser a esperada | `FixedIncomeAssetTests.Taxa_a_partir_de_1e12_e_rejeitada_citando_o_limite` (`Assert.Equal("Rate deve ser menor que 1000000000000.", ...)`) |
| `Position.cs:36` `$""` | idem | `PositionTests.Valor_investido_a_partir_de_1e16_e_rejeitado_citando_o_limite` (`Assert.Equal("InvestedAmount deve ser menor que 10000000000000000.", ...)`) |
| `Position.cs:76` `referenceDate < MaturityDate` | a data de cálculo ficaria presa ao vencimento mesmo antes dele | `PositionTests.Projecao_com_capitalizacao_composta_em_base_252_calcula_bruto_rendimento_imposto_e_liquido` (caso 1.2, bruto 1.099,17) |
| `Position.cs:106` `grossYield < 0` | rendimento positivo deixaria de gerar imposto | `PositionTests.Projecao_calcula_imposto_sobre_o_rendimento_e_nao_sobre_o_principal` (caso 1.1, IR 173,60) |
| `Position.cs:174` `month.Key > lastPublished` | mês anterior ao último publicado deixaria de lançar | `PositionIpcaProjectionTests.Ipca_ausente_no_primeiro_mes_do_intervalo_anterior_ao_ultimo_publicado_lanca_excecao`, `Ipca_ausente_no_meio_da_serie_lanca_excecao_citando_o_mes` |
| `IofTable.cs:26` `elapsedDays < Rates.Length` | a tabela devolveria 0 em vez de 96% no dia 1 | `IofTableTests.RateFor_segue_a_tabela_do_decreto_para_os_dias_1_a_30` (`InlineData(1, 96)` até `(30, 0)`) |

**Prova independente desta classificação.** Em vez de aceitar a auditoria, tirei o ruído: copiei
o repositório para o scratchpad (sem `.git`, `bin`, `obj`), removi dos cinco arquivos de `src` todos
os comentários e o bloco `Stryker disable` (nenhum sobrou, `grep` confirmou) e rodei `dotnet stryker`
na cópia. Resultado: **95,30%**; Killed 293 (285 + 8), Timeout 11, Survived **15**, Ignored 69 (só o
filtro padrão), CompileError 19. Os 15 sobreviventes são exatamente os 13 equivalentes da lista acima
(`DecimalMath` linhas 21, 22, 54 e 61; `FixedIncomeAsset` 14, 15, 94; `Holiday` 10; `IofTable` 26 `>=`;
`Position` 35, 74, 103, 170 — numeração da cópia, deslocada pelas linhas de comentário) mais os 2
aceitos (`x >= 2m`, `x <= 0.5m`). Ou seja, os 8 "efeitos colaterais" são mortos pelos testes, e os
13 restantes sobrevivem porque são equivalentes. A cópia foi apagada ao final. Mesmo no pior caso, sem
nenhum `disable`, o score seria 95,30%, bem acima de 80%.

Limite: a equivalência dos 13 foi conferida pela leitura do código e pela sobrevivência deles com a
suíte atual; não gerei uma prova formal de equivalência para cada um.

## Critério 5 — Nenhum teste depende da ordem nem dos dados de outro

| Evidência | Comando e resultado | Resultado |
|---|---|---|
| Sem estado estático mutável | `rg --pcre2 "static (?!readonly)(?!class)[^(\n]*(=\|;\|\{\s*get)" tests -g "*.cs"`: só 2 `TheoryData` expression-bodied (`PositionIofProjectionTests.CdbCases`, `LciCases`), que montam um objeto novo a cada leitura. Os `static readonly` são constantes de data/duração, séries imutáveis, `Regex` e `Assembly` | ok |
| Variável de ambiente e diretório | única mexida em estado global: `FixedIncomeDbContextFactoryTests` (F8), em coleção própria com `DisableParallelization = true`, restaurando variável e diretório no `Dispose` | ok |
| Banco por teste | `PostgreSqlFixture.CreateDatabase`/`DropDatabase`; nenhuma classe reaproveita banco de outro teste | ok |
| Cada classe de integração sozinha, Infrastructure.Tests (`dotnet test --no-build --filter "FullyQualifiedName~.<Classe>"`) | `IndexRatesImportIntegrationTests` 6/6; `FixedIncomeAssetRepositoryTests` 7/7; `HolidayRepositoryTests` 5/5; `IndexRateRepositoryTests` 20/20; `PositionRepositoryTests` 8/8; `AnbimaHolidaySeedBusinessDaysTests` 12/12 | 58 testes, 0 falhas |
| Cada classe sozinha, Api.Tests | `AssetsControllerTests` 7/7; `ColumnLimitsTests` 6/6; `ExceptionHandlingMiddlewareDomainExceptionTests` 1/1; `ExceptionHandlingMiddlewareUnhandledExceptionTests` 1/1; `FrameworkErrorResponsesTests` 6/6; `HealthEndpointTests` 3/3; `IndexesControllerTests` 16/16; `IndexRatesImportJobDisabledInTestsTests` 1/1; `InvalidModelStateResponseTests` 6/6; `PortfolioSummaryFlowTests` 1/1; `PositionProjectionFlowTests` 2/2 | 50 testes, 0 falhas |
| Classes do Api.Tests fora do PostgreSQL (fábrica própria) | `IndexRatesImportJobCompositionTests` 4/4; `IndexRatesImportStartupLogTests` 2/2; `MissingConnectionStringStartupTests` 1/1 | 7 testes; 50 + 7 = 57, o total do projeto |
| Suíte com ordem de casos e de coleções invertida | **relato do implementador (P5b)**: `[assembly: TestCaseOrderer]` e `[assembly: TestCollectionOrderer]` temporários nos cinco assemblies, 616 testes verdes, log provando a inversão, árvore restaurada. Não repeti | relato |

Os 616 do relato do P5b são a contagem naquele momento do P5b (antes dos testes do P7); a contagem
final desta verificação é 656.

**Limitação (da spec, confirmada):** no xUnit 2 não existe orderer de classe dentro de uma coleção,
então a inversão não muda a ordem entre as classes da coleção `PostgreSQL`. Essa lacuna é coberta
pela execução classe a classe acima e pelo banco por teste (cada teste cria e descarta o próprio
banco, então a ordem entre classes não tem dado compartilhado para expor).

Resultado: **ATENDIDO**, com a inversão total da ordem apoiada em relato.

## Workflows

| Ferramenta | Comando | Resultado |
|---|---|---|
| `actionlint` 1.7.7 | `MSYS_NO_PATHCONV=1 docker run --rm -v "$(pwd -W):/repo" -w /repo rhysd/actionlint:1.7.7` (`-version` imprime `1.7.7`) sobre `ci.yml`, `mutation.yml`, `release.yml` | saída vazia, **exit 0** |
| `zizmor` 1.14.2 | `docker run ... ghcr.io/zizmorcore/zizmor:1.14.2 --offline --persona=pedantic .github`, sobre cópia de `.github` no scratchpad | `No findings to report. Good job!`, **exit 0**, 0 achados nos três workflows |

O `zizmor` rodou offline: as auditorias que exigem token da API do GitHub (`impostor-commit`,
`ref-confusion`, `known-vulnerable-actions`, `stale-action-refs`, `ref-version-mismatch`) foram
puladas, como na F8. O `ci.yml` conserva os passos e o `workflow_call` existentes; só ganhou os
comentários sobre o Docker do runner e o passo "Testes de Arquitetura". O `release.yml` reaproveita
o `ci.yml`, então o passo novo também vale para a publicação.

## Item 4 do fluxo padrão (docker compose e chamadas à API)

**N/A.** A fase não alterou código de produção: `git diff --stat -- src` mostra 5 arquivos,
14 inserções e 1 remoção, todas comentário (`// Stryker disable`, mais a reformatação da linha do
`IofTable` para o bloco inline, que não muda a condição). Não subi o compose. O banco real foi
exercitado pelos testes de integração, que são o objetivo da fase.

## Critérios do ROADMAP (seção F9)

| # | Critério | Evidência | Resultado |
|---|---|---|---|
| 1 | Os testes de integração rodam com `dotnet test`, local e no CI, sem PostgreSQL instalado | Local: `docker ps -a` vazio antes, 5432 livre, zero referência a `localhost:5432` nos testes, 656 verdes, contêineres `postgres:16` e Ryuk do Testcontainers vistos no `docker ps`. CI: `ci.yml` e `actionlint`/`zizmor` limpos, sem execução real. Ressalva: o serviço `postgresql-x64-17` existe na máquina, parado | **ATENDIDO localmente; ABERTO no CI** |
| 2 | Violar uma regra de arquitetura de propósito quebra o build | 7 testes de camada lidos e verdes; prova do P5 (a: `Failed: 2, Passed: 5`; c: `Failed: 1, Passed: 6`) por relato do implementador; árvore sem resíduo conferida | **ATENDIDO** (prova por relato; reprodução independente do orquestrador a anexar) |
| 3 | Mutation score do Domain em pelo menos 80% | `dotnet stryker`: `The final mutation score is 99.33 %`; 296 mortos de 298 testáveis; 95,30% mesmo sem nenhum `disable` | **ATENDIDO** |
| 4 | O relatório do Stryker fica salvo como artifact do workflow | Relatório html e json gerados em `StrykerOutput/<ts>/reports/`, mesmo caminho do `upload-artifact` do `mutation.yml` (`if: always()`, `overwrite: true`, `retention-days: 30`); `actionlint` e `zizmor` limpos | **ABERTO**: só a execução real do workflow no GitHub fecha |
| 5 | Nenhum teste depende da ordem de execução ou dos dados de outro | Sem estado estático mutável; banco por teste; 17 classes (11 de Api, 6 de Infrastructure) mais as de fábrica própria passam sozinhas; inversão total por relato (P5b) | **ATENDIDO** (inversão por relato; limitação do xUnit 2 registrada) |

## Critérios de aceite da spec (1 a 8)

| # | Critério | Resultado |
|---|---|---|
| 1 | Integração com `dotnet test`, local e no CI, sem PostgreSQL instalado | Local **ATENDIDO**; CI **ABERTO** (precisa do PR) |
| 2 | Violar regra de arquitetura quebra o build (prova do P5 anexada; reprodução do orquestrador) | **ATENDIDO** por relato; reprodução a anexar |
| 3 | Mutation score do Domain >= 80%, com a lista de desabilitados e o motivo de cada um | 99,33%; lista com os 21 acima e os 2 sobreviventes aceitos. **ATENDIDO** |
| 4 | Relatório do Stryker como artefato do `mutation.yml` | Gerado localmente; **ABERTO** no GitHub |
| 5 | Nenhum teste depende da ordem nem dos dados de outro | **ATENDIDO** (limitação registrada) |
| 6 | `dotnet build` sem warning, `dotnet test` verde, contagem >= 591, sem `Skip` | 0 avisos, 656 testes, 0 falhas, 0 ignorados. **ATENDIDO** |
| 7 | `actionlint` e `zizmor` limpos | exit 0 e 0 achados. **ATENDIDO** |
| 8 | Nenhum commit foi criado | HEAD continua em `a741bf4`; `git diff --cached` vazio; `git stash list` vazio. **ATENDIDO** |

## Regras de qualidade da fase e o que cobre cada uma

A fase não cria regra de negócio. As quatro regras de qualidade ("Regras novas" da spec):

| Regra | Teste ou arquivo |
|---|---|
| 1. Repositório e ponta a ponta rodam contra PostgreSQL real, com o esquema das migrations | `tests/Shared/PostgreSqlFixture.cs` (`MigrateAsync`); `HolidayRepositoryTests.Migrations_aplicadas_materializam_os_139_feriados_semeados`; `ApiTestBase` (banco por teste, sem `EnsureCreated`, sem InMemory); `grep` sem `UseInMemoryDatabase` |
| 2. Nenhum teste depende da ordem nem dos dados de outro | banco por teste na fixture; ausência de estático mutável (`rg`); execução classe a classe; inversão do P5b (relato) |
| 3. As três regras de camada do `CLAUDE.md` são testes; violar quebra o build | `LayerDependencyTests` (4 regras + 3 de conjunto não vazio, incluindo a lista branca do Domain) |
| 4. Mutation score do Domain >= 80%, equivalente justificado um a um | `dotnet stryker` 99,33%; `stryker-config.json` (`break: 80`); lista de 21 desabilitações acima; `mutation.yml` |

## Passos P1 a P9 e o que cobre cada um

| Passo | O que entregou | Evidência |
|---|---|---|
| P1 Fixture e primeiro repositório | `Testcontainers.PostgreSql 4.16.0`, `tests/Shared/PostgreSqlFixture.cs` ligado por `Compile Include` nos dois `.csproj`, `RepositoryTestBase` com `CriarContexto()` | sobe no Docker 29.7.2 (testes verdes); `HolidayRepositoryTests` (5 testes) |
| P2 Demais repositórios | `FixedIncomeAssetRepositoryTests` (7), `PositionRepositoryTests` (8), `IndexRateRepositoryTests` (20) | os 4 substitutos do SQL existem com os nomes da spec (`Upsert_grava_todas_as_cotacoes_do_lote`, `Upsert_preserva_indexador_data_e_valor_de_cada_cotacao`, `Upsert_com_a_mesma_cotacao_repetida_no_lote_grava_so_a_ultima_sem_erro`, `Upsert_com_colecao_vazia_nao_altera_a_tabela`); novos: `Upsert_de_data_existente_atualiza_o_valor_e_mantem_o_id_da_linha`, `Upsert_repetido_com_o_mesmo_lote_nao_duplica_registros`, `Upserts_concorrentes_com_as_mesmas_datas_deixam_uma_linha_por_data_sem_violar_o_indice_unico`, `Indice_unico_recusa_duas_cotacoes_do_mesmo_indexador_na_mesma_data`, `Aporte_de_titulo_inexistente_e_recusado_pela_chave_estrangeira`, `Remover_titulo_que_tem_aportes_e_recusado_e_o_titulo_continua_gravado`, ida e volta e formato de coluna para título, aporte e cotação |
| P3 `ApiTestBase` e API | `ApiTestBase(PostgreSqlFixture)`; classes migradas; `Microsoft.EntityFrameworkCore.InMemory` removido dos dois `.csproj` | 57 testes de Api verdes no PostgreSQL; `PositionProjectionFlowTests.Fluxo_completo_cria_titulo_aporte_e_obtem_projecao_coerente`; 11 classes `ApiTestBase` mais 3 de fábrica própria |
| P4 Job contra o banco | `Integration/IndexRatesImportIntegrationTests.cs` (6) com stub de `HttpMessageHandler` e `TimeProvider` stub | `Primeira_execucao_grava_o_cdi_e_o_ipca_buscados_desde_2020_01_01_ate_hoje`, `Segunda_execucao_no_mesmo_dia_nao_duplica_nem_altera_os_registros`, `Execucao_incremental_do_cdi_pula_paixao_fim_de_semana_e_tiradentes_ate_o_proximo_dia_util`, `Cotacao_de_cdi_em_dia_nao_util_e_descartada_e_nao_grava_linha`, `Ipca_ja_importado_ate_o_mes_corrente_nao_e_buscado_de_novo`, `Job_de_importacao_inteiro_grava_as_cotacoes_no_banco_via_conteiner_de_di` |
| P5 Regras de arquitetura | `FixedIncome.Architecture.Tests`, `NetArchTest.Rules 1.3.2`, no `FixedIncome.sln` | 7 testes verdes; prova de violação por relato |
| P5b Independência | `grep`, classe a classe, inversão | `grep` e classe a classe refeitos aqui; inversão por relato |
| P6 Stryker: medição | `dotnet-stryker 4.16.0`, `stryker-config.json`, `StrykerOutput/` no `.gitignore` | `dotnet stryker` roda e imprime o score |
| P7 Matar sobreviventes | +40 testes no Domain; 2 sobreviventes aceitos; 21 desabilitados | score 99,33%, e 95,30% sem os `disable` |
| P8 Workflows | `ci.yml` (passo de arquitetura, comentários), `mutation.yml` | `actionlint` exit 0, `zizmor` 0 achados |
| P9 Documentação | `README.md` (+73 linhas), `CLAUDE.md`, `docs/ESPECIFICACAO.md` (linhas "Testes de integração (v2)", "Regras de arquitetura (v2)" e "Mutation testing (v2)"; `ESPECIFICACAO.md:260-262` lidas), `.gitignore` | releitura; a contagem e a tabela de cobertura do README são do orquestrador (ver abaixo) |

Decisões registradas: `Testcontainers.PostgreSql 4.16.0` (sobe no Docker 29.7.2; o risco de
compatibilidade da spec não se materializou); `NetArchTest.Rules 1.3.2`; `dotnet-stryker 4.16.0` (a
5.0.0 não instala no SDK 8); `rollForward: false` nas duas ferramentas.

## Definição de pronto (itens verificáveis aqui)

| Item | Evidência | Resultado |
|---|---|---|
| Build sem warnings | `0 Warning(s)  0 Error(s)` em Debug e Release | atendido |
| `dotnet test` verde, sem Skip, contagem não cai | 656 contra 591 | atendido |
| Sem biblioteca de mock | nenhum pacote de mock nos `.csproj`; dublês `private sealed` (por exemplo o handler e o `TimeProvider` stub no `IndexRatesImportIntegrationTests`) | atendido |
| Domain sem referência nova | `FixedIncome.Domain.csproj` fora do diff | atendido |
| Pacotes dentro da lista autorizada | só `Testcontainers.PostgreSql`, `NetArchTest.Rules` e `dotnet-stryker` entram; `InMemory` sai | atendido |
| Cobertura Domain e Application acima de 80% | 96,3% e 92,6% | atendido |
| PR, commit e CI no GitHub | fora do alcance do verificador | pendente do Matheus |

## Itens abertos e pendências (honestidade)

| # | Item | O que falta |
|---|---|---|
| i | Testes de integração no CI (critério 1) e relatório do Stryker como artefato (critério 4) dependem de execução real no GitHub | PR com o `ci.yml` verde (os passos de Infrastructure e Api usam o Docker do runner e baixam `postgres:16` e Ryuk; ficar atento a `toomanyrequests` do Docker Hub) e uma execução manual do `mutation.yml` (`workflow_dispatch`, que só aparece depois do merge na `main`) com o artefato `stryker-report` listado |
| ii | Mensagem em português de "sem Docker" | provada só por simulação do implementador (um `throw DockerUnavailableException` descartável dentro do `try` do `InitializeAsync`) e por leitura do código; **não** testada com o Docker de fato parado (aqui também não: o Docker ficou ligado todo o tempo) |
| iii | Série vazia de IPCA | o teste `PositionTests` "IPCA sem nenhuma cotação publicada" (`Não há cotação de IPCA publicada ...`) e `PositionIpcaProjectionTests.Ipca_sem_nenhuma_cotacao_publicada_lanca_excecao` fixam que a projeção lança exceção; é requisito implícito da RN-10 (sem IPCA algum não há o que estimar), a decidir pelo Matheus se vira regra explícita na especificação |
| iv | Tabela de cobertura e contagem do README | a atualizar pelo orquestrador com os números desta verificação: 656 testes; Domain 96,3% / 100%; Application 92,6% / 98,9%; Infrastructure 99,7% / 98,4%; Api 96,4% / 91,3%; score 99,33% |
| v | Reprodução independente da prova do P5 | do orquestrador, em `git clone --local` no scratchpad; esta verificação não alterou código |
| vi | Inversão total da ordem de execução (P5b) | por relato; não repetida aqui |
| vii | PostgreSQL 17 instalado como serviço do Windows (parado) | ressalva do critério 1: nada o usa; vale parar ou não tratar como ambiente "sem PostgreSQL" ao comparar com o runner |

Observações que não bloqueiam:

1. **Pastas antigas em `StrykerOutput/`.** A pasta local acumula uma por execução (esta criou
   `2026-11-09.14-15-53`). No runner só existe a da execução, então o artefato sai limpo; é
   `.gitignore`.
2. **Dois contêineres `postgres:16` por `dotnet test` da solution** (um por assembly), com o custo de
   pull de duas imagens no runner na primeira vez; as execuções seguintes reaproveitam o cache.
3. **Tempo de `dotnet test` da solution** sobe de poucos segundos para cerca de 1 minuto (62 s aqui)
   por causa do Docker; o teto de 60 s da spec vale por projeto e está respeitado.
4. **Comentário `Stryker disable` do `DecimalMath.cs:56`** explica que o `<<=` é silenciado por
   efeito colateral. Está correto e conferido (item "Segunda lista"); só é um comentário longo.

## Veredito

**APROVADA**, com os itens abertos acima.

Tudo o que podia ser provado sem uma execução real do GitHub foi provado: build com 0 avisos
(Debug e Release), 656 testes verdes (591 antes), nenhum `Skip`, cobertura acima da meta (Domain
96,3%, Application 92,6%), integração contra PostgreSQL de verdade pelo Testcontainers (contêineres
vistos no `docker ps`, nenhuma referência a banco fixo nos testes), sete regras de arquitetura
verdes e o desenho conferido, Stryker com 99,33% (95,30% sem nenhum `disable`), 21 desabilitações
listadas e auditadas por contraprova, independência dos testes com 17 classes de integração
passando sozinhas, `actionlint` 1.7.7 e `zizmor` 1.14.2 limpos, e nenhum commit criado.

Fica aberto até haver execução real no GitHub:

- [ ] Testes de integração no CI (critério 1 do ROADMAP, lado do CI): link do `ci.yml` verde num PR
- [ ] Relatório do Stryker como artifact do workflow (critério 4): link da execução manual do `mutation.yml` com o artefato `stryker-report`

Critérios que este arquivo aprova (o orquestrador pode marcar):

- [x] Mutation score do Domain em pelo menos 80% (critério 3): 99,33%
- [x] Violar uma regra de arquitetura quebra o build (critério 2): regras e estado da árvore conferidos aqui; a prova do P5 é relato, com a reprodução do orquestrador a anexar
- [x] Nenhum teste depende da ordem nem dos dados de outro (critério 5): inversão total por relato, com a limitação do xUnit 2 registrada
- [~] Testes de integração com `dotnet test`, local e no CI, sem PostgreSQL instalado (critério 1): local atendido; o CI fica aberto

Pendência do orquestrador: cumprida no fechamento (README, ROADMAP e linha 142 corrigidos; critérios
aprovados marcados; reprodução independente da prova do P5 no QA9-17, `docs/QA-F9-parteA.md`).

## Correções pós-QA

Data: 09/11/2026. O QA (`docs/QA-F9.md`, `docs/QA-F9-parteA.md`) não achou bug; Matheus decidiu zerar os
riscos: corrigir uns, aceitar outros com o motivo no ROADMAP (seção "Riscos do QA da F9"). Os itens
abaixo foram feitos com o teste antes do código, quando havia código.

| Risco | O que mudou | Evidência |
|---|---|---|
| R9-04 | `PostgreSqlFixture.CreateStartupFailure` traduz qualquer falha de inicialização na mensagem em português (agora citando `DOCKER_HOST`), com a causa em `InnerException`; o `catch` deixou de ser só de `DockerUnavailableException` | `PostgreSqlFixtureTests` (2 testes) vermelhos por não compilar, depois verdes. Com `DOCKER_HOST=npipe:////./pipe/docker_engine` (o formato malformado do QA) a saída trouxe a mensagem nova, com `TypeInitializationException` como causa |
| A-R1, A-R2 | Teste `Nenhum_tipo_fora_da_infrastructure_depende_do_dbcontext_nem_do_npgsql` sobre Domain, Application e Api | Violação descartável em `FixedIncome.Api.Descartavel` (um tipo com `FixedIncomeDbContext`, outro com `NpgsqlConnection`): `Failed: 1, Passed: 7`, os dois tipos nomeados. Pasta removida: `Passed: 8`; `git status` sem resíduo em `src/FixedIncome.Api` |
| A-R4 | "Testes de Arquitetura" passou para logo depois do build no `ci.yml` | `actionlint` 1.7.7 e `zizmor` 1.14.2 (`--offline --persona=pedantic`) sem achados |
| A-R5 | `release.yml`: `paths-ignore` também para `tests/**` e `.github/workflows/mutation.yml` (o `stryker-config.json` mora em `tests/`) | `actionlint` e `zizmor` limpos |
| A-R7 | `global.json`: SDK 8.0.100, `rollForward: latestFeature` | Build e testes verdes com o SDK 8.0.427 local |
| R9-02 | `**/StrykerOutput` no `.dockerignore` | Arquivo conferido |
| R9-01, A-R6, A-R8 | README: onde rodar o Stryker e como limpar a pasta; `schedule` só vale depois do merge na `main`; SDK 8 e `global.json`; por que `postgres:16` nos testes e `16-alpine` no Compose; mensagem de "sem Docker" e o aviso sobre a repetição; regra 3 atualizada; contagens (659; Infrastructure 150; Architecture 8) | README relido |
| A-R9 | `CLAUDE.md` alinhado ao `.gitignore` e à `ESPECIFICACAO.md`: `graphify-out/` é local, fora do versionamento | `.gitignore` linha 487 |

Aceitos no ROADMAP, sem mudança de código: R9-03, R9-05, R9-06, QA9-03c, QA9-09c, QA9-05b e o CI em duplicata
na `main`.

Conferência completa depois das correções:

- `dotnet build`: 0 warnings, 0 erros.
- `dotnet test`: **659 verdes**, 0 ignorados (Domain 332, Application 112, Architecture 8, Infrastructure 150, Api 57). Antes das correções: 656.
- Api.Tests e Infrastructure.Tests em 19 s cada, abaixo do teto de 60 s.
- `actionlint` 1.7.7 e `zizmor` 1.14.2 (`--offline --persona=pedantic`): sem achados nos três workflows.
- `dotnet stryker` em `tests/FixedIncome.Domain.Tests`: **99,33%**, exit 0 (igual ao anterior).

## Execuções no GitHub

Fecham os dois critérios que a validação local deixou abertos. Conferido pela API pública do GitHub em 10/11/2026: o CI do PR passou em todos os passos (arquitetura logo após o build, Infrastructure e Api contra o PostgreSQL do Testcontainers); CI e Release da main verdes no merge 699aa29; o mutation.yml publicou o artefato stryker-report.

| Critério do ROADMAP | Execução | Resultado |
|---|---|---|
| Testes de integração no CI (critério 1, lado do CI) | PR #11, [`ci.yml` 42725508992](https://github.com/matheuscardosolazzarini/renda-fixa-api/actions/runs/42725508992) | Verde, com os testes de Infrastructure e Api contra o PostgreSQL do Testcontainers no runner |
| O mesmo, depois do merge | Merge `699aa29` na `main` | CI e Release verdes |
| Relatório do Stryker como artifact (critério 4) | [`mutation.yml` manual, 42727252936](https://github.com/matheuscardosolazzarini/renda-fixa-api/actions/runs/42727252936) | Artefato `stryker-report` publicado |

Com elas, os cinco critérios da seção F9 do ROADMAP estão marcados e a fase está concluída (PR #11, merge
`699aa29`, 659 testes).
