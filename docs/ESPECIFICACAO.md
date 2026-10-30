# Especificação — API de Controle de Investimentos em Renda Fixa

## Problema

Investidor pessoa física que possui títulos de renda fixa em mais de uma corretora não
tem visão consolidada da carteira. Cada instituição mostra apenas os próprios papéis, e
o cálculo de rentabilidade líquida — que depende de prazo, indexador e tributação — fica
por conta do investidor.

Esta API centraliza o cadastro dos títulos, registra os aportes e calcula a posição
projetada para uma data, já líquida de imposto de renda.

## Escopo

### Dentro do escopo

- Cadastro de títulos de renda fixa (CDB, LCI, LCA, Tesouro Direto)
- Registro de aportes vinculados a um título
- Cálculo de rentabilidade bruta até uma data de referência
- Aplicação da tabela regressiva de IR e das isenções por tipo de título
- Consolidação da carteira

### Dentro do escopo (v2)

- Calendário de dias úteis com os feriados nacionais da ANBIMA
- Importação diária das cotações de CDI e IPCA pela API SGS do Banco Central
- Rentabilidade de títulos Cdi e Ipca calculada sobre dias úteis, base 252
- IOF regressivo sobre resgates com menos de 30 dias
- Deploy contínuo no Azure com infraestrutura como código
- Testes de integração contra PostgreSQL real, regras de arquitetura e mutation testing

### Fora do escopo

- Autenticação e multiusuário
- Resgates parciais
- Marcação a mercado do Tesouro Direto

Na v1, o IOF e a integração com API de mercado também estavam fora: os indexadores usavam
taxas fixas configuradas em `appsettings.json`, porque buscar CDI e IPCA reais exigia
integração externa que multiplicaria o escopo. A v2 inclui os dois. É a parte que aproxima
o cálculo do que o mercado pratica e que exercita integração externa, job agendado e
resiliência.

## Domínio

### Entidades

**FixedIncomeAsset** — o título disponível para investimento.

| Campo | Tipo | Regra |
|---|---|---|
| Id | Guid | |
| Name | string | obrigatório, até 120 caracteres |
| Issuer | string | obrigatório, até 120 caracteres |
| AssetType | enum | CDB, LCI, LCA, TreasuryBond |
| IndexType | enum | PreFixed, Cdi, Ipca |
| Rate | decimal | maior que zero |
| IssueDate | DateOnly | |
| MaturityDate | DateOnly | posterior a IssueDate |

O significado de `Rate` depende de `IndexType`: taxa anual em PreFixed, percentual do CDI
em Cdi, e taxa real anual composta com a variação do IPCA em Ipca (RN-10).

**Position** — o aporte do investidor em um título.

| Campo | Tipo | Regra |
|---|---|---|
| Id | Guid | |
| AssetId | Guid | FK para FixedIncomeAsset |
| InvestedAmount | decimal | maior que zero |
| ApplicationDate | DateOnly | entre IssueDate e MaturityDate do título |

**Holiday** (v2) — feriado nacional do calendário ANBIMA.

| Campo | Tipo | Regra |
|---|---|---|
| Id | Guid | |
| Date | DateOnly | única |
| Name | string | obrigatório, até 120 caracteres |

**IndexRate** (v2) — cotação de um indexador numa data, como publicada pelo Banco Central.

| Campo | Tipo | Regra |
|---|---|---|
| Id | Guid | |
| IndexType | enum | Cdi ou Ipca; PreFixed é rejeitado |
| ReferenceDate | DateOnly | dia útil para Cdi; primeiro dia do mês de referência para Ipca |
| Value | decimal | em percentual: % ao dia para Cdi, % no mês para Ipca |

O par `IndexType` e `ReferenceDate` é único. `Value` não pode ser negativo para Cdi, mas
pode para Ipca: deflação mensal acontece e precisa ser aceita.

### Regras de negócio

**RN-01 — Rentabilidade bruta do PreFixed.** Capitalização composta sobre os dias úteis entre
a data de aplicação e a data de referência (RN-08), com base 252:
`InvestedAmount × (1 + Rate / 100) ^ (DU / 252)`. Na v1, a regra usava 365 dias corridos para
todos os indexadores; a v2 adota a convenção de mercado. Cdi e Ipca seguem a RN-09 e a RN-10.
Os prazos da tabela de IR (RN-03) e do IOF (RN-11) continuam contados em dias corridos.

