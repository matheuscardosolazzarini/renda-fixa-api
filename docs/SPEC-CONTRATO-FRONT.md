# Spec de execução — Contrato para o front (L-01 e L-03)

Status: aprovada (16/11/2026)

Decisões do Matheus na aprovação: A-1 (`applicationDate` na projeção) e A-4 (teste do
`swagger.json`) entram; A-2 (`isEstimated` na raiz do resumo) e A-3 (`calculationDate` ou
`isMatured`) não entram. A mudança da carteira vazia (D-4) e o item `{ projection, asset }` (D-5)
valem como descritos.

Documento de execução para agente. Tarefa fora das fases. Objetivo: a projeção e o resumo da
carteira passam a dizer qual data de referência foi usada (L-03), e o resumo passa a devolver
também as posições incluídas, cada uma com a projeção e os dados do título (L-01). As mudanças são
só aditivas: nenhum campo existente muda de nome, de tipo ou de significado, e nenhuma regra de
cálculo muda.

## Contexto

A especificação funcional está em `docs/ESPECIFICACAO.md`. As convenções de código estão em
`CLAUDE.md`. Ambas prevalecem sobre qualquer suposição. As lacunas vêm de
`../renda-fixa-web/docs/ESPECIFICACAO.md`, seção "Lacunas da API":

- **L-01.** O resumo não traz os valores por posição. Para mostrar bruto e líquido de cada posição,
  o front faria uma chamada de projeção por posição, e essas chamadas não seguem as regras do
  resumo (ver "Por que estender o resumo").
- **L-03.** Projeção e resumo não dizem qual data foi usada quando `referenceDate` é omitida. O
  front não consegue escrever "posição em 09/11/2026" sem pedir a data.

L-02 e L-04 não entram.

Estado de partida (linha de base medida em 16/11/2026): branch `contrato-para-o-front`, árvore
limpa, build sem warning, 683 testes verdes (Domain 332, Application 112, Infrastructure 172,
Api 59, Architecture 8).

Nomenclatura: "achado R-02" é a exclusão, do resumo com data informada, das posições aportadas
depois dela. Não confundir com a RN-02, o limite do cálculo no vencimento do título.

Como o código está hoje:

- `GetPositionProjectionUseCase` resolve a data (`referenceDate` informada, ou o dia útil seguinte
  à última cotação de CDI, ou hoje em Brasília), chama `Position.Project` e monta
  `PositionProjectionResponse.FromProjection(positionId, assetId, projection)`. A data resolvida
  não sai na resposta.
- `GetPortfolioSummaryUseCase` monta uma lista `(PositionProjection, AssetType)` com as posições
  incluídas, chama `PortfolioSummary.Create` (Domain, RN-07) e devolve
  `PortfolioSummaryResponse.FromSummary(summary)`. As projeções individuais e os títulos, que o
  caso de uso já tem em mãos, são descartados. Com a carteira vazia, a data padrão nem é resolvida.
- No resumo, a data usada por posição nem sempre é a do resumo: sem `referenceDate`, a posição com
  aporte posterior à data padrão é projetada na própria data do aporte (rendimento zero).

Esta tarefa toca:

- `src/FixedIncome.Application/DTOs` — `PositionProjectionResponse`, `PortfolioSummaryResponse`
- `src/FixedIncome.Application/UseCases` — `GetPositionProjectionUseCase`,
  `GetPortfolioSummaryUseCase`
- `tests/FixedIncome.Application.Tests`, `tests/FixedIncome.Api.Tests`
- `docs/ESPECIFICACAO.md`, `docs/ROADMAP.md`, `README.md`, `src/FixedIncome.Api/FixedIncome.Api.http`

## Fora de escopo

- `FixedIncome.Domain`: nenhum arquivo muda. `Position.Project`, `PositionProjection` e
  `PortfolioSummary` ficam como estão (por isso o Stryker não roda nesta tarefa)
- Regras de cálculo, de data padrão, de exclusão por data (R-02) e de aporte posterior à data
  padrão: nenhuma muda
