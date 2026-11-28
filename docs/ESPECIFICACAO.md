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
- Entrega contínua da imagem Docker, testada no pipeline
- Publicação em nuvem sob demanda na AWS, com infraestrutura como código em Terraform e ambiente
  efêmero destruído automaticamente (F11)
- Testes de integração contra PostgreSQL real, regras de arquitetura e mutation testing
- Contrato v2 para o front (F12): projeção com data futura (RN-12), variação acumulada de um
  indexador (RN-13) e projeção consolidada por título (RN-14)
- Contrato v3 para o front (F13): ganho líquido (RN-15), próxima faixa de IR (RN-16), fim do IOF
  (RN-17) e totais por título no consolidado (RN-18)

### Fora do escopo

- Autenticação e multiusuário
- Resgates parciais
- Marcação a mercado do Tesouro Direto. O valor de um título do Tesouro antes do vencimento é
  sempre o da curva (taxa contratada), e quem exibe deve deixar isso claro (decisão de
  26/11/2026)

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
precisa. `TotalNetAmount` é sempre igual a `TotalGrossAmount` menos `TotalTaxAmount` menos
`TotalIofAmount` (v2), e igual à soma dos `NetAmount` individuais. Os totais são a soma dos
itens de `positions` do resumo, e a soma é exata porque totais e itens saem da mesma lista de
projeções, sem recálculo.

**RN-08 — Dias úteis** (v2). Dia útil é qualquer dia de segunda a sexta que não seja feriado
nacional cadastrado em `Holiday`. A contagem entre duas datas inclui a inicial e exclui a
final: aplicação numa sexta com referência na segunda seguinte conta 1 dia útil. Uma data
fora do período coberto pelo calendário cadastrado gera erro, nunca uma suposição.

**RN-09 — Rentabilidade pós-fixada em CDI** (v2). O fator acumulado é o produto, para cada
dia útil do intervalo da RN-08, de `1 + CDI do dia × Rate / 100`, com o CDI do dia em fração
(série 12 do SGS, publicada em % ao dia). O valor bruto é `InvestedAmount × fator`. As casas
decimais do fator diário e do acumulado seguem a metodologia de DI da B3 e são fixadas na
SPEC-F7. Se faltar cotação para algum dia útil do intervalo anterior à última cotação importada, a
projeção não é calculada e a resposta informa a primeira data sem cotação. Dia útil posterior à
última cotação segue a RN-12.

**RN-10 — Rentabilidade IPCA+** (v2). O valor bruto é
`InvestedAmount × fator IPCA × (1 + Rate / 100) ^ (DU / 252)`, com DU contado pela RN-08. O
fator IPCA é o produto de `1 + IPCA do mês / 100` dos meses cheios do intervalo, com pro rata
em dias úteis nos meses parciais. Substitui a soma simples de IPCA e taxa usada na v1. Para
um mês já encerrado cuja variação ainda não foi divulgada, usa-se a última variação mensal
publicada, e a projeção sai marcada como estimada. O mês de hoje e os seguintes seguem a RN-12. Quando o IPCA oficial do mês é importado, as projeções
seguintes passam a usá-lo. A estimativa vale só para meses posteriores ao último publicado:
mês anterior ao último publicado e sem cotação é falha de importação, e a projeção não é
calculada, com a resposta informando o mês sem cotação.

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

O IOF é apurado antes do IR, e a base do IR passa a ser o rendimento bruto menos o IOF. LCA
tem IOF zero (Decreto 6.306/2007, art. 32, § 2º, V). A isenção de IR da RN-04 não se estende
ao IOF da LCI, que paga IOF normalmente, assim como CDB e Tesouro Direto. Rendimento menor ou
igual a zero não gera IOF nem IR. A projeção passa a retornar
alíquota e valor de IOF, e a RN-07 se estende ao consolidado:
`TotalNetAmount = TotalGrossAmount − TotalTaxAmount − TotalIofAmount`. Para isso fechar, cada
projeção arredonda em ordem: valor bruto, depois IOF, depois IR, e o líquido é a subtração dos
valores já arredondados.