**RN-02 — Data de referência limitada.** Se a data de referência ultrapassar o vencimento
do título, o cálculo é feito até o vencimento.

**RN-03 — Tabela regressiva de IR.** Alíquota determinada pelo prazo decorrido:

| Prazo | Alíquota |
|---|---|
| até 180 dias | 22,5% |
| de 181 a 360 dias | 20,0% |
| de 361 a 720 dias | 17,5% |
| acima de 720 dias | 15,0% |

O imposto incide apenas sobre o rendimento, nunca sobre o principal.

**RN-04 — Isenção.** LCI e LCA são isentos de IR para pessoa física. A alíquota é zero
independentemente do prazo.

**RN-05 — Aporte fora da vigência.** Aporte com data anterior à emissão ou posterior ao
vencimento é rejeitado.

**RN-06 — Alteração de título com aportes.** Um título que já possui aportes registrados
não pode ter `IssueDate` nem `MaturityDate` alteradas. Permitir isso tornaria
retroativamente inválido um aporte que era válido no momento do registro, violando a
RN-05 sem que ninguém tenha feito nada errado. `Name`, `Issuer` e `Rate` permanecem
alteráveis em qualquer situação.

**RN-07 — Coerência do consolidado.** Os totais do resumo da carteira são obtidos somando
os valores já arredondados de cada projeção individual, não arredondando a soma dos
valores brutos. Um total que não fecha com as parcelas exibidas é lido como erro do
sistema, ainda que a soma sem arredondamento intermediário seja matematicamente mais
precisa. `TotalNetAmount` é sempre igual a `TotalGrossAmount` menos `TotalTaxAmount`, e
igual à soma dos `NetAmount` individuais.

**RN-08 — Dias úteis** (v2). Dia útil é qualquer dia de segunda a sexta que não seja feriado
nacional cadastrado em `Holiday`. A contagem entre duas datas inclui a inicial e exclui a
final: aplicação numa sexta com referência na segunda seguinte conta 1 dia útil. Uma data
fora do período coberto pelo calendário cadastrado gera erro, nunca uma suposição.

**RN-09 — Rentabilidade pós-fixada em CDI** (v2). O fator acumulado é o produto, para cada
dia útil do intervalo da RN-08, de `1 + CDI do dia × Rate / 100`, com o CDI do dia em fração
(série 12 do SGS, publicada em % ao dia). O valor bruto é `InvestedAmount × fator`. As casas
decimais do fator diário e do acumulado seguem a metodologia de DI da B3 e são fixadas na
SPEC-F7. Se faltar cotação para algum dia útil do intervalo, a projeção não é calculada e a
resposta informa a primeira data sem cotação.

**RN-10 — Rentabilidade IPCA+** (v2). O valor bruto é
`InvestedAmount × fator IPCA × (1 + Rate / 100) ^ (DU / 252)`, com DU contado pela RN-08. O
fator IPCA é o produto de `1 + IPCA do mês / 100` dos meses cheios do intervalo, com pro rata
em dias úteis nos meses parciais. Substitui a soma simples de IPCA e taxa usada na v1. Para
um mês cuja variação ainda não foi divulgada, usa-se a última variação mensal publicada, e a
projeção sai marcada como estimada. Quando o IPCA oficial do mês é importado, as projeções
seguintes passam a usá-lo.

**RN-11 — IOF regressivo** (v2). Quando o prazo decorrido (dias corridos da aplicação até a
data de referência, limitada pela RN-02) for menor que 30 dias, incide IOF sobre o rendimento
bruto, pela tabela do anexo do Decreto 6.306/2007:

