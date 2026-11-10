# Spec de execução — F9: Qualidade (Testcontainers, NetArchTest e Stryker.NET)

Status: aprovada (09/11/2026)

Decisões do Matheus na aprovação: o risco do Docker 29 é provado no P1; o teto de 60 s do Api.Tests
vale como descrito; o score de partida do Stryker é medido no P6 e mutante equivalente pode ser
desabilitado com o motivo escrito; sem gatilho temporário de workflow (os testes de integração no CI
se provam no PR, e o `mutation.yml` é executado manualmente pelo Matheus depois do merge); a linha 142
do ROADMAP é corrigida no fechamento.

Documento de execução para agente. Objetivo: os testes de repositório e de ponta a ponta passam a
rodar contra um PostgreSQL de verdade, as regras de camada do `CLAUDE.md` viram testes que quebram
o build, e o Domain passa a ter mutation score de pelo menos 80%, medido pelo Stryker.NET.

## Contexto

A especificação funcional está em `docs/ESPECIFICACAO.md`. As convenções de código estão em
`CLAUDE.md`. Ambas prevalecem sobre qualquer suposição. O plano da v2 está em `docs/ROADMAP.md`,
seção "F9".

Estado de partida: 591 testes (Domain 292, Application 112, Infrastructure 130, Api 57), build sem
warning, na branch `f9-qualidade`. Os testes de repositório e o `ApiTestBase` usam o provedor
InMemory do EF Core; o `IndexRateRepositoryTests` testa o `UpsertAsync` sem banco, capturando o SQL
por interceptador, e deixa escrito que o efeito real do `ON CONFLICT` "é verificado pelo race.sh e,
na F9, por Testcontainers". O `ci.yml` roda os quatro projetos de teste em sequência, com cobertura.
O Docker local é a versão 29.7.2.

O que o InMemory não reproduz e o PostgreSQL reproduz: chave estrangeira (`Position.AssetId`, com
`DeleteBehavior.Restrict`), índice único `(index_type, reference_date)`, precisão de coluna
(`numeric`), ordenação de texto pelo collation do banco, enum gravado como texto, `ON CONFLICT` e o
esquema criado pelas **migrations** em vez de `EnsureCreated`. É esse intervalo que a fase cobre.

Esta fase toca:

- `tests/FixedIncome.Infrastructure.Tests` — base de repositório, repositórios, job de importação
- `tests/FixedIncome.Api.Tests` — `ApiTestBase` e as classes que herdam dele
- `tests/FixedIncome.Architecture.Tests` — projeto novo
- `tests/Shared` — fonte compartilhada da fixture do PostgreSQL (ver "Decisões de desenho")
- `tests/FixedIncome.Domain.Tests` — testes novos que matam mutantes
- `.config/dotnet-tools.json`, `FixedIncome.sln`, `.github/workflows/` — Stryker, solution, `ci.yml`
  e `mutation.yml`
- `README.md`, `CLAUDE.md` e `docs/` — documentação da fase
- Código de produção: **nada**, exceto os dois casos de "Restrições" (bug revelado pelo banco real e
  mutante equivalente)

## Fora de escopo

- Regras de cálculo, entidades, casos de uso, controllers e migrations: nenhuma muda
- Stryker em Application, Infrastructure ou Api; Stryker como condição de PR (ele é lento e fica em
  workflow à parte)
- Biblioteca de limpeza de banco (Respawn) e reaproveitamento de contêiner entre execuções
  (`WithReuse`)
- Teste de deriva entre o modelo do EF e as migrations
- Testes de carga, de contrato ou do `Dockerfile`
- Itens abertos que não são desta fase: "R-01 ao vivo" e "Calendário ANBIMA só até 2030"
- F10: `/health/ready`, OpenTelemetry, Redis, outbox

## Pacotes e ferramentas autorizados

Pacotes NuGet novos, todos só em projetos de teste, com versão fixa (sem faixa) e confirmada no
passo em que entram:

| Pacote | Projeto | Por quê |
|---|---|---|
| `Testcontainers.PostgreSql` | Infrastructure.Tests, Api.Tests | sobe o PostgreSQL 16 por `dotnet test`, sem banco instalado na máquina nem no runner; é a decisão registrada em "Testes de integração (v2)" da especificação |
| `NetArchTest.Rules` | Architecture.Tests | expressa as regras de camada do `CLAUDE.md` como teste; é a decisão registrada em "Regras de arquitetura (v2)" |

