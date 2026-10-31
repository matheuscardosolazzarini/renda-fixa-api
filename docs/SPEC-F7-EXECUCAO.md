# Spec de execução — F7: Indexadores reais e dias úteis

Status: aprovada (30/10/2026)

Documento de execução para agente. Objetivo: trocar as taxas fixas de indexador por cotações
reais do Banco Central, calcular a rentabilidade sobre dias úteis com o calendário ANBIMA e
aplicar o IOF regressivo antes do IR.

## Contexto

A especificação funcional está em `docs/ESPECIFICACAO.md`. As convenções de código estão em
`CLAUDE.md`. Ambas prevalecem sobre qualquer suposição. O plano da v2 está em
`docs/ROADMAP.md`.

Regras desta fase: RN-01 (revisada para base 252), RN-08, RN-09, RN-10 e RN-11, além da
extensão da RN-07 com o IOF. Todas já estão na especificação: esta fase não altera o texto
dela, só a implementa.

Esta fase toca:

- `src/FixedIncome.Domain` — calendário, cotações, tabela de IOF, cálculo da projeção
- `src/FixedIncome.Application` — contratos, carregamento de dados de mercado, casos de uso
- `src/FixedIncome.Infrastructure` — configurações, seed de feriados, migration,
  repositórios, cliente do SGS e job de importação
- `src/FixedIncome.Api` — endpoint de cotações, registro do job, remoção das taxas fixas
- Os projetos de teste correspondentes

## Fora de escopo

- Health check e qualquer item de deploy (F8)
- Testcontainers e testes de arquitetura (F9): os testes de repositório continuam com InMemory
- Selic, marcação a mercado do Tesouro e resgates parciais
- Cache das cotações (F10)

## Pacotes autorizados

| Pacote | Projeto | Justificativa |
|---|---|---|
| `Microsoft.Extensions.Http.Resilience` | Infrastructure | retry com backoff e circuit breaker no cliente do SGS (RN-09 depende da cotação existir) |
| `Microsoft.Extensions.Hosting.Abstractions` | Infrastructure | `BackgroundService` do job diário de importação |
| `Microsoft.Extensions.Logging.Abstractions` | Application | `ILogger` no `ImportIndexRatesUseCase`, que registra o descarte de CDI em dia não útil e a falha do provedor. Só abstrações, da mesma família do `DependencyInjection.Abstractions` que o projeto já usa; trouxe esse último de 8.0.1 para 8.0.2 por dependência. Autorizado pelo Matheus durante a revisão da fase (30/10/2026) |

Nenhum outro pacote. Testes do cliente HTTP usam `HttpMessageHandler` stub, sem biblioteca de
mock.

## Decisões de desenho

**O domínio continua puro.** `Position.Project` deixa de receber `IndexRates` e passa a
receber um `MarketData`: um objeto de valor com o calendário de dias úteis e as séries de CDI
e IPCA já carregadas em memória. Quem carrega é a camada de aplicação. O domínio não consulta
nada.

**Sem cotação, sem projeção (RN-09).** Faltando CDI para um dia útil do intervalo, o domínio
lança `DomainException` com a primeira data faltante. Não existe fallback.

**IPCA estimado (RN-10).** Mês sem IPCA divulgado usa a última variação publicada, e a
projeção sai com `IsEstimated = true`.

**Rendimento negativo.** Em IPCA+ com deflação o rendimento pode ser negativo. Nesse caso
IOF e IR são zero: imposto não incide sobre perda.

**Arredondamento por projeção (achado B1 da revisão).** Hoje cada campo é arredondado
separadamente, e o líquido pode não fechar com bruto − IR por um centavo (já acontece no
caso de 10% a.a. em 2020). Na v2 a ordem é: `GrossAmount = round2(bruto)`;
`GrossYield = GrossAmount − InvestedAmount`; `IofAmount = round2(GrossYield × IofRate / 100)`;
`TaxAmount = round2((GrossYield − IofAmount) × TaxRate / 100)`;
`NetAmount = GrossAmount − IofAmount − TaxAmount`. Assim cada projeção fecha exatamente e a
RN-07 fecha no consolidado. Nos casos de referência, vale a coluna C6b; a coluna C6a fica só como registro do problema.

**Casas decimais do CDI (achado B2, metodologia B3).** A taxa diária é a série 12 dividida por
100 (8 casas, exata). O fator diário `1 + taxa × Rate / 100` é exato. O acumulado é truncado
em 16 casas a cada dia, e o fator final é arredondado em 8 casas antes de multiplicar pelo
principal.

**Pro rata do IPCA (achado B3).** Para cada mês do intervalo:
`(1 + IPCA do mês / 100) ^ (DU do mês dentro do intervalo / DU total do mês civil)`. É uma
simplificação da convenção de mercado, que usa data de aniversário; o README registra a
diferença. A série 433 vem com duas casas, e o produto das variações difere um pouco da razão
de números-índice usada pela B3; também vai para o README.

**IPCA estimado (achado I3).** O carregamento busca o último IPCA publicado mesmo que ele seja
anterior ao período da projeção, para ter com o que estimar.

**LCA sem IOF (achado I1).** O Decreto 6.306/2007, art. 32, § 2º, V, reduz a zero o IOF em
operações com LCA. LCI e Tesouro Direto pagam. A RN-11 da especificação já foi corrigida.

**Rendimento menor ou igual a zero (achado I6).** `IofAmount` e `TaxAmount` são zero, e
`IofRate` e `TaxRate` continuam os da tabela pelo prazo.

**Data de referência omitida (achado I2).** Quando o resumo da carteira ou a projeção de uma
posição é pedido sem data, usa-se o dia útil seguinte à última cotação de CDI importada, e
não a data de hoje: assim a consulta padrão nunca falha por falta da cotação de ontem. A
data é obtida por `TimeProvider` injetado, não por `DateTime.UtcNow`, para os testes não
dependerem do relógio. Data informada explicitamente e sem cotação continua devolvendo 400
com a primeira data faltante. Sem nenhuma cotação de CDI importada, usa-se a data de hoje
pelo `TimeProvider`, em UTC (simplificação: entre 21h e 24h em Brasília a data de hoje já é a
do dia seguinte). Extensão decidida pelo Matheus na revisão da fase: a projeção ganha a
mesma regra do resumo, e, no resumo sem data, a posição com aporte posterior à data padrão é
projetada na data do aporte (rendimento zero, sem exigir cotação).

**IPCA ausente no meio da série (decidido na revisão da fase).** A estimativa da RN-10 vale
só para meses posteriores ao último IPCA publicado. Mês anterior ao último publicado e sem
cotação lança `DomainException` citando o mês: é falha de importação, não variação ainda não
divulgada.

**Rendimento com o investido arredondado.** `GrossYield = GrossAmount − round2(InvestedAmount)`,
para o rendimento fechar com os campos exibidos. Em produção é idêntico à fórmula acima, porque
`invested_amount` é `numeric(18,2)`.

**Exponenciação em decimal.** `DecimalMath.Pow` (série de atanh para o logaritmo e Taylor para a
exponencial, cerca de 26 dígitos) substitui o `Math.Pow` da v1, que perdia precisão ao
converter `double` para `decimal` e não bateria com os casos de referência de 16 casas.

**Job desligável.** O job de importação lê `IndexRatesImport:Enabled`. Nos testes da API ele
fica desligado, para nenhum teste depender da internet nem do Banco Central.

## Implementação

Cada passo indica o modelo do `implementador`: Sonnet para passo com regra ou desenho, Haiku
para passo mecânico.

### P1 — Calendário de dias úteis (Sonnet)

`Holiday` em `Domain/Entities`, herdando `BaseEntity`, com `Date` (`DateOnly`) e `Name`
(obrigatório, até 120 caracteres).

`BusinessDayCalendar` em `Domain/ValueObjects`, criado a partir de uma coleção de feriados.
O período coberto vai de 1º de janeiro do ano do primeiro feriado a 31 de dezembro do ano do
último.

- `IsBusinessDay(DateOnly date)`
- `CountBusinessDays(DateOnly start, DateOnly end)` — inclui `start`, exclui `end` (RN-08);
  `start == end` devolve zero; `end < start` lança `DomainException`
- `NextBusinessDay(DateOnly date)`
- `BusinessDaysBetween(DateOnly start, DateOnly end)` — os dias úteis do intervalo, na mesma
  convenção, para o produtório do CDI