- Endpoint novo, parâmetro novo, paginação, filtro ou ordenação configurável da lista
- Controllers, repositórios, migrations e pacotes NuGet: nenhum muda
- L-02 (título na resposta de `/api/positions`) e L-04 (Swagger fora de `Development`)
- Qualquer arquivo em `../renda-fixa-web`

## Decisões

**D-1. Estender o resumo, sem endpoint novo.** `GET /api/portfolio/summary` ganha `positions`. Os
totais e as posições saem da mesma lista de projeções, na mesma execução: valem para as duas coisas
a mesma data, a exclusão das posições aportadas depois da data informada (achado R-02) e a projeção na
data do aporte quando ele é posterior à data padrão. Um endpoint de projeção em lote teria de
repetir essas três regras ou divergir delas, e o front teria duas respostas que podem não fechar
(chamadas em instantes diferentes, com uma importação de CDI no meio). O custo é a resposta do
resumo crescer com a carteira; a carteira é pessoal e pequena, e o caso de uso já calcula todas as
projeções hoje.

**D-2. `referenceDate` é a data pedida, passada a `Position.Project`.** É a data informada ou a
data padrão resolvida. Quando ela é posterior ao vencimento do título, os valores são os do
vencimento (RN-02): `elapsedDays` e `businessDays` contam até o vencimento e o valor não rende
depois dele. Quem exibe compara `referenceDate` com `maturityDate` do título (comparação de datas,
não cálculo) e rotula a posição como vencida. No resumo, o `maturityDate` vem no próprio item
(D-5); na projeção, o front busca o título.

**D-3. No resumo há duas datas, e as duas saem.** `referenceDate` na raiz é a data do resumo (a
informada ou a padrão). Cada item de `positions` traz a própria `referenceDate`, dentro de
`projection`: é igual à da raiz, exceto na posição com aporte posterior à data padrão (sem data
informada), em que é a data do aporte. Sem isso a resposta diria que uma posição aportada em 05/05
foi calculada em 22/04.

**D-4. Carteira vazia também devolve `referenceDate`.** Hoje a data padrão só é resolvida quando há
posição. Passa a ser resolvida sempre que `referenceDate` for omitida, para o campo nunca ser nulo
e o front ter um só caminho. Com data informada e todas as posições fora do resumo, vale a
informada. Consequência a aceitar: a carteira vazia sem data faz uma consulta a mais (última
cotação de CDI e feriados) e, se a resolução da data padrão lançar `DomainException` (há cotação de
CDI e a tabela de feriados está vazia, ou a última cotação está no limite da cobertura do
calendário), responde 400 onde hoje responde 200 zerado. Nesses estados, qualquer carteira não
vazia já responde 400. Alternativas descartadas: campo nulo (devolve ao front os dois caminhos que
a L-03 quer eliminar) e capturar a exceção caindo para "hoje" (esconde um banco quebrado e devolve
uma data diferente da que a projeção usaria).

**D-5. Item por composição: `{ projection, asset }`.** Cada item de `positions` tem `projection`,
que é o mesmo `PositionProjectionResponse` do endpoint de projeção (com `positionId`, `assetId` e
`referenceDate`), e `asset`, que é o mesmo `AssetResponse` de `/api/assets`. Nenhum DTO de campos é
duplicado, "os mesmos campos da projeção" vale por construção, e o front reaproveita os dois tipos
gerados do OpenAPI. `AssetResponse` traz `issueDate` e `maturityDate` além dos seis campos pedidos;
ficam, porque criar um DTO só para tirá-los seria abstração a mais e porque `maturityDate` é o que
permite ao front rotular a posição vencida (D-2). Alternativa descartada: item plano, com os campos
da projeção e do título no mesmo nível (repete 14 campos e colide `assetId` com `id` do título).
O record do item se chama `PortfolioPositionResponse`, aninhado em `PortfolioSummaryResponse`.
Observação para o front, sem mudança: `asset.assetType` é `string` no OpenAPI (como em
`/api/assets`) e `byAssetType[].assetType` é `enum`; no JSON os dois saem como o mesmo texto.