Ferramenta, não pacote de projeto: `dotnet-stryker` entra no `.config/dotnet-tools.json`, ao lado do
`dotnet-ef`, com versão fixa. Nenhum `.csproj` o referencia. Decisão "Mutation testing (v2)" da
especificação.

Sai um pacote: `Microsoft.EntityFrameworkCore.InMemory`, dos dois `.csproj` de teste, quando nenhum
teste mais o usar (o `AddInMemoryCollection` de configuração é outro pacote e fica).

As ações do GitHub do `mutation.yml` são as mesmas do `ci.yml` (`checkout`, `setup-dotnet`,
`upload-artifact`), fixadas pelo mesmo SHA, como manda a decisão R-03 da F8.

**Risco de compatibilidade, a provar no P1:** o Docker local é o 29.7.2. Versões antigas de clientes
Testcontainers falharam contra o Docker 29 por causa da versão mínima da API do Docker. O
implementador escolhe a versão estável mais recente do `Testcontainers.PostgreSql` que suporte
`net8.0` e prova no P1 que o contêiner sobe neste Docker. Se não subir com nenhuma versão estável,
PARE e reporte. O pacote também pode trazer um `Npgsql` diferente do que o EF usa: se o NuGet
acusar rebaixamento (NU1605) ou o build gerar warning, reporte em vez de silenciar.

## Decisões de desenho

**Um contêiner por coleção, um banco por teste.** O PostgreSQL sobe uma vez por coleção de testes
(`ICollectionFixture`), como pede o ROADMAP. Na subida, a fixture cria um banco-modelo
(`template_fixedincome`), aplica nele as **migrations** (`Database.MigrateAsync()`, as mesmas que o
bundle da F8 aplica) e fecha as conexões dele (`NpgsqlConnection.ClearAllPools()`, porque o
`CREATE DATABASE ... TEMPLATE` exige o modelo sem conexões). Cada teste recebe um banco próprio por
`CREATE DATABASE "test_<guid>" TEMPLATE template_fixedincome` e o descarta no fim
(`DROP DATABASE ... WITH (FORCE)`). Alternativas descartadas: limpar tabelas entre testes
(biblioteca nova, e o teste seguinte herda o estado se a limpeza falhar) e rollback de transação
(não vale para o `WebApplicationFactory`, onde a requisição abre a própria conexão). Banco por teste
é isolamento por construção: nenhum teste enxerga dados de outro, em qualquer ordem. O seed de
feriados já vem no modelo, porque está nas migrations.

Detalhes da fixture: `CREATE DATABASE` e `DROP DATABASE` usam uma conexão administrativa ao banco
`postgres` (nunca ao modelo) e fora de transação; o nome é `test_{Guid:N}`, sem hífen; antes do
`DROP ... WITH (FORCE)` limpa-se o pool da connection string daquele teste
(`NpgsqlConnection.ClearPool`); `ClearAllPools()` só roda no setup, nunca durante os testes (limparia
os pools dos testes em andamento). O `[CollectionDefinition("PostgreSQL")]` mora no próprio
`PostgreSqlFixture.cs`, que compila em cada assembly. Cada classe concreta leva
`[Collection("PostgreSQL")]` explícito, em vez de depender da herança do atributo: fica legível e
verificável por `grep`.

**Consequência aceita: coleção única serializa as classes.** Hoje cada classe do `ApiTestBase` é uma
coleção implícita e roda em paralelo; com a coleção única elas rodam em série, cada teste com um
`WebApplicationFactory` e um `CREATE DATABASE` (no PostgreSQL 16 o padrão é `STRATEGY=WAL_LOG`, o que
tende a custar de 100 a 300 ms, a medir). O xUnit 2 não tem fixture de assembly, e manter o
paralelismo exigiria estado estático, que a regra 2 proíbe. O P1 e o P3 medem o tempo da suíte antes
e depois; se o `Api.Tests` passar de 60 s, o passo para e reporta, e o teto aceito é o que o Matheus
aprovar com esses números.