| Dia | Alíquota | Dia | Alíquota | Dia | Alíquota |
|---|---|---|---|---|---|
| 1 | 96% | 11 | 63% | 21 | 30% |
| 2 | 93% | 12 | 60% | 22 | 26% |
| 3 | 90% | 13 | 56% | 23 | 23% |
| 4 | 86% | 14 | 53% | 24 | 20% |
| 5 | 83% | 15 | 50% | 25 | 16% |
| 6 | 80% | 16 | 46% | 26 | 13% |
| 7 | 76% | 17 | 43% | 27 | 10% |
| 8 | 73% | 18 | 40% | 28 | 6% |
| 9 | 70% | 19 | 36% | 29 | 3% |
| 10 | 66% | 20 | 33% | 30 | 0% |

O IOF é apurado antes do IR, e a base do IR passa a ser o rendimento bruto menos o IOF. A
isenção da RN-04 vale só para o IR: LCI e LCA também pagam IOF. A projeção passa a retornar
alíquota e valor de IOF, e a RN-07 se estende ao consolidado:
`TotalNetAmount = TotalGrossAmount − TotalTaxAmount − TotalIofAmount`.

## Endpoints

| Método | Rota | Descrição |
|---|---|---|
| POST | `/api/assets` | Cadastra título |
| GET | `/api/assets` | Lista títulos |
| GET | `/api/assets/{id}` | Detalha título |
| PUT | `/api/assets/{id}` | Atualiza título |
| DELETE | `/api/assets/{id}` | Remove título sem aportes |
| POST | `/api/positions` | Registra aporte |
| GET | `/api/positions` | Lista aportes |
| GET | `/api/positions/{id}/projection?referenceDate=` | Projeção da posição |
| GET | `/api/portfolio/summary?referenceDate=` | Consolidado da carteira |
| GET | `/api/indexes/{indexType}?from=&to=` | Cotações de um indexador no período (v2) |
| GET | `/health` | Health check (v2, F8) |

A projeção retorna valor investido, valor bruto, rendimento, alíquota aplicada, imposto
devido e valor líquido. Na v2, retorna também alíquota e valor de IOF, e indica se a projeção
é estimada (RN-10).

A F1 previa um health check, mas ele não existe no código da v1. Entra na F8, onde o smoke
test do deploy depende dele.

## Arquitetura

```
src/
  FixedIncome.Domain          entidades, enums, exceções de domínio
  FixedIncome.Application     casos de uso, DTOs, interfaces de repositório
  FixedIncome.Infrastructure  EF Core, configurações, repositórios
  FixedIncome.Api             controllers, middleware, DI
tests/
  FixedIncome.Domain.Tests
  FixedIncome.Application.Tests
  FixedIncome.Infrastructure.Tests
  FixedIncome.Api.Tests
  FixedIncome.Architecture.Tests   regras de camada com NetArchTest (v2, F9)
```

Na v2, `Holiday`, `IndexRate`, o calendário de dias úteis e a tabela de IOF ficam no
domínio. `Application` declara o contrato de leitura das cotações do Banco Central e os
repositórios novos; `Infrastructure` implementa o cliente HTTP do SGS e o job diário de
importação.

Dependências apontam para dentro: Api → Application → Domain, com Infrastructure
implementando as interfaces declaradas em Application.

O cálculo de rentabilidade e a apuração de IR ficam no domínio, não nos casos de uso.
São regras que independem de orquestração e precisam ser testáveis sem infraestrutura.

## Decisões técnicas

| Item | Escolha |
|---|---|
| Runtime | .NET 8 |
| Banco | PostgreSQL 16 |
| ORM | Entity Framework Core 8 com Npgsql |
| Testes | xUnit, padrão AAA, stubs inline |
| Documentação | Swagger / OpenAPI |
| Execução local | Docker Compose |
| CI | GitHub Actions — build e testes |
| Cotações (v2) | API SGS do Banco Central via HttpClient tipado |
| Resiliência HTTP (v2) | Microsoft.Extensions.Http.Resilience |
| Calendário (v2) | Feriados nacionais ANBIMA em tabela própria, carregados por seed |
| Deploy contínuo (v2) | GitHub Actions com OIDC para Azure Container Apps |
| Infraestrutura como código (v2) | Bicep (ver Decisões em aberto, item 1) |
| Taxas fixas de indexador (v2) | Removidas: saem a seção `IndexRates` do `appsettings.json` e o `IndexRatesOptions`. Sem cotação, vale a RN-09 |
| Testes de integração (v2) | Testcontainers com PostgreSQL 16 |
| Regras de arquitetura (v2) | NetArchTest.Rules |
| Mutation testing (v2) | Stryker.NET, em workflow separado |
| Navegação do código | graphify, com o grafo local em `graphify-out/`, fora do versionamento |