**D-6. Ordem de `positions`.** A do repositório, que é a de `GET /api/positions`: aporte mais
recente primeiro. Nenhuma ordenação nova. Entre posições com a mesma data de aporte a ordem não é
garantida (já é assim hoje), e nenhum teste depende dela.

**D-7. O Domain não muda.** `PortfolioSummary.Create` continua recebendo `(Projection, AssetType)`
e calculando os totais (RN-07). `positions` e `referenceDate` são dados de apresentação montados
pelo caso de uso a partir da mesma lista que alimenta `Create`. O que garante que a soma das
posições fecha com os totais é essa lista única, e é isso que os testes afirmam.

**D-8. `positions` são as posições incluídas no resumo, não a lista de aportes.** Com data
informada, a posição aportada depois dela não aparece (achado R-02). Para abrir a projeção a partir
de um item, o consumidor passa a `projection.referenceDate` do item: a projeção sem data de uma
posição com aporte posterior à data padrão responde 400 (B-03), enquanto o resumo a projeta na data
do aporte. A divergência já existe e é intencional; passa a ficar escrita.

### Propostas das revisões além do escopo pedido

| # | Proposta | Origem | Decisão do Matheus |
|---|---|---|---|
| A-1 | `applicationDate` em `PositionProjectionResponse` (no P1). A tela Carteira do front lista "valor aportado e data do aporte", e sem o campo o front continua chamando `/api/positions` só pela data | revisor-arquitetura (importante) | **Entra**, no P1 |
| A-2 | `isEstimated` na raiz do resumo (verdadeiro se algum item for estimado). Hoje os totais podem conter estimativa de IPCA sem aviso | especialista (importante) | Não entra: com `positions` o front já detecta. A regra fica escrita na ESPECIFICACAO (P4) |
| A-3 | `calculationDate` ou `isMatured` na projeção, para a posição vencida | especialista (importante) | Não entra: a D-2 resolve com `maturityDate` |
| A-4 | Teste de API lendo `/swagger/v1/swagger.json` e afirmando os campos novos nos dois schemas (é de onde o front gera o `schema.ts`) | revisor-arquitetura (menor) | **Entra**, no P3 |

Observação sem ação: o OpenAPI marca todos os campos como opcionais (o `AddSwaggerGen` não declara
`required`), inclusive os novos. É pré-existente e fica como possível lacuna futura do front.

### Contrato resultante

`GET /api/positions/{id}/projection` — `data` ganha um campo:

```json
{
  "positionId": "…", "assetId": "…",
  "investedAmount": 1000, "grossAmount": 1004.93, "grossYield": 4.93,
  "taxRate": 22.5, "taxAmount": 0.78, "netAmount": 1002.67,
  "elapsedDays": 21, "businessDays": 13,
  "iofRate": 30, "iofAmount": 1.48, "isEstimated": false,
  "referenceDate": "2025-04-22"
}
```

`GET /api/portfolio/summary` — `data` ganha dois campos:

```json
{
  "totalInvestedAmount": 1500, "totalGrossAmount": 1504.93, "totalGrossYield": 4.93,
  "totalTaxAmount": 0.78, "totalIofAmount": 1.48, "totalNetAmount": 1502.67,
  "positionCount": 2,
  "byAssetType": [ … ],
  "referenceDate": "2025-04-22",
  "positions": [
    {
      "projection": { "positionId": "…", "assetId": "…", "investedAmount": 500, "…": "…",
                      "referenceDate": "2025-05-05" },
      "asset": { "id": "…", "name": "LCI Banco Y", "issuer": "Banco Y", "assetType": "Lci",
                 "indexType": "Cdi", "rate": 100, "issueDate": "…", "maturityDate": "…" }
    },
    {
      "projection": { "positionId": "…", "…": "…", "referenceDate": "2025-04-22" },
      "asset": { "…": "…" }
    }
  ]
}
```