**Imagem.** `postgres:16`, a mesma do smoke do `release.yml` (o `docker-compose.yml` usa a
`16-alpine`). A ordenação de texto depende do collation da imagem (glibc na `postgres:16`; musl, com
comportamento próximo de C, na alpine). Nenhum requisito pede ordem por collation: o teste existente
`Listar_todos_retorna_titulos_ordenados_por_nome` migra com nomes em que qualquer collation concorda
(mesma caixa, só letras ASCII), e nenhum teste novo afirma ordem dependente de collation. Sem digest: o banco do teste não guarda
segredo, e o digest exigiria manutenção manual (a limitação já aceita na F8).

**Fixture compartilhada por arquivo ligado.** Infrastructure.Tests e Api.Tests são assemblies
separados e os dois precisam da fixture. Ela vive em `tests/Shared/PostgreSqlFixture.cs` e entra nos
dois projetos com `<Compile Include="..\Shared\PostgreSqlFixture.cs" Link="PostgreSqlFixture.cs" />`.
Alternativas descartadas: projeto de suporte novo (uma camada que a spec não pede) e duplicar o
arquivo (as duas cópias divergem). Cada assembly sobe o próprio contêiner, uma vez.

**Arrange e Assert por contextos diferentes.** O teste grava com um `DbContext` e lê com outro. Com o
mesmo contexto o `Find`/`ToListAsync` pode devolver a entidade do change tracker sem ir ao banco, e o
teste passa sem provar a persistência. Vale para todo teste de repositório e de job.

**Sem Docker, o teste falha.** Se o Docker não estiver em execução, a fixture lança exceção com
mensagem clara ("os testes de integração precisam do Docker em execução"). Nada de `Skip`: um verde
que não tocou o banco é pior que um vermelho.

**Como o `ApiTestBase` aponta para o banco do teste.** Removendo o descritor de
`DbContextOptions<FixedIncomeDbContext>` e registrando `AddDbContext(o => o.UseNpgsql(<connection
string do teste>))`, como o `ApiTestBase` já faz com o InMemory. Fica **proibido** apontar o banco por
`UseSetting("ConnectionStrings:DefaultConnection", ...)` ou por variável de ambiente: o `Program.cs`
lê a variável `ConnectionStrings__DefaultConnection` antes da configuração, e uma variável definida
na máquina ou no runner venceria o teste. O `Program` continua precisando de uma connection string
não vazia para não sair com `return 1`; ela vem do `appsettings.Development.json`
(`localhost:5432`) e nunca é usada pelo teste.

**Todas as classes do `ApiTestBase` passam para o PostgreSQL.** Uma base só, um jeito só. As que não
precisam de banco (erros de rota, modelo inválido) pagam a criação de um banco a partir do modelo,
da ordem de dezenas de milissegundos. Os testes com `WebApplicationFactory` próprio, que não herdam
do `ApiTestBase` (partida sem connection string, log de partida, composição do job), não mudam.

**Regras de arquitetura (NetArchTest).** Três regras, as do ROADMAP, cada uma num teste próprio e
com mensagem que diz qual tipo violou:

1. Domain: nenhum tipo depende de `FixedIncome.Application`, `FixedIncome.Infrastructure`,
   `FixedIncome.Api`, `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore`,
   `Microsoft.Extensions` nem `Npgsql`; e `GetReferencedAssemblies()` do Domain só contém
   assemblies do framework (`System.*`, `netstandard`, `mscorlib`), em lista branca. Limitação: o
   compilador omite do metadata uma `ProjectReference` sem uso, então esse teste não a enxerga; o
   `dotnet list reference` do `revisor-arquitetura` cobre esse caso.
2. Application: nenhum tipo depende de `FixedIncome.Infrastructure`, `FixedIncome.Api`,
   `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore` nem `Npgsql` (verdadeiro hoje: só usa
   `Microsoft.Extensions.DependencyInjection` e `Logging`).
3. Controllers (namespace `FixedIncome.Api.Controllers`): nenhum tipo depende de
   `FixedIncome.Infrastructure` nem de `Microsoft.EntityFrameworkCore`. É um pouco mais amplo que o
   ROADMAP ("não acessam o `DbContext`"), porque o `DbContext` vive em `FixedIncome.Infrastructure`
   e o `CLAUDE.md` manda o controller só chamar o caso de uso. Verificado hoje: nenhum controller
   nem o middleware importa Infrastructure ou EF. Se alguma regra falhar na primeira execução por
   código existente, o passo para e reporta; o código não é alterado para a regra passar.