**RN-12 — Projeção com data futura** (v2, F12). Quando o intervalo da projeção, limitado pela
RN-02, inclui dia de CDI ou mês de IPCA ainda não publicados nos termos abaixo, a projeção é
calculada com taxas projetadas e sai com `isProjected` verdadeiro. Cada dia útil (RN-08) posterior à última cotação de CDI importada e a
partir do dia útil anterior a hoje (Brasília) usa a taxa diária dessa última cotação, com as casas
da RN-09. O dia útil anterior a hoje é o maior dia útil estritamente anterior a hoje, seja hoje dia
útil ou não (num sábado, é a sexta, ou o dia útil antes dela se for feriado). Ele entra porque o CDI de um dia só é publicado no SGS no dia útil
seguinte e a importação roda a cada 6 horas: sem ele, toda projeção futura falharia. Dia útil sem
cotação posterior à última cotação e anterior ao dia útil anterior a hoje é falha de importação
(400, com a mensagem que lembra que a importação pode estar desligada), como na RN-09 (decisão de
27/11/2026). "Última cotação" é a do banco inteiro, não a do período consultado. O mês de IPCA de hoje (Brasília) e os seguintes, sem variação
publicada, usam a média geométrica dos 12 meses de competência seguidos que terminam no último
publicado, em percentual com 2 casas: `round(((∏ (1 + IPCA do mês / 100)) ^ (1 / 12) − 1) × 100, 2)`,
com um único arredondamento, e o pro rata da RN-10. O mês de hoje conta como não encerrado até o
último dia dele, porque o IPCA mede o mês inteiro e só é divulgado no mês seguinte. Um mês já
encerrado e ainda não divulgado continua na RN-10 (última variação, `isEstimated`). As duas marcas
são independentes e podem vir verdadeiras juntas. Valor publicado sempre prevalece sobre
estimativa e projeção. Um PreFixed nunca é projetado. Se faltar algum dos 12 meses, a projeção
futura de um título Ipca não é calculada e a resposta diz quantos meses existem e qual falta. A
regra não muda o tratamento de lacunas: um dia útil sem cotação anterior à última cotação de
CDI, ou um mês sem variação anterior ao último publicado, continua sendo falha de importação
(RN-09 e RN-10). O limite do calendário (RN-08) também continua: data posterior à cobertura é
400. IR e IOF seguem a RN-03 e a RN-11, com os prazos contados até a data de referência. No
consolidado, `isProjected` na raiz é verdadeiro se algum item for projetado. Taxa projetada não é
previsão: é a hipótese de que as taxas recentes se mantêm (no CDI, a última taxa diária importada;
no IPCA, a média geométrica dos 12 últimos meses), sem decisão do Copom já anunciada
nem expectativas de mercado, e quem exibe deve dizer isso. Duas consequências: um IPCA+ com dia
útil no mês de hoje sai projetado também na consulta sem data (e, com menos de 12 meses de IPCA,
responde 400); e uma data informada depois da última cotação de CDI é projetada em vez de erro
quando a última cotação é de dois dias úteis antes de hoje ou mais recente. Com a importação parada
há mais tempo, continua 400.

**RN-13 — Variação acumulada de um indexador** (v2, F12). A variação acumulada usa só cotações
publicadas, sem a estimativa da RN-10 nem a projeção da RN-12. No CDI, é o produto de
`1 + CDI do dia / 100` para cada dia útil do intervalo da RN-08, com as casas da RN-09. No IPCA, é o
produto de `1 + IPCA do mês / 100` dos meses de competência entre o mês de `from` e o mês de
`to`, inclusive, sem pro rata, com o fator arredondado em 7 casas (a convenção que reproduz a
Calculadora do Cidadão). O resultado é devolvido em percentual, `(fator − 1) × 100`, com 6 casas.
Quando `to` passa da última cotação, o período termina na última cotação publicada e a resposta
devolve a data efetiva usada (`effectiveTo`), em vez de erro: no CDI, o dia útil seguinte à última
cotação (o fim é exclusivo); no IPCA, o último dia do último mês publicado. Sem corte,
`effectiveTo` é igual a `to`. No IPCA as comparações são por mês de competência e o calendário não
é consultado. Lacuna anterior à última cotação continua sendo falha de importação. `from`
posterior a `to`, ou um período sem nenhuma cotação publicada, é 400: no CDI, nenhum dia útil em
`[from, effectiveTo)`; no IPCA, o mês de `from` posterior ao último publicado. No CDI, `from`
igual a `to` é 400 com mensagem própria de período vazio (o fim exclusivo pede `to` posterior a
`from`), mesmo num dia com cotação; no IPCA, é a variação do mês. O acumulado do IPCA
usa meses cheios e por isso não fecha com o valor bruto de um título IPCA+, que usa pro rata; o do
CDI fecha com o de um título 100% do CDI.