As alíquotas do exemplo são as do caso de referência (dia 21 do IOF: 30%; IR até 180 dias: 22,5%),
em pontos percentuais, a escala que a API já devolve.

Respostas de erro (400, 404) não mudam: `data` continua nulo.

## Implementação em passos

Cada passo: teste antes do código, `dotnet build` sem warning e `dotnet test` verde ao final.

**P1 — `referenceDate` e `applicationDate` na projeção (L-03 e A-1).**
`PositionProjectionResponse` ganha `DateOnly ReferenceDate` e `DateOnly ApplicationDate` como
últimos parâmetros do record; `FromProjection` passa a receber as duas datas.
`GetPositionProjectionUseCase` passa a `date` já resolvida e `position.ApplicationDate`. No JSON, a
projeção ganha `"applicationDate": "2025-04-01"` ao lado de `referenceDate`. Nenhuma mudança na
resolução da data nem nas mensagens de erro. A chamada de `FromProjection` no resumo só aparece no
P3.

**P2 — `referenceDate` no resumo (L-03).**
`PortfolioSummaryResponse` ganha `DateOnly ReferenceDate`. Em `GetPortfolioSummaryUseCase`, a data
do resumo é `referenceDate ?? data padrão`, resolvida sempre que `referenceDate` for omitida,
inclusive com a carteira vazia (D-4); com data informada vale a informada, mesmo que todas as
posições fiquem de fora. Três condições mantêm o cálculo intacto:

1. a resolução fica depois da verificação de título inexistente (404) e do filtro por data;
2. fica dentro do tratamento de `DomainException` que já existe;
3. a regra do aporte posterior (`DateFor`) continua decidida por a `referenceDate` **informada**
   ser nula, nunca pela data do resumo já resolvida.

As posições fora do resumo continuam fora do carregamento de mercado, e os títulos continuam
carregados uma única vez.

**P3 — `positions` no resumo (L-01).**
`PortfolioSummaryResponse` ganha `IReadOnlyList<PortfolioPositionResponse> Positions`, com
`PortfolioPositionResponse(PositionProjectionResponse Projection, AssetResponse Asset)` aninhado,
como `AssetTypeSummaryResponse` já é. O caso de uso mantém uma única lista com a posição, o título,
a projeção e a data usada (`DateFor(position)`) de cada posição projetada. `PortfolioSummary.Create`
recebe essa lista projetada em `(Projection, AssetType)`, e os itens saem dela na ordem em que as
posições vieram do repositório. `Position.Project` é chamado uma vez por posição: o item reaproveita
a mesma projeção, sem recalcular. Carteira vazia e data anterior a todos os aportes devolvem
`positions: []`.

**P4 — Documentação.**
`docs/ESPECIFICACAO.md`: seção "Endpoints" descreve os campos novos da projeção e do resumo, as
duas datas do resumo (D-3), a posição vencida (D-2), a carteira vazia (D-4) e o que `positions`
contém (D-8); diz também que os totais incluem valores estimados quando algum item tem
`projection.isEstimated` verdadeiro (RN-10) e que os percentuais de `byAssetType`, arredondados um
a um, podem não somar 100. A RN-07 ganha a frase: os totais são a soma dos itens de `positions`, e
a soma é exata porque totais e itens saem da mesma lista de projeções, sem recálculo. `README.md`: exemplo de resposta do resumo e da projeção com os
campos novos. `FixedIncome.Api.http`: comentário nas chamadas de projeção e resumo e uma chamada do
resumo sem `referenceDate`. `docs/ROADMAP.md`: item em "Manutenção". Em `../renda-fixa-web` nada é
alterado; a baixa de L-01 e L-03 lá é do fluxo do front.

## Testes