Data fora do período coberto lança `DomainException` (RN-08: erro, nunca suposição). A
checagem vale para `start` e para `end` menos um dia, já que o fim é exclusivo. A mensagem
informa o período coberto (por exemplo, "data anterior à cobertura do calendário,
2020-01-01").

O seed cobre 2020 a 2030. Estender o calendário é manutenção prevista: basta acrescentar os
feriados publicados pela ANBIMA.

### P2 — Tabela de IOF (Sonnet)

`IofTable` em `Domain/ValueObjects`, estática como `IncomeTaxTable`, com
`RateFor(int elapsedDays)` devolvendo a alíquota percentual da RN-11. Dia 0 devolve zero
(não há rendimento), dias 1 a 29 seguem a tabela, 30 em diante devolvem zero. Prazo negativo
lança `DomainException`.

Comente a origem: anexo do Decreto 6.306/2007.

### P3 — Cotações e dados de mercado (Sonnet)

`IndexRate` em `Domain/Entities`, herdando `BaseEntity`, com `IndexType`, `ReferenceDate` e
`Value`. Invariantes:

- `IndexType.PreFixed` é rejeitado
- Cdi: `Value` maior ou igual a zero
- Ipca: `ReferenceDate` no dia 1 do mês; `Value` pode ser negativo

A entidade não tem o calendário para saber se a data do CDI é dia útil. Essa checagem fica no
`ImportIndexRatesUseCase`, que descarta e registra em log cotação de CDI em dia não útil.

`MarketData` em `Domain/ValueObjects`: calendário, série de CDI (data → % ao dia) e série de
IPCA (mês → % no mês). Remova `IndexRates`.

### P4 — Projeção v2 (Sonnet)

`Position.Project(FixedIncomeAsset asset, DateOnly referenceDate, MarketData market)`.
Remova `FixedIncomeAsset.ResolveAnnualRate`.

Fluxo:

1. Data de cálculo limitada ao vencimento (RN-02).
2. `ElapsedDays` em dias corridos (base do IR e do IOF) e `BusinessDays` pela RN-08.
3. Fator de crescimento por `IndexType`:
    - PreFixed (RN-01): `(1 + Rate / 100) ^ (BusinessDays / 252)`
    - Cdi (RN-09): produtório de `1 + CDI do dia / 100 × Rate / 100` sobre
      `BusinessDaysBetween`. Casas decimais conforme a seção "Casos de referência"
    - Ipca (RN-10): fator IPCA × `(1 + Rate / 100) ^ (BusinessDays / 252)`, com pro rata em
      dias úteis nos meses parciais e estimativa para mês sem IPCA
4. IOF sobre o rendimento (RN-11), zero para LCA.
5. IR sobre o rendimento menos o IOF (RN-03 e RN-04).
6. Líquido = bruto − IOF − IR, na ordem de arredondamento das decisões de desenho.

`PositionProjection` ganha `BusinessDays`, `IofRate`, `IofAmount` e `IsEstimated`. Os
arredondamentos usam duas casas e `MidpointRounding.AwayFromZero`, como hoje.

Os testes de PreFixed da v1 que mudarem de valor recebem os valores novos da seção "Casos de
referência". Nenhum teste é apagado sem um substituto que cubra o mesmo comportamento.

### P5 — Consolidado com IOF (Sonnet)

`PortfolioSummary` ganha `TotalIofAmount`. A RN-07 passa a valer como
`TotalNetAmount = TotalGrossAmount − TotalTaxAmount − TotalIofAmount`, somando valores já
arredondados.

### P6 — Persistência (Haiku)

- `HolidayConfiguration` e `IndexRateConfiguration`, com `HasColumnName` em SNAKE_CASE,
  `IndexType` como texto (mesma decisão dos enums existentes), índice único em `date` e em
  (`index_type`, `reference_date`), `Value` com precisão `numeric(12, 6)`.
- Seed de feriados via `HasData`, com a lista da seção "Feriados ANBIMA" e Ids fixos.
- `IHolidayRepository` e `IIndexRateRepository` em `Application/Repositories`;
  implementação em `Infrastructure/Repositories`. Métodos: listar feriados; listar cotações
  de um tipo num período; obter a última data importada de um tipo; upsert de uma coleção
  (insere ou atualiza pelo par tipo e data).
- Migration `AddHolidaysAndIndexRates`.

### P7 — Cliente do SGS (Sonnet)

`IIndexRateProvider` em `Application`, com
`FetchAsync(IndexType type, DateOnly from, DateOnly to)`. Implementação `SgsIndexRateProvider`
em `Infrastructure`, como HttpClient tipado com `AddStandardResilienceHandler()`.

- Base: `https://api.bcb.gov.br/`
- Rota: `dados/serie/bcdata.sgs.{série}/dados?formato=json&dataInicial={dd/MM/yyyy}&dataFinal={dd/MM/yyyy}`
- Séries: Cdi 12, Ipca 433
- Resposta: lista de `{ "data": "dd/MM/yyyy", "valor": "0.051660" }`; `valor` com ponto
  decimal, lido com `CultureInfo.InvariantCulture`
- O SGS responde com 404 (`Value(s) not found`) quando o período não tem dado, confirmado em
  chamada real em 30/10/2026; janela acima de 10 anos responde 406. O provedor trata só o 404
  como lista vazia; qualquer outro erro propaga com status e corpo, e os transitórios seguem
  para a política de resiliência
- O SGS responde 400 quando `dataInicial` é igual a `dataFinal`. Para período de um dia, o
  provedor consulta a partir da véspera e filtra o resultado para o período pedido (descoberto
  no smoke real da verificação; a importação incremental diária cai sempre nesse caso)
- Consulta acima de 10 anos é dividida em janelas, porque a API limita o período das séries
  diárias

### P8 — Aplicação (Sonnet)

- `MarketDataLoader` (classe concreta, sem interface: não cruza camada) monta o
  `MarketData` para um período a partir dos repositórios.
- `GetPositionProjectionUseCase` e `GetPortfolioSummaryUseCase` passam a usar o
  `MarketDataLoader`. Remova `IndexRatesOptions`. O resumo sem data segue a decisão de
  desenho da data de referência omitida, com `TimeProvider`.
- `ImportIndexRatesUseCase`: para cada tipo, busca desde o dia seguinte à última data
  importada (ou desde 2020-01-01 na primeira execução) até hoje e faz upsert. Falha do
  provedor vira `ApiResponse` de erro com log, sem exceção.
- `GetIndexRatesUseCase` para o endpoint: valida `from <= to` e o tipo diferente de
  PreFixed.
- `PositionProjectionResponse` e `PortfolioSummaryResponse` ganham os campos novos.

### P9 — Job e composição (Sonnet)

`IndexRatesImportJob : BackgroundService` em `Infrastructure`: roda ao iniciar e depois a
cada 6 horas, criando um escopo para resolver o caso de uso. Só é registrado se
`IndexRatesImport:Enabled` for `true` (padrão `true` no `appsettings.json`, `false` na
fábrica de testes da API).

No `Program.cs`, remova a configuração de `IndexRates` e registre o provedor e o job. No
`appsettings.json`, remova a seção `IndexRates` e acrescente `IndexRatesImport`.

### P10 — Endpoint e documentação (Haiku)

- `IndexesController` com `GET /api/indexes/{indexType}?from=&to=`, no padrão dos demais.
- Requisições novas no `FixedIncome.Api.http`.
- README: regras novas, um exemplo de chamada e as simplificações do IPCA (mês civil em vez
  de data de aniversário; produto das variações de duas casas).

## Testes

Siga a skill `testes-xunit`. Valores esperados vêm da seção "Casos de referência", nunca da
saída do código.

**Domínio**

- Calendário: sexta para segunda conta 1; intervalo com feriado nacional; virada de ano;
  Carnaval; `start == end` conta zero; data fora da cobertura lança exceção
- IOF: dias 0, 1, 15, 29, 30 e 31; LCA com IOF zero e LCI pagando IOF
- IR: acrescentar 181, 361 e 721 aos testes de faixa existentes (os dois lados de cada fronteira)
- `IndexRate`: PreFixed rejeitado; CDI negativo rejeitado; IPCA negativo aceito; IPCA fora
  do dia 1 rejeitado
- Projeção PreFixed em base 252 (valores v1 atualizados)
- Projeção Cdi com série curta; CDI faltante lança exceção com a data
- Projeção Ipca com meses cheios, mês parcial, mês estimado e deflação
- IOF antes do IR; LCI pagando IOF e isenta de IR
- RN-07 com `TotalIofAmount`; o fixture de `PortfolioSummaryTests` com líquido zero passa a
  fechar com a identidade bruto − IR − IOF
- Comentários dos testes v1 atualizados: o fator 1,10 só é exato em 2025 porque o ano tem 252
  dias úteis; comente que esse teste vira sentinela do feriado de 20/11

**Aplicação** — `MarketDataLoader` com repositórios stub; importação idempotente (rodar duas
vezes com os mesmos dados não duplica); falha do provedor vira erro sem exceção; validações
do caso de uso de cotações.

**Infraestrutura** — upsert idempotente no repositório; `SgsIndexRateProvider` com
`HttpMessageHandler` stub: parse de data e decimal, resposta vazia, divisão em janelas.

**API** — endpoint de cotações; projeção com os campos novos; job desligado na fábrica de
testes. Confirme que o `ApiTestBase` cria o banco InMemory com `EnsureCreated`, para o seed de
feriados existir nos testes de fluxo.

## Casos de referência

Produzidos pelo `especialista-renda-fixa` (modo oráculo) em 2026-10-30. Todos os números foram calculados com Python `decimal` em precisão de 50 dígitos e conferidos contra as fórmulas abaixo. Nenhum valor veio da saída do código. Os valores intermediários aparecem com 6 a 16 casas; o esperado, com 2 casas, `MidpointRounding.AwayFromZero`.

### Convenções usadas

| Id | Convenção | Fonte |
|---|---|---|
| C1 | Dias úteis: segunda a sexta, exceto feriado nacional ANBIMA (seção "Feriados ANBIMA"). Conta a data inicial e exclui a final. | RN-08; anbima.com.br/feriados |
| C2 | PreFixed: `fator = (1 + Rate/100)^(DU/252)`. | RN-01 |
| C3 | Cdi: `TDIk = SGS12/100` (8 casas, exato); fator diário `1 + TDIk × Rate/100` (exato); acumulado truncado em 16 casas a cada dia; fator final arredondado em 8 casas; bruto = `P × fator final`. | RN-09; B3, Caderno de Fórmulas Debêntures Cetip 21, item 2.1.1. Ver achado B2 da revisão |
| C4 | Ipca: `fator = Π_M (1 + IPCA_M/100)^(DU_M_intervalo / DU_M_total) × (1 + Rate/100)^(DU/252)`. Mês sem IPCA usa o último publicado e marca `IsEstimated`. **Pro rata pela leitura proposta no achado B3**: denominador = DU do mês civil. | RN-10 + achado B3 |
| C5 | IOF sobre o rendimento bruto, pela tabela da RN-11, com prazo em dias corridos. IR sobre (rendimento − IOF), pela RN-03. LCI/LCA com IR zero. Rendimento ≤ 0 zera os valores de IOF e IR; as alíquotas continuam as da tabela pelo prazo (achado I6). | RN-03, RN-04, RN-11; Decreto 6.306/2007, anexo; Lei 11.033/2004, art. 1º |
| C6a | **Spec atual (P4):** cada campo é arredondado a partir do valor sem arredondamento: `Net = round2(g − iof − ir)`. | SPEC-F7 P4 |
| C6b | **Proposta B1:** `G = round2(g)`; `Y = G − P`; `IOF = round2(Y × aliqIOF)`; `IR = round2((Y − IOF) × aliqIR)`; `Net = G − IOF − IR`. | achado B1 da revisão |

Onde C6a e C6b dão resultados diferentes, as duas colunas aparecem lado a lado e a linha fica marcada com **≠**. A escolha é do Matheus (achado B1). Em C6a, **≠** quer dizer que `Gross − Iof − Tax` (arredondados) não fecha com `NetAmount`.

---

### 1. PreFixed em base 252 e os testes v1

Feriados que caem em dia útil nos intervalos usados:

- **2020** (11): 01/01, 24/02, 25/02, 10/04, 21/04, 01/05, 11/06, 07/09, 12/10, 02/11, 25/12. O 15/11/2020 caiu num domingo.
- **2025** (9): 01/01, 03/03, 04/03, 18/04, 21/04, 01/05, 19/06, 20/11, 25/12. Os feriados de 07/09, 12/10, 02/11 e 15/11 caíram no fim de semana.

| Intervalo | Dias corridos | Dias de semana | Feriados em dia de semana | DU |
|---|---|---|---|---|
| [2020-01-01, 2020-12-31) | 365 | 261 | 11 | **250** |
| [2025-01-01, 2026-01-01) | 365 | 261 | 9 | **252** |

#### 1.1 CDB R$ 100.000 a 1% a.a., 2020-01-01 → 2020-12-31 (365 dias corridos, IR 17,5%)

| Passo | Valor |
|---|---|
| DU | 250 |
| fator = 1,01^(250/252) | 1,0099202425607141 |
| bruto | 100.992,024256071 |
| rendimento | 992,024256071 |
| IOF (365 dias) | 0 |
| IR = 992,024256 × 17,5% | 173,604244812 |
| líquido | 100.818,420011259 |

| Campo | C6a | C6b |
|---|---|---|
| GrossAmount | 100.992,02 | 100.992,02 |
| GrossYield | 992,02 | 992,02 |
| TaxRate | 17,5 | 17,5 |
| TaxAmount | 173,60 | 173,60 |
| NetAmount | 100.818,42 | 100.818,42 |
| BusinessDays | 250 | 250 |

#### 1.2 CDB R$ 1.000 a 10% a.a., 2020-01-01 → 2020-12-31 (365 dias corridos, IR 17,5%)

| Passo | Valor |
|---|---|
| DU | 250 |
| fator = 1,10^(250/252) | 1,0991682416248615 |
| bruto | 1.099,168241625 |
| rendimento | 99,168241625 |
| IR = 99,168241625 × 17,5% | 17,354442284 |
| líquido | 1.081,813799341 |
| C6b: IR = round2(99,17 × 17,5% = 17,35475) | 17,35 |

| Campo | C6a | C6b |
|---|---|---|
| GrossAmount | 1.099,17 | 1.099,17 |
| GrossYield | 99,17 | 99,17 |
| TaxRate | 17,5 | 17,5 |
| TaxAmount | 17,35 | 17,35 |
| NetAmount | **1.081,81 ≠** | 1.081,82 |
| ElapsedDays / BusinessDays | 365 / 250 | 365 / 250 |

#### 1.3 CDB R$ 1.000 a 10% a.a., 2025-01-01 → 2026-01-01 (365 dias corridos)

DU = 252, então o fator é 1,10^(252/252) = 1,10 exato. Bruto 1.100,00; rendimento 100,00; IR 17,5% = 17,50; líquido 1.082,50. C6a e C6b dão o mesmo.

Sentinela: sem o feriado de 20/11/2025 no seed, o DU vira 253, e o bruto, 1.000 × 1,10^(253/252) = **1.100,42**.

Também LCI R$ 500 a 8% a.a. no mesmo intervalo: bruto 540,00, IR 0, IOF 0, líquido 540,00. E LCI R$ 500 a 10% a.a.: 550,00 / 550,00.

#### 1.4 Teste por teste da v1

| Arquivo / teste | Muda? | Novo esperado |
|---|---|---|
| `PositionTests.Construcao_valida_...`, `Valor_investido_menor_ou_igual_a_zero_...`, `Aporte_antes_da_emissao_...`, `Aporte_depois_do_vencimento_...`, `Aporte_exatamente_na_data_de_emissao_...`, `Aporte_exatamente_na_data_de_vencimento_...` | Não | Não usam projeção |
| `PositionTests.Projecao_com_data_de_referencia_anterior_ao_aporte_e_rejeitada` | Não (só assinatura) | Continua lançando `DomainException` |
| `PositionTests.Projecao_e_limitada_ao_vencimento_...` | Não | `ElapsedDays` = 3653. Se for acrescentado: `BusinessDays` = 2504 ([2020-01-01, 2030-01-01)). Exige calendário até 2029 |
| `PositionTests.Projecao_de_titulo_isento_resulta_em_imposto_zero` | Não | `TaxRate` 0, `TaxAmount` 0. Acrescentar `IofAmount` 0 (365 dias) |
| `PositionTests.Projecao_aplica_a_aliquota_conforme_a_faixa_...` (180/360/720/900) | Não | Só verifica `TaxRate`, que depende apenas dos dias corridos. `ZeroRates` não afeta PreFixed. Exige calendário até 2022-06-19 (900 dias) |
| `PositionTests.Projecao_calcula_imposto_sobre_o_rendimento_e_nao_sobre_o_principal` | **Sim** | GrossYield **992,02**; TaxAmount **173,60**; NetAmount **100.818,42** (caso 1.1). Ajustar o comentário: o rendimento deixa de ser 1.000 |
| `PositionTests.Projecao_com_capitalizacao_composta_...` | **Sim** | Gross **1.099,17**; Yield **99,17**; TaxRate 17,5; Tax **17,35**; Net **1.081,81** (C6a) ou **1.081,82** (C6b); ElapsedDays 365 (caso 1.2). Remover o comentário "fator exato de 1,10" |
| `GetPositionProjectionUseCaseTests.Projecao_valida_...` | Não | 1.100,00 / 100,00 / 17,5 / 17,50 / 1.082,50 (caso 1.3); pode acrescentar `BusinessDays` 252 |
| `GetPositionProjectionUseCaseTests` (404, 404, 400) | Não | Mesmos status |
| `GetPortfolioSummaryUseCaseTests.Caminho_de_sucesso_...` | Não | Não verifica valores. Para referência: CDB 1.082,50 + LCI 550,00 |
| `GetPortfolioSummaryUseCaseTests.Carteira_vazia_...` | Não | Zero |
| `GetPortfolioSummaryUseCaseTests.Data_de_referencia_ausente_usa_a_data_atual` | Não | O esperado é calculado pelo próprio `Project`. São 10 dias, então passa a ter IOF de 66%. Depende do calendário cobrir a data atual (achado M1) |
| `GetPortfolioSummaryUseCaseTests.Titulos_sao_carregados_uma_unica_vez_...` | Não | 30 dias, IOF 0 |
| `PositionProjectionFlowTests.Fluxo_completo_...` | Não | 365 / 1.100,00 / 1.082,50 (caso 1.3). Depende do seed de feriados no banco de teste (achado M9) |
| `PortfolioSummaryFlowTests.Resumo_da_carteira_fecha_...` | Não | Não verifica valores. Para referência: CDB 1.082,50 + LCI 8% 540,00 = 1.622,50 |
| `PortfolioSummaryTests` (todos) | Não | Usam fixtures; só ganham `TotalIofAmount`/`IofAmount` |

---

### 2. Contagem de dias úteis (RN-08)

| # | Início | Fim | DU | Dias úteis contados | Fronteira |
|---|---|---|---|---|---|
| 2.1 | 2026-09-11 (sex) | 2026-09-14 (seg) | **1** | 11/09 | sexta → segunda |
| 2.2 | 2026-04-20 (seg) | 2026-04-24 (sex) | **3** | 20, 22, 23/04 | Tiradentes 21/04 (ter) |
| 2.3 | 2026-09-04 (sex) | 2026-09-08 (ter) | **1** | 04/09 | feriado na segunda (07/09) |
| 2.4 | 2025-12-31 (qua) | 2026-01-05 (seg) | **2** | 31/12, 02/01 | virada de ano; 31/12 é dia útil ANBIMA e tem CDI no SGS |
| 2.5 | 2020-12-31 (qui) | 2021-01-04 (seg) | **1** | 31/12 | virada com 01/01 na sexta |
| 2.6 | 2026-02-13 (sex) | 2026-02-18 (qua) | **1** | 13/02 | Carnaval 16 e 17/02 |
| 2.7 | 2026-02-13 (sex) | 2026-02-19 (qui) | **2** | 13/02, 18/02 | Quarta de Cinzas é dia útil (tem CDI no SGS) |
| 2.8 | 2020-02-21 (sex) | 2020-02-26 (qua) | **1** | 21/02 | Carnaval 2020 (24 e 25/02) |
| 2.9 | 2026-11-19 (qui) | 2026-11-23 (seg) | **1** | 19/11 | Consciência Negra 20/11 (sex) |
| 2.10 | 2026-09-07 (seg, feriado) | 2026-09-08 (ter) | **0** | — | início em feriado |
| 2.11 | 2026-09-12 (sáb) | 2026-09-14 (seg) | **0** | — | início no sábado |
| 2.12 | 2026-09-14 | 2026-09-14 | **0** | — | `start == end` |
| 2.13 | 2019-12-31 | 2020-01-02 | erro | — | `start` fora da cobertura |
| 2.14 | 2026-09-14 | 2026-09-11 | erro | — | `end < start` |

Fontes: RN-08; ANBIMA, feriados 2020, 2025 e 2026; SGS série 12 (há cotação em 31/12/2025 e 18/02/2026; não há em 20/11/2025).

---

### 3. CDI com série real curta (R$ 1.000)

**Entrada:** aplicação 2026-09-14 (seg), referência 2026-09-23 (qua). 9 dias corridos, então IOF de 70% (dia 9) e IR de 22,5%. DU = 7. Há troca de patamar do CDI no meio do período.

**Cotações (SGS série 12, % a.d.)** e conferência com a série 4389 (% a.a.):

| Data | SGS 12 | SGS 4389 | TDI = (1+DI)^(1/252)−1, 8 casas |
|---|---|---|---|
| 14/09/2026 | 0,051660 | 13,90 | 0,00051660 |
| 15/09/2026 | 0,051660 | 13,90 | 0,00051660 |
| 16/09/2026 | 0,051660 | 13,90 | 0,00051660 |
| 17/09/2026 | 0,050788 | 13,65 | 0,00050788 |
| 18/09/2026 | 0,050788 | 13,65 | 0,00050788 |
| 21/09/2026 | 0,050788 | 13,65 | 0,00050788 |
| 22/09/2026 | 0,050788 | 13,65 | 0,00050788 |

A cotação de 23/09 fica fora, porque a data final é excluída (RN-08 e caderno B3).

**Produtório (C3), acumulado truncado em 16 casas:**

| Data | Fator diário 100% | Acumulado 100% | Fator diário 110% | Acumulado 110% |
|---|---|---|---|---|
| 14/09 | 1,0005166 | 1,0005166000000000 | 1,00056826 | 1,0005682600000000 |
| 15/09 | 1,0005166 | 1,0010334668755600 | 1,00056826 | 1,0011368429194276 |
| 16/09 | 1,0005166 | 1,0015506007645479 | 1,00056826 | 1,0017057489417849 |
| 17/09 | 1,00050788 | 1,0020592682836641 | 1,000558668 | 1,0022653698891347 |
| 18/09 | 1,00050788 | 1,0025681941448400 | 1,000558668 | 1,0028253034787999 |
| 21/09 | 1,00050788 | 1,0030773784792822 | 1,000558668 | 1,0033855498854437 |
| 22/09 | 1,00050788 | 1,0035868214182642 | 1,000558668 | 1,0039461092838271 |
| **Fator 8 casas** | | **1,00358682** | | **1,00394611** |

| # | Título | Bruto (sem arred.) | Rendimento | IOF 70% | IR 22,5% | Líquido (sem arred.) |
|---|---|---|---|---|---|---|
| 3.1 | CDB 100% | 1.003,58682 | 3,58682 | 2,510774 | 0,24211035 | 1.000,83393565 |
| 3.2 | CDB 110% | 1.003,94611 | 3,94611 | 2,762277 | 0,26636243 | 1.000,91747058 |
| 3.3 | LCI 100% | 1.003,58682 | 3,58682 | 2,510774 | 0 | 1.001,076046 |
| 3.4 | LCI 110% | 1.003,94611 | 3,94611 | 2,762277 | 0 | 1.001,183833 |

| # | Campo | C6a | C6b |
|---|---|---|---|
| 3.1 | Gross / Yield / Iof / Tax / Net | 1.003,59 / 3,59 / 2,51 / 0,24 / **1.000,83 ≠** | 1.003,59 / 3,59 / 2,51 / 0,24 / 1.000,84 |
| 3.2 | Gross / Yield / Iof / Tax / Net | 1.003,95 / 3,95 / 2,76 / 0,27 / 1.000,92 | 1.003,95 / 3,95 / **2,77** / 0,27 / 1.000,91 |
| 3.3 | Gross / Yield / Iof / Tax / Net | 1.003,59 / 3,59 / 2,51 / 0,00 / 1.001,08 | igual |
| 3.4 | Gross / Yield / Iof / Tax / Net | 1.003,95 / 3,95 / 2,76 / 0,00 / **1.001,18 ≠** | 1.003,95 / 3,95 / **2,77** / 0,00 / 1.001,18 |

Comum a todos: `ElapsedDays` 9, `BusinessDays` 7, `IofRate` 70, `TaxRate` 22,5 no CDB e 0 na LCI.

Em C6b, 3,95 × 70% = 2,765 é ponto médio exato em `decimal` e vai para 2,77 com `AwayFromZero`. É um bom teste de arredondamento.

**CDI faltante:** a mesma posição, com a cotação de 17/09/2026 removida do `MarketData`, lança `DomainException` citando **2026-09-17**.

Fontes: SGS 12 (https://api.bcb.gov.br/dados/serie/bcdata.sgs.12/dados?formato=json&dataInicial=11/09/2026&dataFinal=20/09/2026 e `21/09/2026`–`30/09/2026`); SGS 4389 (mesma rota, série 4389); B3, Caderno Debêntures Cetip 21, item 2.1.1; Decreto 6.306/2007, anexo; Lei 11.033/2004, art. 1º, I.

---

### 4. CDI de um mês inteiro, para conferir na Calculadora do Cidadão

**Entrada:** R$ 1.000, aplicação 2026-09-01 (ter), referência 2026-10-01 (qui). 30 dias corridos (IOF 0, IR 22,5%). DU = 21 (o feriado de 07/09 fica de fora).

**Cotações (SGS 12):**
- 0,051660% a.d. em 11 dias: 01, 02, 03, 04, 08, 09, 10, 11, 14, 15 e 16/09;
- 0,050788% a.d. em 10 dias: 17, 18, 21, 22, 23, 24, 25, 28, 29 e 30/09.

**Conferência à mão (100%):** 1,0005166^11 = 1,00569730093; 1,00050788^10 = 1,00509042313; o produto dá 1,01081672573.

| | 100% do CDI | 110% do CDI |
|---|---|---|
| Acumulado, truncado em 16 casas | 1,0108167257286023 | 1,0119045058928937 |
| Fator, 8 casas | **1,01081673** | **1,01190451** |
| Bruto (sem arred.) | 1.010,81673 | 1.011,90451 |
| IR 22,5% (sem arred.) | 2,43376425 | 2,67851475 |
| Gross / Yield / Iof / Tax / Net — C6a | 1.010,82 / 10,82 / 0,00 / 2,43 / **1.008,38 ≠** | 1.011,90 / 11,90 / 0,00 / 2,68 / **1.009,23 ≠** |
| Gross / Yield / Iof / Tax / Net — C6b | 1.010,82 / 10,82 / 0,00 / 2,43 / 1.008,39 | 1.011,90 / 11,90 / 0,00 / 2,68 / 1.009,22 |

**Para comparar com a Calculadora do Cidadão** (aba CDI, valor R$ 1.000, datas 01/09/2026 a 01/10/2026, percentual 100):
- esperado: **valor corrigido R$ 1.010,82, fator 1,01081673**;
- se a calculadora mostrar **R$ 1.011,33 (fator 1,01133010)**, ela incluiu a taxa da data final (01/10/2026 = 0,050788, 22 taxas). A diferença, então, é de convenção, não de cálculo;
- com 110%: R$ 1.011,90 esperado; R$ 1.012,47 se a data final entrar.

**A CONFIRMAR:**
- se a calculadora inclui ou exclui a data final;
- se ela arredonda ou trunca o valor;
- se aplica o percentual como `1 + CDI × p`.

As páginas de metodologia do BCB que consegui abrir não tratam do CDI. O critério de aceite tolera R$ 0,01.

Fonte: SGS 12 (janelas de 01/09 a 10/09, 11/09 a 20/09 e 21/09 a 30/09/2026); B3, caderno citado.

---

### 5. IOF (RN-11) e a ordem IOF → IR

#### 5.A Aritmética pura: rendimento bruto de R$ 10,00, CDB (IR 22,5%)

Para testar `IofTable` e a ordem dos impostos sem depender de fator.

| Dia | Alíquota IOF | IOF | Base do IR (10,00 − IOF) | IR 22,5% | Rendimento líquido |
|---|---|---|---|---|---|
| 0 | 0% | 0,00 | 10,00 | 2,25 | 7,75 |
| 1 | 96% | 9,60 | 0,40 | 0,09 | 0,31 |
| 15 | 50% | 5,00 | 5,00 | 1,13 (1,125) | 3,87 |
| 29 | 3% | 0,30 | 9,70 | 2,18 (2,1825) | 7,52 |
| 30 | 0% | 0,00 | 10,00 | 2,25 | 7,75 |
| 31 | 0% | 0,00 | 10,00 | 2,25 | 7,75 |

Se o IR incidisse sobre o rendimento bruto, ignorando o IOF, o dia 15 daria IR 2,25 e líquido 2,75, e não 3,87. A diferença mostra a ordem certa. Na LCI, o IR é sempre 0, e o líquido é 10,00 − IOF.

`IofTable.RateFor(-1)` lança `DomainException`.

#### 5.B Ponta a ponta: R$ 1.000 PreFixed 12% a.a., aplicação 2026-03-02 (seg)

`fator = 1,12^(DU/252)`

| Dia | Referência | DU | Fator | Bruto (sem arred.) | IOF % | IOF (sem arred.) | IR CDB (sem arred.) |
|---|---|---|---|---|---|---|---|
| 0 | 2026-03-02 | 0 | 1 | 1.000,000000 | 0 | 0 | 0 |
| 1 | 2026-03-03 | 1 | 1,0004498181430395 | 1.000,449818 | 96 | 0,431825 | 0,004048 |
| 15 | 2026-03-17 | 11 | 1,0049591431042557 | 1.004,959143 | 50 | 2,479572 | 0,557904 |
| 29 | 2026-03-31 | 21 | 1,0094887929345830 | 1.009,488793 | 3 | 0,284664 | 2,070929 |
| 30 | 2026-04-01 | 22 | 1,0099428793088400 | 1.009,942879 | 0 | 0 | 2,237148 |
| 31 | 2026-04-02 | 23 | 1,0103971699393866 | 1.010,397170 | 0 | 0 | 2,339363 |

| # | Dia | Título | C6a: Gross / Yield / Iof / Tax / Net | C6b: Gross / Yield / Iof / Tax / Net |
|---|---|---|---|---|
| 5.1 | 0 | CDB | 1.000,00 / 0,00 / 0,00 / 0,00 / 1.000,00 (IofRate 0, TaxRate 22,5) | igual |
| 5.2 | 1 | CDB | 1.000,45 / 0,45 / 0,43 / 0,00 / **1.000,01 ≠** | 1.000,45 / 0,45 / 0,43 / 0,00 / 1.000,02 |
| 5.3 | 1 | LCI | 1.000,45 / 0,45 / 0,43 / 0,00 / 1.000,02 | igual |
| 5.4 | 15 | CDB | 1.004,96 / 4,96 / 2,48 / 0,56 / 1.001,92 | igual |
| 5.5 | 15 | LCI | 1.004,96 / 4,96 / 2,48 / 0,00 / 1.002,48 | igual |
| 5.6 | 29 | CDB | 1.009,49 / 9,49 / 0,28 / 2,07 / **1.007,13 ≠** | 1.009,49 / 9,49 / 0,28 / 2,07 / 1.007,14 |
| 5.7 | 29 | LCI | 1.009,49 / 9,49 / 0,28 / 0,00 / **1.009,20 ≠** | 1.009,49 / 9,49 / 0,28 / 0,00 / 1.009,21 |
| 5.8 | 30 | CDB | 1.009,94 / 9,94 / 0,00 / 2,24 / **1.007,71 ≠** | 1.009,94 / 9,94 / 0,00 / 2,24 / 1.007,70 |
| 5.9 | 30 | LCI | 1.009,94 / 9,94 / 0,00 / 0,00 / 1.009,94 | igual |
| 5.10 | 31 | CDB | 1.010,40 / 10,40 / 0,00 / 2,34 / 1.008,06 | igual |
| 5.11 | 31 | LCI | 1.010,40 / 10,40 / 0,00 / 0,00 / 1.010,40 | igual |

A fronteira 29/30 aparece em 5.6 → 5.8 (IOF de 3% para 0%). A LCI paga IOF e tem IR zero em 5.3, 5.5 e 5.7.

Pela RN-11 atual, a LCA teria os mesmos números da LCI. Pelo Decreto, a LCA tem IOF zero (achado I1): em 5.5, LCA daria 1.004,96 / 4,96 / 0,00 / 0,00 / 1.004,96. **Esse valor depende da decisão sobre o achado I1.**

Fontes: Decreto 6.306/2007, art. 32 e anexo; Lei 11.033/2004, art. 1º, I, e art. 3º, II; RN-01, RN-03, RN-04, RN-11.

---

### 6. IPCA+ (RN-10, calculado pela regra como está hoje, com o pro rata do achado B3)

**IPCA usado (SGS 433, % no mês), última divulgação: 09/2026:**

| Mês | 01/2026 | 02/2026 | 03/2026 | 04/2026 | 05/2026 | 06/2026 | 07/2026 | 08/2026 | 09/2026 | 10/2026 |
|---|---|---|---|---|---|---|---|---|---|---|
| IPCA | 0,33 | 0,70 | 0,88 | 0,67 | 0,58 | 0,16 | 0,07 | −0,32 | 0,82 | não divulgado |

**DU por mês civil (C1):** abril 20 (feriados 03 e 21/04), maio 20 (01/05), junho 21 (04/06), agosto 21, setembro 21 (07/09), outubro 21 (12/10).

Todos os casos usam CDB de R$ 1.000.

#### 6.1 Meses cheios — IPCA + 6% a.a., 2026-04-01 (qua) → 2026-07-01 (qua)

91 dias corridos (IR 22,5%, IOF 0); DU 61.

| Mês | DU no intervalo / DU do mês | IPCA | Fator do mês |
|---|---|---|---|
| 04/2026 | 20/20 | 0,67 | 1,0067 |
| 05/2026 | 20/20 | 0,58 | 1,0058 |
| 06/2026 | 21/21 | 0,16 | 1,0016 |

| Passo | Valor |
|---|---|
| Fator IPCA | 1,014158922176 |
| Fator real = 1,06^(61/252) | 1,014204717056 |
| Fator total | 1,028564762715 |
| Bruto | 1.028,564763 |
| IR 22,5% | 6,427072 |
| Líquido | 1.022,137691 |
| IsEstimated | false |

| Campo | C6a | C6b |
|---|---|---|
| Gross / Yield / Iof / Tax / Net | 1.028,56 / 28,56 / 0,00 / 6,43 / **1.022,14 ≠** | 1.028,56 / 28,56 / 0,00 / 6,43 / 1.022,13 |

#### 6.2 Mês parcial — IPCA + 6% a.a., 2026-04-15 (qua) → 2026-06-15 (seg)

61 dias corridos (IR 22,5%, IOF 0); DU 40.

| Mês | DU no intervalo / DU do mês | IPCA | Fator do mês |
|---|---|---|---|
| 04/2026 | 11/20 (15 a 30/04, sem o 21) | 0,67 | 1,0067^(11/20) = 1,003679462778 |
| 05/2026 | 20/20 | 0,58 | 1,0058 |
| 06/2026 | 9/21 (01 a 12/06, sem o 04) | 0,16 | 1,0016^(9/21) = 1,000685401079 |

| Passo | Valor |
|---|---|
| Fator IPCA | 1,010192716602 |
| Fator real = 1,06^(40/252) | 1,009291937515 |
| Fator total | 1,019579364203 |
| Bruto | 1.019,579364 |
| IR | 4,405357 |
| Líquido | 1.015,174007 |
| IsEstimated | false |

| Campo | C6a | C6b |
|---|---|---|
| Gross / Yield / Iof / Tax / Net | 1.019,58 / 19,58 / 0,00 / 4,41 / 1.015,17 | igual |

#### 6.3 Mês estimado — IPCA + 6% a.a., 2026-09-01 (ter) → 2026-10-15 (qui)

44 dias corridos (IR 22,5%, IOF 0); DU 30. `MarketData` com IPCA até 09/2026.

| Mês | DU no intervalo / DU do mês | IPCA | Fator do mês |
|---|---|---|---|
| 09/2026 | 21/21 | 0,82 (SGS) | 1,0082 |
| 10/2026 | 9/21 (01 a 14/10, sem o 12) | **0,82, estimado = último publicado (09/2026)** | 1,0082^(9/21) = 1,003506087424 |

| Passo | Valor |
|---|---|
| Fator IPCA | 1,011734837341 |
| Fator real = 1,06^(30/252) | 1,006960889927 |
| Fator total | 1,018777412179 |
| Bruto | 1.018,777412 |
| IR | 4,224918 |
| Líquido | 1.014,552494 |
| IsEstimated | **true** |

| Campo | C6a | C6b |
|---|---|---|
| Gross / Yield / Iof / Tax / Net | 1.018,78 / 18,78 / 0,00 / 4,22 / **1.014,55 ≠** | 1.018,78 / 18,78 / 0,00 / **4,23** / 1.014,55 |

Na C6a, 1.018,78 − 4,22 = 1.014,56, que não fecha com o líquido de 1.014,55. Na C6b, o IR é round2(18,78 × 22,5% = 4,2255) = 4,23.

#### 6.4 Deflação — IPCA + 1% a.a., 2026-08-03 (seg) → 2026-09-01 (ter)

29 dias corridos (IOF da tabela: 3%; IR: 22,5%); DU 21.

| Mês | DU no intervalo / DU do mês | IPCA | Fator do mês |
|---|---|---|---|
| 08/2026 | 21/21 (01 e 02/08 são fim de semana, então o mês fica cheio em DU) | −0,32 | 0,9968 |

| Passo | Valor |
|---|---|
| Fator real = 1,01^(21/252) | 1,000829538114 |
| Fator total | 0,997626883592 |
| Bruto | 997,626884 |
| Rendimento | **−2,373116** |
| IOF e IR | 0, porque o rendimento é ≤ 0 (decisão do rascunho e achado I6) |

| Campo | C6a = C6b |
|---|---|
| GrossAmount | 997,63 |
| GrossYield | −2,37 |
| IofRate / IofAmount | 3 / 0,00 |
| TaxRate / TaxAmount | 22,5 / 0,00 |
| NetAmount | 997,63 |
| IsEstimated | false |

Se a decisão sobre o achado I6 for zerar também as alíquotas, `IofRate` e `TaxRate` passam a 0; os valores não mudam.

**Dependência da recomendação (b):** os quatro casos seguem a RN-10 como está, com o IPCA do próprio mês civil, sem defasagem e sem aniversário. Se a RN mudar para aniversário com defasagem, todos precisam ser recalculados.

Fontes: SGS 433 (https://api.bcb.gov.br/dados/serie/bcdata.sgs.433/dados?formato=json&dataInicial=01/01/2026&dataFinal=30/04/2026 e `01/05/2026`–`30/10/2026`); RN-10; achado B3.

---

### 7. RN-07 estendida (`TotalNetAmount = TotalGrossAmount − TotalTaxAmount − TotalIofAmount`)

#### 7.1 Fecha nas duas convenções: casos 5.4 + 5.5 (CDB e LCI PreFixed 12%, R$ 1.000 cada, dia 15)

| Posição | Invested | Gross | Yield | Iof | Tax | Net |
|---|---|---|---|---|---|---|
| CDB (5.4) | 1.000,00 | 1.004,96 | 4,96 | 2,48 | 0,56 | 1.001,92 |
| LCI (5.5) | 1.000,00 | 1.004,96 | 4,96 | 2,48 | 0,00 | 1.002,48 |
| **Total** | **2.000,00** | **2.009,92** | **9,92** | **4,96** | **0,56** | **2.004,40** |

Conferência: 2.009,92 − 0,56 − 4,96 = 2.004,40 = 1.001,92 + 1.002,48.

Para `PortfolioSummary.Create`, use esses valores como fixture (`AssetType` Cdb e Lci). A participação fica em CDB 49,99 e LCI 50,01. Isso **A CONFIRMAR** pela regra de percentual do `PortfolioSummary`, que não foi enviada (`1.001,92/2.004,40 = 49,9860%` e `1.002,48/2.004,40 = 50,0140%`).

#### 7.2 Mostra o achado B1: casos 5.6 + 5.7 (CDB e LCI, dia 29)

| Posição | Gross | Iof | Tax | Net C6a | Net C6b |
|---|---|---|---|---|---|
| CDB (5.6) | 1.009,49 | 0,28 | 2,07 | 1.007,13 | 1.007,14 |
| LCI (5.7) | 1.009,49 | 0,28 | 0,00 | 1.009,20 | 1.009,21 |
| **Total** | **2.018,98** | **0,56** | **2,07** | **2.016,33** | **2.016,35** |

Gross − Tax − Iof = 2.018,98 − 2,07 − 0,56 = **2.016,35**.

- Em C6a, a soma dos `NetAmount` dá 2.016,33. Diferença de R$ 0,02: a RN-07 não pode ser atendida.
- Em C6b, fecha.

Use esse caso como teste de regressão depois da decisão do achado B1.

Fonte: RN-07 e RN-11 da especificação.

---

### Feriados ANBIMA por ano

Feriados nacionais de 2020 a 2030, conferidos em https://www.anbima.com.br/feriados/fer_nacionais/AAAA.asp (um link por ano). As datas móveis (Carnaval = Páscoa − 48 e − 47; Paixão = Páscoa − 2; Corpus Christi = Páscoa + 60) batem com a Páscoa calculada. Total: **139 feriados**, sendo 12 por ano de 2020 a 2023 e 13 por ano de 2024 a 2030, quando entra o Dia Nacional de Zumbi e da Consciência Negra (Lei 14.759/2023, conforme o calendário ANBIMA).

Nomes como na ANBIMA:
- Confraternização Universal
- Carnaval
- Paixão de Cristo
- Tiradentes
- Dia do Trabalho
- Corpus Christi
- Independência do Brasil
- Nossa Sr.a Aparecida - Padroeira do Brasil
- Finados
- Proclamação da República
- Dia Nacional de Zumbi e da Consciência Negra
- Natal

| Ano | Confraternização Universal | Carnaval (seg) | Carnaval (ter) | Paixão de Cristo | Tiradentes | Dia do Trabalho | Corpus Christi | Independência do Brasil | Nossa Sr.a Aparecida | Finados | Proclamação da República | Consciência Negra | Natal |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 2020 | 01/01 | 24/02 | 25/02 | 10/04 | 21/04 | 01/05 | 11/06 | 07/09 | 12/10 | 02/11 | 15/11 | — | 25/12 |
| 2021 | 01/01 | 15/02 | 16/02 | 02/04 | 21/04 | 01/05 | 03/06 | 07/09 | 12/10 | 02/11 | 15/11 | — | 25/12 |
| 2022 | 01/01 | 28/02 | 01/03 | 15/04 | 21/04 | 01/05 | 16/06 | 07/09 | 12/10 | 02/11 | 15/11 | — | 25/12 |
| 2023 | 01/01 | 20/02 | 21/02 | 07/04 | 21/04 | 01/05 | 08/06 | 07/09 | 12/10 | 02/11 | 15/11 | — | 25/12 |
| 2024 | 01/01 | 12/02 | 13/02 | 29/03 | 21/04 | 01/05 | 30/05 | 07/09 | 12/10 | 02/11 | 15/11 | 20/11 | 25/12 |
| 2025 | 01/01 | 03/03 | 04/03 | 18/04 | 21/04 | 01/05 | 19/06 | 07/09 | 12/10 | 02/11 | 15/11 | 20/11 | 25/12 |
| 2026 | 01/01 | 16/02 | 17/02 | 03/04 | 21/04 | 01/05 | 04/06 | 07/09 | 12/10 | 02/11 | 15/11 | 20/11 | 25/12 |
| 2027 | 01/01 | 08/02 | 09/02 | 26/03 | 21/04 | 01/05 | 27/05 | 07/09 | 12/10 | 02/11 | 15/11 | 20/11 | 25/12 |
| 2028 | 01/01 | 28/02 | 29/02 | 14/04 | 21/04 | 01/05 | 15/06 | 07/09 | 12/10 | 02/11 | 15/11 | 20/11 | 25/12 |
| 2029 | 01/01 | 12/02 | 13/02 | 30/03 | 21/04 | 01/05 | 31/05 | 07/09 | 12/10 | 02/11 | 15/11 | 20/11 | 25/12 |
| 2030 | 01/01 | 04/03 | 05/03 | 19/04 | 21/04 | 01/05 | 20/06 | 07/09 | 12/10 | 02/11 | 15/11 | 20/11 | 25/12 |

Feriados que caem em sábado ou domingo entram no seed mesmo assim. Não alteram a contagem, mas mantêm a tabela igual à da ANBIMA.

Notas da própria ANBIMA:
- a quinta-feira santa é dia útil desde 2000;
- o último dia útil do ano não tem expediente público, mas **não** consta como feriado nacional. Por isso o 31/12 conta como dia útil, e o SGS publica CDI nessa data.

Dias úteis por ano civil, para conferência: 2020: 251; 2021: 251; 2022: 251; 2023: 249; 2024: 253; 2025: 252; 2026: 249; 2027: 251; 2028: 248; 2029: 249; 2030: 252.

A citação da Lei 14.759/2023 vem do meu conhecimento, não de leitura nesta sessão: **A CONFIRMAR**. O calendário ANBIMA em si foi conferido.

Conferência independente feita pelo orquestrador com script próprio: contagens de dias úteis
2.x e intervalos de 2020, 2025 e 2020–2030, casos 1.1 e 1.2, casos 3.1 e 3.2 e o fator do
caso 4 batem com os valores acima.

## Feriados ANBIMA

Fonte: ANBIMA, páginas anuais de feriados nacionais https://www.anbima.com.br/feriados/fer_nacionais/AAAA.asp (AAAA de 2020 a 2030), a partir de https://www.anbima.com.br/feriados/. A planilha https://www.anbima.com.br/feriados/arqs/feriados_nacionais.xls não pôde ser lida (retornou dados binários), por isso foram usadas as páginas HTML.

A ANBIMA lista também os feriados que caem em sábado ou domingo (25 de 139 no período). Eles estão incluídos abaixo. A ANBIMA inclui o Dia Nacional de Zumbi e da Consciência Negra (20/11) a partir de 2024.

Datas móveis conferidas com a Páscoa (Meeus/Butcher): Carnaval = Páscoa-48 e -47, Paixão de Cristo = Páscoa-2, Corpus Christi = Páscoa+60. Nenhuma divergência.

| Data | Nome |
|---|---|
| 2020-01-01 | Confraternização Universal |
| 2020-02-24 | Carnaval |
| 2020-02-25 | Carnaval |
| 2020-04-10 | Paixão de Cristo |
| 2020-04-21 | Tiradentes |
| 2020-05-01 | Dia do Trabalho |
| 2020-06-11 | Corpus Christi |
| 2020-09-07 | Independência do Brasil |
| 2020-10-12 | Nossa Sr.a Aparecida - Padroeira do Brasil |
| 2020-11-02 | Finados |
| 2020-11-15 | Proclamação da República |
| 2020-12-25 | Natal |
| 2021-01-01 | Confraternização Universal |
| 2021-02-15 | Carnaval |
| 2021-02-16 | Carnaval |
| 2021-04-02 | Paixão de Cristo |
| 2021-04-21 | Tiradentes |
| 2021-05-01 | Dia do Trabalho |
| 2021-06-03 | Corpus Christi |
| 2021-09-07 | Independência do Brasil |
| 2021-10-12 | Nossa Sr.a Aparecida - Padroeira do Brasil |
| 2021-11-02 | Finados |
| 2021-11-15 | Proclamação da República |
| 2021-12-25 | Natal |
| 2022-01-01 | Confraternização Universal |
| 2022-02-28 | Carnaval |
| 2022-03-01 | Carnaval |
| 2022-04-15 | Paixão de Cristo |
| 2022-04-21 | Tiradentes |
| 2022-05-01 | Dia do Trabalho |
| 2022-06-16 | Corpus Christi |
| 2022-09-07 | Independência do Brasil |
| 2022-10-12 | Nossa Sr.a Aparecida - Padroeira do Brasil |
| 2022-11-02 | Finados |
| 2022-11-15 | Proclamação da República |
| 2022-12-25 | Natal |
| 2023-01-01 | Confraternização Universal |
| 2023-02-20 | Carnaval |
| 2023-02-21 | Carnaval |
| 2023-04-07 | Paixão de Cristo |
| 2023-04-21 | Tiradentes |
| 2023-05-01 | Dia do Trabalho |
| 2023-06-08 | Corpus Christi |
| 2023-09-07 | Independência do Brasil |
| 2023-10-12 | Nossa Sr.a Aparecida - Padroeira do Brasil |
| 2023-11-02 | Finados |
| 2023-11-15 | Proclamação da República |
| 2023-12-25 | Natal |
| 2024-01-01 | Confraternização Universal |
| 2024-02-12 | Carnaval |
| 2024-02-13 | Carnaval |
| 2024-03-29 | Paixão de Cristo |
| 2024-04-21 | Tiradentes |
| 2024-05-01 | Dia do Trabalho |
| 2024-05-30 | Corpus Christi |
| 2024-09-07 | Independência do Brasil |
| 2024-10-12 | Nossa Sr.a Aparecida - Padroeira do Brasil |
| 2024-11-02 | Finados |
| 2024-11-15 | Proclamação da República |
| 2024-11-20 | Dia Nacional de Zumbi e da Consciência Negra |
| 2024-12-25 | Natal |
| 2025-01-01 | Confraternização Universal |
| 2025-03-03 | Carnaval |
| 2025-03-04 | Carnaval |
| 2025-04-18 | Paixão de Cristo |
| 2025-04-21 | Tiradentes |
| 2025-05-01 | Dia do Trabalho |
| 2025-06-19 | Corpus Christi |
| 2025-09-07 | Independência do Brasil |
| 2025-10-12 | Nossa Sr.a Aparecida - Padroeira do Brasil |
| 2025-11-02 | Finados |
| 2025-11-15 | Proclamação da República |
| 2025-11-20 | Dia Nacional de Zumbi e da Consciência Negra |
| 2025-12-25 | Natal |
| 2026-01-01 | Confraternização Universal |
| 2026-02-16 | Carnaval |
| 2026-02-17 | Carnaval |
| 2026-04-03 | Paixão de Cristo |
| 2026-04-21 | Tiradentes |
| 2026-05-01 | Dia do Trabalho |
| 2026-06-04 | Corpus Christi |
| 2026-09-07 | Independência do Brasil |
| 2026-10-12 | Nossa Sr.a Aparecida - Padroeira do Brasil |
| 2026-11-02 | Finados |
| 2026-11-15 | Proclamação da República |
| 2026-11-20 | Dia Nacional de Zumbi e da Consciência Negra |
| 2026-12-25 | Natal |
| 2027-01-01 | Confraternização Universal |
| 2027-02-08 | Carnaval |
| 2027-02-09 | Carnaval |
| 2027-03-26 | Paixão de Cristo |
| 2027-04-21 | Tiradentes |
| 2027-05-01 | Dia do Trabalho |
| 2027-05-27 | Corpus Christi |
| 2027-09-07 | Independência do Brasil |
| 2027-10-12 | Nossa Sr.a Aparecida - Padroeira do Brasil |
| 2027-11-02 | Finados |
| 2027-11-15 | Proclamação da República |
| 2027-11-20 | Dia Nacional de Zumbi e da Consciência Negra |
| 2027-12-25 | Natal |
| 2028-01-01 | Confraternização Universal |
| 2028-02-28 | Carnaval |
| 2028-02-29 | Carnaval |
| 2028-04-14 | Paixão de Cristo |
| 2028-04-21 | Tiradentes |
| 2028-05-01 | Dia do Trabalho |
| 2028-06-15 | Corpus Christi |
| 2028-09-07 | Independência do Brasil |
| 2028-10-12 | Nossa Sr.a Aparecida - Padroeira do Brasil |
| 2028-11-02 | Finados |
| 2028-11-15 | Proclamação da República |
| 2028-11-20 | Dia Nacional de Zumbi e da Consciência Negra |
| 2028-12-25 | Natal |
| 2029-01-01 | Confraternização Universal |
| 2029-02-12 | Carnaval |
| 2029-02-13 | Carnaval |
| 2029-03-30 | Paixão de Cristo |
| 2029-04-21 | Tiradentes |
| 2029-05-01 | Dia do Trabalho |
| 2029-05-31 | Corpus Christi |
| 2029-09-07 | Independência do Brasil |
| 2029-10-12 | Nossa Sr.a Aparecida - Padroeira do Brasil |
| 2029-11-02 | Finados |
| 2029-11-15 | Proclamação da República |
| 2029-11-20 | Dia Nacional de Zumbi e da Consciência Negra |
| 2029-12-25 | Natal |
| 2030-01-01 | Confraternização Universal |
| 2030-03-04 | Carnaval |
| 2030-03-05 | Carnaval |
| 2030-04-19 | Paixão de Cristo |
| 2030-04-21 | Tiradentes |
| 2030-05-01 | Dia do Trabalho |
| 2030-06-20 | Corpus Christi |
| 2030-09-07 | Independência do Brasil |
| 2030-10-12 | Nossa Sr.a Aparecida - Padroeira do Brasil |
| 2030-11-02 | Finados |
| 2030-11-15 | Proclamação da República |
| 2030-11-20 | Dia Nacional de Zumbi e da Consciência Negra |
| 2030-12-25 | Natal |

```csv
date,name
2020-01-01,Confraternização Universal
2020-02-24,Carnaval
2020-02-25,Carnaval
2020-04-10,Paixão de Cristo
2020-04-21,Tiradentes
2020-05-01,Dia do Trabalho
2020-06-11,Corpus Christi
2020-09-07,Independência do Brasil
2020-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
2020-11-02,Finados
2020-11-15,Proclamação da República
2020-12-25,Natal
2021-01-01,Confraternização Universal
2021-02-15,Carnaval
2021-02-16,Carnaval
2021-04-02,Paixão de Cristo
2021-04-21,Tiradentes
2021-05-01,Dia do Trabalho
2021-06-03,Corpus Christi
2021-09-07,Independência do Brasil
2021-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
2021-11-02,Finados
2021-11-15,Proclamação da República
2021-12-25,Natal
2022-01-01,Confraternização Universal
2022-02-28,Carnaval
2022-03-01,Carnaval
2022-04-15,Paixão de Cristo
2022-04-21,Tiradentes
2022-05-01,Dia do Trabalho
2022-06-16,Corpus Christi
2022-09-07,Independência do Brasil
2022-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
2022-11-02,Finados
2022-11-15,Proclamação da República
2022-12-25,Natal
2023-01-01,Confraternização Universal
2023-02-20,Carnaval
2023-02-21,Carnaval
2023-04-07,Paixão de Cristo
2023-04-21,Tiradentes
2023-05-01,Dia do Trabalho
2023-06-08,Corpus Christi
2023-09-07,Independência do Brasil
2023-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
2023-11-02,Finados
2023-11-15,Proclamação da República
2023-12-25,Natal
2024-01-01,Confraternização Universal
2024-02-12,Carnaval
2024-02-13,Carnaval
2024-03-29,Paixão de Cristo
2024-04-21,Tiradentes
2024-05-01,Dia do Trabalho
2024-05-30,Corpus Christi
2024-09-07,Independência do Brasil
2024-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
2024-11-02,Finados
2024-11-15,Proclamação da República
2024-11-20,Dia Nacional de Zumbi e da Consciência Negra
2024-12-25,Natal
2025-01-01,Confraternização Universal
2025-03-03,Carnaval
2025-03-04,Carnaval
2025-04-18,Paixão de Cristo
2025-04-21,Tiradentes
2025-05-01,Dia do Trabalho
2025-06-19,Corpus Christi
2025-09-07,Independência do Brasil
2025-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
2025-11-02,Finados
2025-11-15,Proclamação da República
2025-11-20,Dia Nacional de Zumbi e da Consciência Negra
2025-12-25,Natal
2026-01-01,Confraternização Universal
2026-02-16,Carnaval
2026-02-17,Carnaval
2026-04-03,Paixão de Cristo
2026-04-21,Tiradentes
2026-05-01,Dia do Trabalho
2026-06-04,Corpus Christi
2026-09-07,Independência do Brasil
2026-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
2026-11-02,Finados
2026-11-15,Proclamação da República
2026-11-20,Dia Nacional de Zumbi e da Consciência Negra
2026-12-25,Natal
2027-01-01,Confraternização Universal
2027-02-08,Carnaval
2027-02-09,Carnaval
2027-03-26,Paixão de Cristo
2027-04-21,Tiradentes
2027-05-01,Dia do Trabalho
2027-05-27,Corpus Christi
2027-09-07,Independência do Brasil
2027-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
2027-11-02,Finados
2027-11-15,Proclamação da República
2027-11-20,Dia Nacional de Zumbi e da Consciência Negra
2027-12-25,Natal
2028-01-01,Confraternização Universal
2028-02-28,Carnaval
2028-02-29,Carnaval
2028-04-14,Paixão de Cristo
2028-04-21,Tiradentes
2028-05-01,Dia do Trabalho
2028-06-15,Corpus Christi
2028-09-07,Independência do Brasil
2028-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
2028-11-02,Finados
2028-11-15,Proclamação da República
2028-11-20,Dia Nacional de Zumbi e da Consciência Negra
2028-12-25,Natal
2029-01-01,Confraternização Universal
2029-02-12,Carnaval
2029-02-13,Carnaval
2029-03-30,Paixão de Cristo
2029-04-21,Tiradentes
2029-05-01,Dia do Trabalho
2029-05-31,Corpus Christi
2029-09-07,Independência do Brasil
2029-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
2029-11-02,Finados
2029-11-15,Proclamação da República
2029-11-20,Dia Nacional de Zumbi e da Consciência Negra
2029-12-25,Natal
2030-01-01,Confraternização Universal
2030-03-04,Carnaval
2030-03-05,Carnaval
2030-04-19,Paixão de Cristo
2030-04-21,Tiradentes
2030-05-01,Dia do Trabalho
2030-06-20,Corpus Christi
2030-09-07,Independência do Brasil
2030-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
2030-11-02,Finados
2030-11-15,Proclamação da República
2030-11-20,Dia Nacional de Zumbi e da Consciência Negra
2030-12-25,Natal
```

## Verificação

```powershell
dotnet build
dotnet test
docker compose up -d --build
```

Com a API no ar: confira que o job importou CDI e IPCA (`GET /api/indexes/Cdi?from=&to=`),
cadastre um CDB 100% do CDI com um aporte no período do caso de referência real e compare a
projeção com a Calculadora do Cidadão do Banco Central. O valor da calculadora é registrado
no `VALIDACAO-F7.md`.

## Restrições

- Não altere `CLAUDE.md`, `docs/ESPECIFICACAO.md` nem specs de fases anteriores
- Não adicione pacotes além dos autorizados
- Não execute `git add` nem `git commit`

## Critérios de aceite

1. `dotnet build` sem erro nem warning
2. Todos os testes passam; a contagem não cai em relação à linha de base
3. `FixedIncome.Domain` continua sem referência de projeto e sem pacote
4. Cada regra (RN-01, RN-07 estendida, RN-08 a RN-11) tem teste identificado
5. Os critérios da F7 em `docs/ROADMAP.md` atendidos com evidência no `VALIDACAO-F7.md`
6. `IndexRates`, `IndexRatesOptions` e `ResolveAnnualRate` removidos, sem referência restante
7. Nenhum teste depende de acesso à internet
8. Nenhum commit foi criado

## Relatório final

- Resultado de `dotnet build` e `dotnet test`, com a contagem antes e depois
- Arquivos criados e removidos
- `Position.Project` na íntegra
- A projeção de um CDB 100% do CDI no período real, ao lado do valor da Calculadora do Cidadão
- Decisões tomadas que não estavam especificadas aqui