**RN-14 — Projeção por título** (v2, F12). A projeção de um título reúne as projeções dos seus
aportes, com as mesmas regras de data do consolidado: sem `referenceDate`, vale a data padrão,
e o aporte posterior a ela é projetado na própria data do aporte; com data informada, os
aportes posteriores a ela ficam de fora. Os totais seguem a RN-07: são a soma dos itens já
arredondados, e o líquido é o bruto menos o IR menos o IOF. Um título sem aportes incluídos
responde 200, com totais zero, lista vazia e `referenceDate` presente. Título inexistente é
404. Os aportes saem na ordem do consolidado, o mais recente primeiro, sem garantia entre aportes do
mesmo dia.

**RN-15 — Ganho líquido** (v2, F13). A projeção devolve `netYield`, o valor líquido menos o valor
investido, com os dois já arredondados. O consolidado e a projeção por título devolvem
`totalNetYield`, a soma dos `netYield` dos itens (RN-07), que é sempre igual a `TotalNetAmount −
TotalInvestedAmount`. O campo existe para quem exibe não precisar subtrair valores.

**RN-16 — Próxima faixa de IR** (v2, F13). A projeção de uma posição devolve `nextTaxRate`, a
alíquota da faixa seguinte da RN-03, e `nextTaxRateDate`, o primeiro dia em que ela vale, contado
em dias corridos a partir da data do aporte: aporte + 181 dias (20%), + 361 dias (17,5%) ou + 721
dias (15%). A faixa seguinte é a primeira ainda não alcançada na data de referência, limitada pela
RN-02. Os dois campos vêm nulos quando o título é isento (RN-04), quando a alíquota na data de
referência já é 15% ou quando `nextTaxRateDate` cairia depois do vencimento, porque a posição nunca
chega a essa faixa. A data depende só do aporte e da referência: não usa cotação, não é projetada
e não muda `isProjected` nem `isEstimated`.

**RN-17 — Fim do IOF** (v2, F13). A projeção de uma posição devolve `iofFreeDate`, o primeiro dia
em que o IOF é zero pela RN-11: aporte + 30 dias corridos. Vem nulo quando o prazo na data de
referência já é de 30 dias ou mais, quando o título é LCA (IOF zero) ou quando a data cairia depois
do vencimento. LCI, CDB e Tesouro Direto pagam IOF e recebem a data.

**RN-18 — Totais por título no consolidado** (v2, F13). O consolidado devolve `byAsset`: um item
por título com pelo menos uma posição incluída no resumo, com `assetId`, `positionCount` e os
totais de investido, bruto, rendimento bruto, IR, IOF, líquido e ganho líquido. Cada total é a soma
dos itens de `positions` daquele título (RN-07), saídos da mesma lista de projeções, e as regras de
data são as do consolidado. A ordem é a da primeira aparição do título em `positions`. `byAsset`
não traz marca de estimado nem de projetado: quem exibe olha os itens de `positions` do título.

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
| GET | `/api/indexes/{indexType}/accumulated?from=&to=` | Variação acumulada de um indexador no período (v2, F12, RN-13) |
| GET | `/api/assets/{id}/projection?referenceDate=` | Projeção consolidada de um título e dos seus aportes (v2, F12, RN-14) |
| GET | `/health` | Liveness, texto `Healthy`, fora do envelope `ApiResponse<T>` (v2, F8) |