Padrão do `CLAUDE.md` e da skill `testes-xunit`: xUnit, AAA, stubs `private sealed`, nome
descrevendo o comportamento. Os valores esperados reutilizam o caso de referência já conferido dos
testes existentes (PreFixed 10% a.a., R$ 1.000 de 2025-04-01 a 2025-04-22: bruto 1.004,93, IOF
1,48, IR 0,78, líquido 1.002,67), reconferido à mão pelo `especialista-renda-fixa` nesta revisão
(13 dias úteis, 21 corridos, fator 1,0049289). Os totais dos casos 12a e 12b derivam dele e também
foram conferidos pelo especialista. Nenhum valor de IPCA novo é introduzido.

Unitários — `GetPositionProjectionUseCaseTests` (P1):

1. Data informada: `ReferenceDate` é a informada.
2. Data omitida com CDI importado: `ReferenceDate` é o dia útil seguinte à última cotação
   (2025-04-22 para CDI em 2025-04-17).
3. Data omitida sem CDI: `ReferenceDate` é a data de hoje em Brasília do `TimeProvider`.
4. Data informada posterior ao vencimento (D-2): CDB PreFixed 10%, vencimento 2025-04-22, R$ 1.000
   em 2025-04-01, `referenceDate` 2025-05-05. `ReferenceDate` é 2025-05-05; `ElapsedDays` 21,
   `BusinessDays` 13, bruto 1.004,93, IOF 1,48, IR 0,78, líquido 1.002,67 (os do vencimento).

Unitários — `GetPortfolioSummaryUseCaseTests` (P2 e P3):

5. Data informada: `ReferenceDate` da raiz é a informada.
6. Data omitida com CDI: `ReferenceDate` da raiz é a data padrão.
7. Carteira vazia sem data: 200, totais zerados, `ReferenceDate` é a data padrão, `Positions` vazia.
8. Carteira vazia sem data e sem CDI: `ReferenceDate` é hoje em Brasília.
9. Data informada anterior a todos os aportes: `ReferenceDate` é a informada, `Positions` vazia.
10. Cada item traz a projeção da posição e o título dela (id, nome, emissor, tipo, indexador, taxa),
    com duas posições de títulos diferentes.
11. Duas posições do mesmo título: dois itens, mesmo `Asset.Id`, `PositionId` diferentes.
12. Fechamento: a soma de cada um dos seis campos monetários dos itens (`InvestedAmount`,
    `GrossAmount`, `GrossYield`, `TaxAmount`, `IofAmount`, `NetAmount`) é igual ao total
    correspondente, e `Positions.Count == PositionCount`. Dois casos, todos PreFixed 10% a.a.,
    R$ 1.000 cada, de 2025-04-01 a 2025-04-22:
    - **12a. Duas posições do mesmo CDB.** Cada item: bruto 1.004,93, IOF 1,48, IR 0,78, líquido
      1.002,67. Totais: investido 2.000,00, bruto 2.009,86, rendimento 9,86, IOF 2,96, IR 1,56,
      líquido 2.005,34. O teste afirma IR 1,56 e líquido 2.005,34: calcular sobre R$ 2.000 daria IR
      1,55 e líquido 2.005,35, e é isso que distingue a RN-07 de um consolidado recalculado.
    - **12b. CDB, LCI e LCA.** CDB: líquido 1.002,67. LCI: IR 0, IOF 1,48, líquido 1.003,45. LCA:
      IOF 0, IR 0, líquido 1.004,93. Totais: investido 3.000,00, bruto 3.014,79, rendimento 14,79,
      IOF 2,96, IR 0,78, líquido 3.011,05.
13. Sem data, aporte posterior à data padrão (cenário do teste existente de carteira mista): o item
    da LCI tem `ReferenceDate` 2025-05-05, 500 investido, bruto e líquido 500, rendimento, IOF e IR
    zero, `ElapsedDays` e `BusinessDays` zero; o item do CDB e a raiz têm 2025-04-22 (D-3).
14. Data informada: a posição aportada depois dela não aparece em `Positions`, e o fechamento do
    caso 12 vale com as que ficaram.