Cada regra tem também um teste de que o conjunto avaliado não é vazio (o namespace existe e tem
tipos): o NetArchTest passa em silêncio quando o filtro não casa com nada.

**Mutation testing.** Stryker.NET sobre `src/FixedIncome.Domain`, com os testes do
`FixedIncome.Domain.Tests`. A configuração fica em `tests/FixedIncome.Domain.Tests/stryker-config.json`,
com `"thresholds": { "high": 90, "low": 80, "break": 80 }` (a execução falha abaixo de 80; a
ferramenta exige `break <= low <= high`). Relatórios `html` e `json`. Mutante desabilitado conta como
ignorado e sai do denominador do score, e por isso cada um vai para a lista do `VALIDACAO-F9.md`. Nada é excluído em bloco (`mutate`/`ignore-*`)
sem justificativa escrita na spec. O score é o "Mutation score" que o Stryker imprime e grava no
relatório.

**Valor esperado de teste novo de cálculo não vem de rodar o código.** Um teste que mata um mutante
em `Domain` confere um número ou uma regra; se o valor esperado for copiado da saída do código,
o teste é circular e prova nada. Para teste novo que afirme um valor de rentabilidade, IR, IOF,
dias úteis ou arredondamento, o orquestrador chama o `especialista-renda-fixa` em modo oráculo e o
valor esperado vem dele (ou dos casos de referência já conferidos à mão na `SPEC-F7-EXECUCAO.md`).
Teste que só afirma invariante do domínio (exceção por argumento inválido, limite de faixa já
documentado) dispensa o oráculo.

**Mutante equivalente.** Só pode ser desabilitado com `// Stryker disable once <mutador> : <motivo>`
no código, quando o implementador demonstra que a mutação não muda o comportamento observável. Cada
desabilitação entra numa lista no `VALIDACAO-F9.md`, com o motivo, e o `revisor-arquitetura` confere
uma a uma. Desabilitar mutante que sobrevive por falta de teste é proibido. Código morto que o
Stryker revelar é reportado, não removido.

**Workflow do Stryker.** `.github/workflows/mutation.yml`, `on: schedule` (semanal, segunda-feira) e
`workflow_dispatch`; `permissions: contents: read`; `persist-credentials: false`; ações fixadas por
SHA; roda `dotnet tool restore` e `dotnet stryker` em `tests/FixedIncome.Domain.Tests`; publica
`tests/FixedIncome.Domain.Tests/StrykerOutput` como artefato (`stryker-report`, `overwrite: true`)
com `if: always()`, para o relatório existir também quando o score ficar abaixo do limite;
`timeout-minutes` explícito no job (o padrão do GitHub é 360).

**Evidência que depende do GitHub.** O `ci.yml` só prova que roda os testes de integração com uma
execução real (PR para a `main`), e o `mutation.yml` só aparece para `workflow_dispatch` depois de
estar na `main`. Como na F8, esses critérios ficam abertos no ROADMAP até haver o link da execução;
antes do merge o que existe é `actionlint`, `zizmor` e a reprodução local. Incluir a branch num
gatilho temporário para provar o `mutation.yml` antes do merge é decisão do Matheus, não da entrega.

## Regras novas

Esta fase não cria regra de negócio. As regras de qualidade que ela estabelece, e que o
`revisor-arquitetura` confere:

1. Teste de repositório e de ponta a ponta roda contra PostgreSQL real, com o esquema criado pelas
   migrations.
2. Nenhum teste depende da ordem de execução nem dos dados de outro: banco por teste, sem estado
   estático mutável nos testes.
3. As três regras de camada do `CLAUDE.md` são testes; violar uma quebra o build.
4. Mutation score do Domain de pelo menos 80%, com mutante equivalente justificado um a um.

## Implementação

Cada passo indica o modelo do `implementador`: Sonnet para passo com desenho ou regra, Haiku para
passo mecânico. Em todo passo de código, o teste vem antes e falha pelo motivo certo. Aqui,
"antes" quer dizer: o teste de integração novo é escrito e visto falhar (ou, na migração de um teste
existente, visto passar no InMemory e depois no PostgreSQL) antes de a base antiga ser removida.