A projeção retorna valor investido, valor bruto, rendimento, alíquota aplicada, imposto
devido e valor líquido. Na v2, retorna também alíquota e valor de IOF, e indica se a projeção
é estimada (RN-10). A partir da F12, indica também se usa taxas projetadas (`isProjected`,
RN-12). As mudanças da F12 no contrato são só aditivas: nenhum campo existente muda de nome ou de
tipo. O que muda de valor são as duas consequências da RN-12 (IPCA+ no mês de hoje e data
informada depois da última cotação de CDI). A F13 acrescenta à projeção `netYield`, `nextTaxRate`,
`nextTaxRateDate` e `iofFreeDate` (RN-15 a RN-17), ao consolidado e à projeção por título
`totalNetYield`, e ao consolidado `byAsset` (RN-18). Também só aditivo, sem mudar nenhum valor existente.

A projeção devolve também `referenceDate`, a data pedida (a informada ou a padrão resolvida,
descrita abaixo), e `applicationDate`, a data do aporte. Numa posição vencida, `referenceDate`
continua sendo a data pedida, mas os valores são os do vencimento (RN-02): quem exibe compara
`referenceDate` com o `maturityDate` do título para rotular a posição como vencida.

Na v2, quando `referenceDate` é omitida na projeção ou no consolidado, a data usada é o dia
útil seguinte à última cotação de CDI importada, e não a data de hoje, para a consulta padrão
não falhar por falta da cotação de ontem. No consolidado, uma posição com aporte posterior a
essa data é projetada na própria data do aporte (rendimento zero, sem exigir cotação). Data
informada explicitamente com dia sem cotação anterior à última cotação continua sendo erro (RN-09);
depois da última cotação, vale a RN-12 (projetado a partir do dia útil anterior a hoje, erro antes
dele). No consolidado com data
informada, as posições aportadas depois dela ficam fora do resumo. A data de "hoje" usada
quando não há nenhuma cotação de CDI é a de Brasília (`America/Sao_Paulo`). O registro de aporte
com data fora do período coberto pelo calendário (RN-08) é rejeitado com 400.

O consolidado devolve `referenceDate` na raiz, sempre presente, inclusive com a carteira vazia:
sem data informada, a data padrão é resolvida mesmo sem posições, e se a resolução falhar por
calendário (cotação de CDI com a tabela de feriados vazia, ou última cotação no limite da
cobertura) a resposta é 400. Com data informada, vale a informada, ainda que todas as posições
fiquem de fora. Devolve também `positions`: as posições incluídas no resumo, não a lista de
aportes (com data informada, a posição aportada depois dela não aparece). Cada item tem
`projection`, no mesmo formato do endpoint de projeção, e `asset`, no mesmo formato de
`/api/assets`. A ordem é a do repositório, aporte mais recente primeiro, sem garantia entre
posições do mesmo dia.

No consolidado há duas datas. A `projection.referenceDate` de cada item é igual à `referenceDate`
da raiz, exceto na posição com aporte posterior à data padrão (sem data informada), em que é a
data do aporte. Para abrir a projeção de um item, o consumidor passa a `projection.referenceDate`
dele: a projeção sem data dessa posição responde 400.

Os totais incluem valores estimados quando algum item tem `projection.isEstimated` verdadeiro
(RN-10). Os percentuais de `byAssetType`, arredondados um a um, podem não somar 100.

A F1 previa um health check; ele só foi entregue na F8, onde o smoke test da imagem publicada
passou a depender dele. `GET /health` é um check de liveness: responde 200 com o texto `Healthy`
enquanto o processo está de pé, não consulta o banco e só aceita GET e HEAD.

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
| Entrega contínua (v2) | GitHub Actions publica a imagem no GitHub Container Registry a cada merge na `main`, depois de um smoke test no próprio runner |
| Infraestrutura como código (v2) | Terraform (decisão de 02/11/2026), com destino na AWS a partir da F11 (decisão de 19/11/2026): ECS Fargate, Application Load Balancer e RDS PostgreSQL num ambiente efêmero, validado antes no emulador Floci. Conta no Free account plan, sem upgrade. Detalhes em `docs/SPEC-F11-EXECUCAO.md` |
| Taxas fixas de indexador (v2) | Removidas: saem a seção `IndexRates` do `appsettings.json` e o `IndexRatesOptions`. Sem cotação, vale a RN-09 |
| Testes de integração (v2) | Testcontainers com PostgreSQL 16 (`postgres:16`): um contêiner por coleção de testes; cada teste ganha um banco próprio, criado a partir de um banco-modelo com as migrations aplicadas |
| Regras de arquitetura (v2) | NetArchTest.Rules |
| Mutation testing (v2) | Stryker.NET no Domain, com limite de 80% (`break`), em workflow separado (`mutation.yml`: semanal e sob demanda, fora do CI de cada push) |
| Navegação do código | graphify, com o grafo local em `graphify-out/`, fora do versionamento |
| Projeção futura (v2, F12) | Taxa constante: o último CDI diário publicado e, no IPCA, a média geométrica dos últimos 12 meses (decisão de 26/11/2026). As expectativas do Boletim Focus ficaram de fora por exigirem mais uma fonte externa |