15. Ordem dos itens igual à do repositório (datas de aporte distintas).
16. `IsEstimated` do item reflete a projeção, com o cenário já conferido de
    `PositionIpcaProjectionTests` (IPCA+ 6%, R$ 1.000 de 2026-09-01 a 2026-10-15, IPCA publicado
    até setembro: bruto 1.018,78, IR 4,23, líquido 1.014,55, estimado).
    Os testes 1 a 4 afirmam também `ApplicationDate` igual à data do aporte; o 10 e o 13 afirmam a
    `ApplicationDate` de cada item (A-1).
17. Título inexistente, sem data, com CDI no stub e calendário vazio: 404. Prova que a resolução da
    data padrão não subiu para antes da verificação de existência.
18. Carteira vazia, sem data, com CDI no stub e calendário vazio: 400 com a mensagem do calendário.
    Prova a consequência aceita na D-4 e que a resolução está dentro do tratamento de
    `DomainException`.

Os testes existentes dos dois casos de uso continuam passando sem alteração de valor esperado.

De API, contra o PostgreSQL do Testcontainers — `PositionProjectionFlowTests` e
`PortfolioSummaryFlowTests`:

19. Projeção com `referenceDate` informada devolve `data.referenceDate` igual, no formato
    `yyyy-MM-dd`.
20. Projeção sem `referenceDate`, com CDI gravado no banco, devolve a data padrão.
21. Resumo com data: `data.referenceDate`, `data.positions` com `projection` e `asset` em camelCase,
    enums como texto; a `projection` de cada item (casado por `positionId`, não pela ordem) é
    igual, campo a campo, à resposta de `GET /api/positions/{id}/projection` chamada com
    `referenceDate` igual à `projection.referenceDate` do item; a soma dos itens fecha com os seis
    totais. Só títulos PreFixed, para não depender de série de IPCA.
22. Resumo sem data, com CDI gravado e um aporte posterior à data padrão: 200, o item tem
    `referenceDate` do aporte, a raiz tem a data padrão.
23. Resumo de carteira vazia: 200, `positions: []`, `referenceDate` presente.
24. Os campos antigos das duas respostas continuam presentes com os mesmos nomes (teste que enumera
    as propriedades de `data`).
25. (A-4, no P3) `GET /swagger/v1/swagger.json`: o schema da projeção tem `referenceDate` e
    `applicationDate` com formato `date`; o schema do resumo tem `referenceDate` e `positions`, e o
    item de `positions` referencia os schemas da projeção e do título.

Os testes 19 e 21 afirmam também `applicationDate` (A-1).

## Critérios

Marcados em 16/11/2026 com a evidência de `docs/VALIDACAO-CONTRATO-FRONT.md` (712 testes, antes
683). QA em `docs/QA-CONTRATO-FRONT.md`: 20 casos, nenhum bug.

- [x] `GET /api/positions/{id}/projection` devolve `referenceDate` com e sem o parâmetro, e
      `applicationDate`
- [x] O `swagger.json` descreve os campos novos da projeção e do resumo
- [x] `GET /api/portfolio/summary` devolve `referenceDate` com e sem o parâmetro, inclusive com a
      carteira vazia
- [x] `GET /api/portfolio/summary` devolve `positions`, cada item com a projeção e o título
- [x] A soma de cada campo monetário dos itens é igual ao total correspondente, e a quantidade de
      itens é igual a `positionCount`, com e sem data informada
- [x] A `projection` de um item é idêntica à do endpoint de projeção chamado com a
      `projection.referenceDate` do item
- [x] Nenhum campo existente mudou de nome, tipo ou significado; nenhum teste existente teve valor
      esperado alterado
- [x] Nenhum arquivo de `src/FixedIncome.Domain` mudou; nenhuma migration; nenhum pacote novo
- [x] `dotnet build` sem warning; `dotnet test` verde, com a contagem maior que a da linha de base;
      cobertura de Domain e Application acima de 80%
- [x] `ESPECIFICACAO.md`, `README.md`, `FixedIncome.Api.http` e `ROADMAP.md` atualizados
- [x] Nenhuma alteração em `../renda-fixa-web`