Padrões mantidos do projeto: controller herda `ControllerBase` e retorna
`StatusCode(response.StatusCode, response)`; respostas encapsuladas em `ApiResponse<T>`;
configuração de entidade via `IEntityTypeConfiguration` com `HasColumnName` em SNAKE_CASE;
repositórios assíncronos retornando `Task<IEnumerable<T>>`.

Comentários em português, identificadores em inglês.

## Fases

**F1 — Fundação.** Solution com os quatro projetos, referências entre camadas, Docker
Compose com PostgreSQL, health check respondendo.

**F2 — Domínio.** Entidades, enums, validações de invariante, cálculo de rentabilidade e
apuração de IR. Testes de domínio cobrindo RN-01 a RN-05, incluindo as quatro faixas da
tabela regressiva e a isenção de LCI/LCA.

**F3 — Persistência.** DbContext, configurações de entidade, migration inicial,
repositórios. Testes com InMemory.

**F4 — Casos de uso e API.** Casos de uso, DTOs, controllers, tratamento global de exceção
de domínio para 400, Swagger.

**F5 — Consolidação.** Endpoint de projeção e de resumo da carteira.

**F6 — Entrega.** Workflow de CI, README com contexto do problema e decisões, coleção de
requisições de exemplo.

Cada fase termina com os testes passando. F1 a F3 concentram o valor de demonstração —
se o tempo apertar, é melhor entregar até F4 bem feito que chegar em F6 corrido.

### v2

**F7 — Indexadores reais e dias úteis.** Calendário ANBIMA, importação diária de CDI e IPCA
pelo SGS, RN-08 a RN-11 e endpoint de cotações. Inclui a migração do PreFixed para base 252
(RN-01), com os testes da v1 atualizados para valores conferidos à mão, e a remoção das taxas
fixas do `appsettings.json`. Testes cobrindo as fronteiras da tabela de
IOF (dias 29 e 30) e a contagem de dias úteis com feriado, virada de ano e Carnaval.

**F8 — Entrega contínua.** Infraestrutura como código, deploy automático na `main` com OIDC,
migrations aplicadas pelo pipeline, health check e smoke test depois do deploy.

**F9 — Qualidade.** Testes de repositório e de integração contra PostgreSQL real com
Testcontainers, regras de arquitetura com NetArchTest e mutation testing no Domain.

**F10 — Observabilidade, cache e eventos (opcional).** OpenTelemetry, cache das cotações e
eventos com outbox. Só entra se as vagas-alvo pedirem.

Passos e critérios de cada fase da v2 estão em `docs/ROADMAP.md`. O detalhamento de execução
fica em `docs/SPEC-Fx-EXECUCAO.md`, escrito no começo de cada fase.

## Critérios de conclusão

- Testes verdes no CI, com badge visível no README
- `docker compose up` sobe API e banco sem passo manual
- Swagger acessível e com todos os endpoints documentados
- Cobertura de testes acima de 80% nas camadas de domínio e aplicação
- README explicando o problema, as decisões de arquitetura e como executar

Na v2, além dos anteriores:

- Um CDB 100% do CDI calculado pela API bate com a Calculadora do Cidadão do Banco Central,
  com diferença de no máximo R$ 0,01 sobre R$ 1.000
- Um merge na `main` publica a nova versão no Azure sem passo manual
- Testes de integração rodando contra PostgreSQL real no CI
- Mutation score do Domain de pelo menos 80%

## Decisões em aberto

Precisam ser fechadas antes da fase indicada. Ao decidir, registre a escolha na regra ou na
tabela de decisões técnicas e remova o item desta lista.

1. **Bicep ou Terraform (antes da F8).** Bicep é nativo do Azure e dispensa gerenciar state;
   Terraform aparece mais em vagas.