Padrões mantidos do projeto: controller herda `ControllerBase` e retorna
`StatusCode(response.StatusCode, response)`; respostas encapsuladas em `ApiResponse<T>`;
configuração de entidade via `IEntityTypeConfiguration` com `HasColumnName` em SNAKE_CASE;
repositórios assíncronos retornando `Task<IEnumerable<T>>`. O `GET /health` é a exceção à regra
do envelope: responde o texto puro `Healthy`, porque é um check de liveness, não um recurso de
negócio.

**Importação das cotações** (v2). Duas regras da importação. A busca incremental do CDI parte do
próximo dia útil após a última cotação gravada, e não do dia seguinte: cotação em dia não útil é
descartada e não avança a última data, então partir de "última + 1" pediria a mesma data a cada
execução. A resposta do SGS só é aceita se for JSON bem formado por inteiro; HTML ou JSON cortado
é falha transitória e entra no retry, em vez de falhar sem nova tentativa.

Comentários em português, identificadores em inglês.

## Fases

**F1 — Fundação.** Solution com os quatro projetos, referências entre camadas, Docker
Compose com PostgreSQL (o health check previsto aqui só foi entregue na F8).

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

**F8 — Entrega contínua da imagem.** Health check, correção das pendências da F7 e um pipeline
que, a cada merge na `main`, gera a imagem e o migration bundle, valida os dois contra um
PostgreSQL limpo no runner e publica a imagem no GitHub Container Registry. A publicação em
nuvem com Terraform ficou adiada: a assinatura do Azure está desabilitada e não há orçamento
para reativá-la (decisão de 08/11/2026).

**F9 — Qualidade.** Testes de repositório e de integração contra PostgreSQL real com
Testcontainers, regras de arquitetura com NetArchTest e mutation testing no Domain.

**F10 — Observabilidade, cache e eventos (opcional).** OpenTelemetry, cache das cotações e
eventos com outbox. Só entra se as vagas-alvo pedirem.

**F11 — Nuvem na AWS.** Terraform de um ambiente efêmero na AWS (ECS Fargate, ALB e RDS
PostgreSQL), validado primeiro no emulador Floci e depois subido só durante uma demo, com
prazo de validade e destruição automática. Não depende da F10, que é opcional.

**F12 — Contrato v2 para o front.** O que o front `renda-fixa-web` precisa para mostrar o valor
no vencimento, o CDI e a inflação acumulados e o total de cada título sem calcular dinheiro no
navegador: RN-12 a RN-14, com os dois endpoints novos e o campo `isProjected`. Depende da F9. A
validação ao vivo da F11 foi adiada pelo Matheus e não bloqueia a F12 (decisão de 26/11/2026).

**F13 — Contrato v3 para o front.** O que ainda obrigaria o front a calcular: o ganho líquido, a
data em que o imposto de cada depósito cai, a data em que o IOF zera e os totais de cada título na
lista. RN-15 a RN-18, só aditivas. Depende da F12 (decisão de 27/11/2026).

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
- Um merge na `main` publica uma imagem versionada e testada no GitHub Container Registry, sem
  passo manual
- Testes de integração rodando contra PostgreSQL real no CI
- Mutation score do Domain de pelo menos 80%

## Decisões em aberto

Precisam ser fechadas antes da fase indicada. Ao decidir, registre a escolha na regra ou na
tabela de decisões técnicas e remova o item desta lista.

Nenhuma no momento. A última, Bicep ou Terraform, foi fechada em 02/11/2026: Terraform, por ser
o padrão de infraestrutura como código mais pedido em vagas e não ficar preso ao Azure.