### P1 — Fixture do PostgreSQL e primeiro repositório migrado (Sonnet)

`Testcontainers.PostgreSql` nos dois projetos de teste. `tests/Shared/PostgreSqlFixture.cs` conforme
"Decisões de desenho": sobe o contêiner, cria o banco-modelo com as migrations, expõe a criação de
um banco novo por teste (o método de criação pode ser síncrono, para uso em construtor) e a
exclusão dele. `[CollectionDefinition("PostgreSQL")]` em cada assembly. `RepositoryTestBase` passa a
receber a fixture, cria o banco do teste e o descarta no `Dispose`; expõe um `CriarContexto()` para
abrir contextos novos sobre o mesmo banco. Migre `HolidayRepositoryTests` primeiro, como prova do
desenho: `EnsureCreated_materializa_os_139_feriados_semeados` vira
`Migrations_aplicadas_materializam_os_139_feriados_semeados`.

Reporte: a versão do `Testcontainers.PostgreSql`, o tempo de subida do contêiner e de criação de um
banco por teste, e confirme que subiu no Docker 29.7.2. Se não subir, PARE (ver "Risco de
compatibilidade").

### P2 — Demais repositórios contra o PostgreSQL (Sonnet)

Migre `FixedIncomeAssetRepositoryTests`, `PositionRepositoryTests` e `IndexRateRepositoryTests`,
com Arrange e Assert por contextos diferentes. Remova `CriarContextoSemBanco` e os interceptadores
do `IndexRateRepositoryTests`. Os quatro testes que afirmavam o SQL são **substituídos**, não
apagados:

| Teste antigo | Substituto |
|---|---|
| `Upsert_grava_todas_as_cotacoes_num_unico_insert_que_atualiza_o_valor_no_conflito` | `Upsert_grava_todas_as_cotacoes_do_lote` (as três voltam na consulta por período) |
| `Upsert_envia_id_indexador_data_e_valor_de_cada_cotacao_como_parametros` | `Upsert_preserva_indexador_data_e_valor_de_cada_cotacao` (lido do banco com outro contexto) |
| `Upsert_com_a_mesma_cotacao_repetida_no_lote_envia_so_a_ultima` | `Upsert_com_a_mesma_cotacao_repetida_no_lote_grava_so_a_ultima_sem_erro` |
| `Upsert_com_colecao_vazia_nao_executa_nenhum_comando` | `Upsert_com_colecao_vazia_nao_altera_a_tabela` |

Testes novos, do que o InMemory escondia (cada um com o nome descrevendo o comportamento):

- upsert de data já existente atualiza o valor e mantém o `Id` da linha existente
- upsert repetido com o mesmo lote não duplica registros
- dois upserts concorrentes sobre as mesmas datas, em contextos e conexões distintos
  (`Task.WhenAll`), terminam sem violar o índice único e deixam uma linha por data (a corrida que o
  `race.sh` mostrava). Os dois lotes têm as **mesmas chaves na mesma ordem**, que é o caso real
  (duas instâncias lendo a mesma série do SGS). O `UpsertAsync` não ordena o lote, então lotes com as
  mesmas chaves em ordens diferentes poderiam dar deadlock (`40P01`); isso fica fora do teste, e se o
  implementador o observar é achado de produção (Restrições, exceção 1), não ajuste do teste
- o índice único recusa duas cotações do mesmo indexador na mesma data inseridas pelo EF
- `Position` aponta para título inexistente é recusada pela chave estrangeira; remover um título
  que tem aportes é recusado (`Restrict`)
- valores de `rate` (`numeric(18,6)`), `invested_amount` (`numeric(18,2)`), `index_rate.value`
  (`numeric(12,6)`, o limite mais apertado), `DateOnly` e o enum
  (gravado como texto, conferido por SQL cru na coluna) fazem a viagem de ida e volta sem perda

Atualize o comentário do `IndexRateRepositoryTests` que cita o `race.sh`.

### P3 — `ApiTestBase` e testes de API contra o PostgreSQL (Sonnet)

`ApiTestBase` recebe a fixture, cria o banco do teste a partir do modelo, aponta o `DbContext` do
`WebApplicationFactory` para ele (a connection string do teste substitui a registrada) e remove o
`EnsureCreated`. Ajuste o construtor de cada classe que herda dele. Remova o
`Microsoft.EntityFrameworkCore.InMemory` dos dois `.csproj` de teste, depois de confirmar com
`grep` que nenhum teste o usa (`UseInMemoryDatabase`). Atualize o comentário do `ColumnLimitsTests`
que diz que o InMemory não impõe a precisão da coluna.

Os testes de API existentes passam a rodar no banco real sem mudar de asserção. Entre eles estão
os que já cobrem o que o banco real prova: `AssetsControllerTests.Titulo_com_aportes_nao_pode_ser_removido`
(o 400 vem do `DeleteAssetUseCase`, que checa as posições antes; a chave estrangeira `Restrict` é
exercitada no P2, no repositório), `PortfolioSummaryFlowTests`, `PositionProjectionFlowTests` e
`IndexesControllerTests` (o calendário passa a vir das migrations). Testes **novos** só se, depois
de conferir os existentes, faltar cobertura de ponta a ponta de algo que o PostgreSQL muda: ao
menos um fluxo `POST /api/assets` → `POST` de aporte → `GET` da projeção, com o banco persistindo
entre as requisições (cada requisição usa escopo e contexto próprios). O implementador lista no
relatório o que reaproveitou e o que criou. Se um teste revelar comportamento errado de produção,
vale a regra de "Restrições".

### P4 — Job de importação contra o PostgreSQL, com o Banco Central simulado (Sonnet)

`tests/FixedIncome.Infrastructure.Tests/Integration/IndexRatesImportIntegrationTests.cs`. O
`SgsIndexRateProvider` de verdade, atrás de um `HttpMessageHandler` stub `private sealed` no próprio
arquivo (responde pela série da URL: CDI 12, IPCA 433, no formato real do SGS, copiado dos testes do
provedor), os repositórios e o `ImportIndexRatesUseCase` de verdade, o banco do P1 e um `TimeProvider`
fixo. O `ImportIndexRatesUseCase` recebe um `TimeProvider` e calcula "hoje" com `GetLocalNow()`, que
depende do fuso: o dublê é um `TimeProvider` stub `private sealed` que sobrescreve `GetUtcNow()` e
`LocalTimeZone` (fuso fixo `America/Sao_Paulo`), senão as datas da URL variam com o fuso da máquina.
No teste do job por DI, o `AddApplication` registra o `TimeProvider` com `TryAddSingleton`, então o
dublê é registrado **antes** do `AddApplication` (ou substitui o descritor depois).
`Microsoft.Extensions.TimeProvider.Testing` seria pacote novo e não está autorizado. Casos:

- primeira execução: grava a série de CDI em dias úteis e a de IPCA; o calendário vem das migrations
- segunda execução no mesmo dia: não duplica registros (critério da F7, agora no banco real)
- execução incremental: a requisição de CDI parte do próximo dia útil após a última cotação gravada
  (confere as datas na URL recebida pelo stub, com feriado no meio)
- cotação de CDI em dia não útil é descartada e não grava linha
- IPCA já importado até o mês corrente não é buscado de novo
- um teste do `IndexRatesImportJob` inteiro, via contêiner de DI: `StartAsync`, espera limitada
  (prazo fixo, como o `WaitLimit` dos testes existentes) até as linhas aparecerem no banco,
  `StopAsync`. O `SgsIndexRateProvider` entra com o stub como `PrimaryHandler`; o teste de falha do
  BC com retry fica fora, porque o backoff real do Polly o tornaria lento e os testes do handler já
  o cobrem.

### P5 — Regras de arquitetura (Sonnet)

`tests/FixedIncome.Architecture.Tests` (xUnit, `net8.0`, mesmos pacotes de teste dos outros
projetos, referências aos quatro projetos de `src`), adicionado à solution com `dotnet sln add`.
`NetArchTest.Rules`. Os três testes e os três de "conjunto não vazio" de "Decisões de desenho".

Prova do ROADMAP, uma vez e desfeita, com a saída anexada ao relatório do passo. (a) O Domain não
tem nenhum `PackageReference` nem `ProjectReference`, então um campo `DbContext` só daria erro de
compilação (CS0246) e não exercitaria a regra. A violação da regra 1 é, portanto, acrescentar ao
`Domain.csproj` o `PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.11"`
**temporário** (exceção explícita e única à proibição de pacotes extras, desfeita no mesmo passo) e
um campo `DbContext` numa classe do Domain; `dotnet test` precisa ficar vermelho **no teste da regra
1**, com a mensagem nomeando o tipo, e não por erro de compilação. (b) Desfaça. (c) Faça um
controller depender do `FixedIncomeDbContext` (a Api já referencia Infrastructure, então compila) e
mostre vermelho na regra 3. (d) Desfaça. Confirme com `git diff --stat` que a árvore voltou ao
estado do passo.

### P5b — Independência dos testes (Sonnet)

Executa e anexa a saída das três evidências de "Testes": o `grep` de estado estático mutável, a
execução de cada classe de integração sozinha e a execução da suíte com ordem de casos e de coleções
invertida. A inversão exige `[assembly: TestCaseOrderer(...)]` e `[assembly: TestCollectionOrderer(...)]`
temporários **em cada um dos cinco assemblies de teste**, executados uma vez e desfeitos; confirme
com `git diff --stat`.

### P6 — Stryker.NET: instalação e medição de partida (Sonnet)

`dotnet-stryker` no `.config/dotnet-tools.json`, `stryker-config.json` conforme "Decisões de
desenho". Rode `dotnet stryker` em `tests/FixedIncome.Domain.Tests` e **registre o score de
partida** e a lista de mutantes sobreviventes e sem cobertura, agrupados por arquivo (guarde o
relatório na pasta de scratchpad; `StrykerOutput/` entra no `.gitignore`). Sem alterar teste nem
código neste passo. Reporte também o tempo da execução.

Se o score de partida já for ≥ 80%, o P7 se limita aos mutantes que forem teste fácil de adicionar e
o passo termina com a medição final. Se for muito abaixo, o orquestrador decide a ordem dos arquivos
no P7.

### P7 — Matar os mutantes sobreviventes do Domain (Sonnet, com oráculo)

Em rodadas por arquivo, a começar pelos de cálculo, `Position` (`Entities/Position.cs`, onde
está o `Project(...)`) primeiro, por ser o maior, depois `MarketData`, `IncomeTaxTable`, `IofTable`,
`BusinessDayCalendar`, `PortfolioSummary` e `DecimalMath` (`PositionProjection` é um record sem
lógica): para cada sobrevivente, escreva
o teste que o mata, veja-o falhar contra o mutante e passar contra o código real, e rode o Stryker
de novo para confirmar. Teste que afirma valor de cálculo segue a regra "Valor esperado de teste novo
de cálculo não vem de rodar o código": o implementador devolve ao orquestrador a lista de cenários
sem valor esperado e só continua com os valores que o `especialista-renda-fixa` devolver. Mutante
equivalente segue a regra própria. A fase termina o P7 com score ≥ 80% na execução completa.

### P8 — Workflows: `ci.yml` e `mutation.yml` (Sonnet)

- `ci.yml`: passo "Testes de Arquitetura" (`dotnet test tests/FixedIncome.Architecture.Tests`, sem
  cobertura, não há o que cobrir); comentário no passo de Infrastructure e Api dizendo que agora
  usam o Docker do runner (cada assembly baixa `postgres:16` e a imagem do Ryuk; se aparecer
  `toomanyrequests` do Docker Hub, reporte). Os passos e o `workflow_call` existentes não mudam.
- `mutation.yml` conforme "Decisões de desenho".
- `.gitignore`: `StrykerOutput/`.
- `actionlint` 1.7.7 e `zizmor` 1.14.2 (`--offline --persona=pedantic`) sobre `.github`, como no
  `VALIDACAO-F8.md`: zero achados altos e médios; os baixos que sobrarem, justificados.

### P9 — Documentação (Haiku)

- README: como rodar os testes (agora exigem Docker em execução; sem Docker o teste falha com
  mensagem clara), seção "Qualidade" com as três frentes e o comando do Stryker, contagem de testes
  e tabela de cobertura com os números medidos pelo verificador, mutation score medido
- `CLAUDE.md`, seção "Testes": uma linha para o projeto de arquitetura e outra para o Stryker
- `docs/ESPECIFICACAO.md`: linha de "Testes de integração (v2)" com a estratégia (contêiner por
  coleção, banco por teste a partir de um modelo com as migrations) e o `postgres:16`; linha de
  "Mutation testing (v2)" com o limite de 80% e o workflow
- `docs/ROADMAP.md` só é alterado no fechamento, pelo orquestrador

## Testes

Siga a skill `testes-xunit`: AAA com seções separadas, nome descrevendo o comportamento, dublês
`private sealed` e nenhuma biblioteca de mock. Esta fase não tem casos de referência próprios:
os testes de integração conferem comportamento (persistência, restrições, idempotência) e os do P7
seguem a regra do oráculo.

**Independência dos testes.** Além do banco por teste, a evidência de que nenhum teste depende da
ordem é: (a) `grep` sem campo `static` mutável nos projetos de teste; (b) cada classe de teste de
integração passa sozinha (`dotnet test --filter`); (c) a suíte inteira passa com a ordem de casos e
de coleções **invertida**, por orderers temporários (P5b). Limitação a registrar: no xUnit 2 não há
orderer de classe dentro de uma coleção, então a inversão não muda a ordem entre as classes da
coleção `PostgreSQL`; essa lacuna é coberta pelo item (b) e pelo banco por teste. Os testes de
variável de ambiente já ficam em classe própria (F8, P5).

## Restrições

- Não altere regras de cálculo, entidades, casos de uso, controllers, repositórios nem migrations.
  Exceção 1: se um teste contra o banco real revelar defeito de produção, o implementador **para**,
  reporta o teste e o comportamento observado, e o orquestrador decide com o Matheus; a correção, se
  aprovada, segue teste-primeiro. Exceção 2: `// Stryker disable once` para mutante equivalente
  demonstrado.
- Não adicione pacotes além dos três itens de "Pacotes e ferramentas autorizados" nem ações de
  terceiros além das do `ci.yml`. A única exceção é o `PackageReference` temporário da prova do P5.
- Não use `Skip`, `Fact(Skip=...)`, `Trait` para excluir teste do CI nem `continue-on-error` para
  esconder falha de integração.
- Não suba o PostgreSQL por `docker compose` nem dependa dele nos testes; o contêiner é do
  Testcontainers, em porta aleatória.
- Não crie camadas, interfaces nem projetos além do `FixedIncome.Architecture.Tests`.
- Não execute `git add`, `git commit` nem `git push`.

## Critérios de aceite

Os do ROADMAP, mais a verificação de build e testes:

1. Os testes de integração rodam com `dotnet test`, localmente e no CI, sem PostgreSQL instalado:
   `docker compose down` antes, nenhuma referência a `localhost:5432` nos testes, `dotnet test`
   verde. O lado do CI só se fecha com a execução real do `ci.yml` num PR
2. Violar uma regra de arquitetura de propósito quebra o build: prova do P5 com a saída anexada. O
   `verificador-de-fase` só cria o `VALIDACAO-F9.md`: confere a saída anexada e a árvore limpa. A
   reprodução independente é do orquestrador, em um `git clone --local` no scratchpad, apagado em
   seguida
3. Mutation score do Domain ≥ 80%, medido por `dotnet stryker` na execução completa, com a lista
   de mutantes desabilitados e o motivo de cada um
4. O relatório do Stryker sai como artefato do `mutation.yml`: localmente, o relatório é gerado; no
   GitHub, só se fecha com a execução real do workflow
5. Nenhum teste depende da ordem nem dos dados de outro (as três evidências de "Testes", P5b, com a
   limitação registrada)
6. `dotnet build` sem warning e `dotnet test` verde, com a contagem de testes igual ou maior que
   591, sem `Skip`
7. `actionlint` e `zizmor` limpos nos workflows
8. Nenhum commit foi criado

## Relatório final de cada passo

Reporte: arquivos alterados, resultado de `dotnet build` e `dotnet test` com a contagem, o teste que
falhou antes da mudança (nome e motivo), o tempo de execução da suíte quando o passo o altera, e
qualquer decisão tomada que não estava especificada aqui.
