# QA exploratório F13, Parte A (pilha `qaf13a`, cotações reais, `QA-F13A`)

Executor: agente `qa-exploratorio`. Roteiro: `docs/QA-F13-ROTEIRO.md` (aprovado em 27/11/2026).
Nada foi corrigido. Os scripts e as saídas brutas ficam no scratchpad da sessão
(`.../scratchpad/qaf13/a`, `.../scratchpad/qaf13/js`), não no repositório.

## Ambiente

| Item | Valor |
|---|---|
| Data e hora | 27/11/2026 (sexta), 20h33 a 20h45 em Brasília (23h33 a 23h45 UTC) |
| Commit base | `9174345` (HEAD = origin/main), fase só na árvore de trabalho (git status com 29 entradas, mais os docs novos) |
| API testada | `qaf13a-fase`, `http://localhost:28115`, `Server: Kestrel`, imagem `qaf13-fase:latest` (id `19f21760d833`, a mesma de `renda-fixa-api-api:latest`, construída com o código da fase), `ASPNETCORE_ENVIRONMENT=Development`, importação de cotações ligada (a única importação do período trouxe 0 cotações) |
| Banco | `qaf13a-db` (banco `qaf13`), cópia completa do Compose: 101 títulos e 355 aportes na partida |
| Compose (`8080`) | só leitura. Fotos antes e depois iguais: 101 títulos, 355 aportes, `index_rate` idêntica |
| **T** (hoje em Brasília) | 2026-11-27 |
| **P** (dia útil anterior) | 2026-11-26 |
| **L** (última cotação de CDI) | 2026-11-26 (valor 0.050788) |
| **D** (data padrão, lida em `GET /api/assets/{VAZIO}/projection`) | **2026-11-27** (dia útil seguinte a L: correto) |
| **M** (último IPCA publicado) | 2026-10 (valor 0.14) |
| Calendário (`holiday`) | 399 feriados, 2020-01-01 a 2050-12-25; cobertura de datas de cálculo até 2050-12-31 |
| Ferramentas | Git Bash, curl, node v26.13.1; os scripts `inv13.mjs`, `linha.mjs` e `coerencia13.mjs` exatamente como no roteiro (números lidos no texto com `context.source`, dinheiro em inteiros `BigInt`) |

Ajustes de âncora: o roteiro assume D = 2026-11-26. Com D = 2026-11-27, o `d` das linhas "em D" sobe um
dia (por exemplo `P_CDI1` em D: `d` 11 e não 10; `P_CDI2` em D: `d` 179 e não 178; `P_IPCA1`: 57;
`P_IPCA2`: 24) e o aporte `D − 181` da A01 é **2026-05-30** (`P_CDI3`). Nenhuma data nova (`aporte + n`)
muda, só o `d`. Todos os esperados foram recalculados pela regra pelo `inv13.mjs`, não copiados das
respostas.

## Veredito

**SEM BUGS BLOQUEANTES.** Nenhum BUG, de nenhuma severidade, nos 19 cenários da Parte A. A
validação cruzada (`inv13`, `coerencia13`) rodou sobre a carteira copiada do Compose, com títulos de
todos os tipos, e deu **zero divergências** em todas as datas. Os achados são só RISCOs de severidade
baixa (zeros, `null` com mais de um significado, documentação) e observações sem severidade.

Resumo:

| Cenário | Resultado |
|---|---|
| A00 Reconhecimento | OK |
| A01 Carteira de teste | OK (aporte no dia do vencimento aceito) |
| A02 Linha do tempo de CDI projetado | OK |
| A03 IPCA+ projetado e Tesouro Direto | OK |
| A04 LCI e LCA, `null` presente | OK |
| A05 Vencimento curto, véspera, fronteira | OK |
| A06 Posições vencidas | OK, com RISCO (baixa) R1 |
| A07 Aporte posterior à data padrão | OK |
| A08 `netYield` negativo e zero | OK, com RISCO (baixa) R2 |
| A09 Datas depois da cobertura do calendário | OK |
| A10 Varredura da carteira inteira | OK |
| A11 Coerência entre os três endpoints | OK |
| A12 `byAsset`: sem aportes, depois da data, empates | OK |
| A13 `PUT` e `DELETE` | OK |
| A14 Serialização no texto | OK, com RISCO (baixa) R2 |
| A15 Swagger e snapshot do front | OK, com observação O1 |
| A16 `.http` e README | OK, com observação O2 |
| A17 Respostas de erro | OK |
| A18 Deriva de L e Compose intacto | OK |

## Cenários

### QA13-A00, Reconhecimento: OK

Feito: passos 0 a 5 (variáveis, Compose e pilha já montados pelo coordenador, scripts escritos em
`$QA/js`, criação do `QA-F13A VAZIO`, leitura de T, L, D, M).

- `GET /api/portfolio/summary` na carteira copiada (antes de qualquer título da F13, só o `VAZIO`,
  que não tem aportes): 200, 355 itens, **87 títulos** em `byAsset`.
- `inv13`: **0 divergências** em 355 itens. Motivos dos nulos de `nextTaxRate`: 164 com próxima faixa,
  125 isento (LCI e LCA), 36 vencimento, 30 em 15%. 20 itens com `iofFreeDate`.
- Raiz: `totalNetYield 119961.37` igual a líquido − investido e à soma dos itens; `byAsset` na ordem da
  primeira aparição; 87 títulos sem divergência.
- D = 2026-11-27 é o dia útil seguinte a L = 2026-11-26. Sem regressão da F12.
- Obs.: `curl -w %{size_download}` com `-o /dev/null` imprime `0B` neste curl do Windows; o corpo real
  tem 268653 bytes (`wc -c`). Artefato do curl, não da API.

### QA13-A01, Carteira de teste: OK

Feito: criados todos os títulos e aportes do roteiro (ids em "Dados criados"). Todos os 201 saíram sem
`ERRO`, inclusive:

- `P_VESP2` (aporte em 2026-12-01, o próprio dia do vencimento de `VESP`): **aceito** (201), como manda a
  decisão 5 (RN-05 só recusa data posterior).
- `CAL` (título que vence em 2055, aporte em 2050-12-20): aceito, a A09 foi executada.

Caso 16 numa carteira suja (`GET /api/portfolio/summary?referenceDate=2025-04-22`, outros 28+ títulos no
mesmo resumo):

```
PRE: {"positionCount":2,"totalInvestedAmount":"2000.00","totalGrossAmount":"2009.86","totalGrossYield":"9.86","totalTaxAmount":"1.56","totalIofAmount":"2.96","totalNetAmount":"2005.34","totalNetYield":"5.34"}
LCI: {"positionCount":1,"totalInvestedAmount":"1500.00","totalGrossAmount":"1506.82","totalGrossYield":"6.82","totalTaxAmount":"0.00","totalIofAmount":"2.25","totalNetAmount":"1504.57","totalNetYield":"4.57"}
```

Iguais às linhas A e B do caso 16. Aporte `PRE` em 2025-04-22: `netYield 2.67`, `nextTaxRate 20.0` em
`2025-09-29`, `iofFreeDate 2025-05-01`, `taxRate 22.5`, `iofRate 30`. LCI: `netYield 4.57`, nulos,
`iofFreeDate 2025-05-02`, `iofRate 33`. `inv13` no mesmo resumo: 89 itens, 0 divergentes.

### QA13-A02, Linha do tempo de um CDI com dias projetados: OK

`linha.mjs` com `P_CDI1` (18 datas, de 2026-11-16 a 2029-12-31), `P_CDI2` (11 datas) e `P_CDI3` (3 datas):
**todas as linhas `OK`**, nenhuma regressão de monotonia. Destaques (D = T = 2026-11-27):

- `P_CDI1`: 2026-12-15 `proj=true`, `iofFree 2026-12-16`, `iofRate 3`; 2026-12-16 `iofFree null`;
  2027-05-15 `d=180 taxRate 22.5 next 20.0/2027-05-16`; 2027-05-16 (domingo) `d=181 taxRate 20.0
  next 17.5/2027-11-12`; 2028-11-05 `d=720`; 2028-11-06 `d=721 taxRate 15.0 next null/null`.
- `P_CDI2`: 2026-11-28 `d=180 next 20.0/2026-11-29` (domingo); 2026-11-29 `d=181 taxRate 20.0 next
  17.5/2027-05-28`.
- `P_CDI3` (aporte 2026-05-30, a faixa vira exatamente em D): 2026-11-26 `d=180 taxRate 22.5 next
  20.0/2026-11-27`; 2026-11-27 (D) `d=181 taxRate 20.0 next 17.5/2027-05-26`.
- `isProjected` falso em D e em T (a data padrão nunca projeta CDI) e verdadeiro de 2026-11-28 em
  diante. Datas novas iguais nas linhas projetadas e nas não projetadas da mesma faixa.

### QA13-A03, IPCA+ projetado e Tesouro Direto: OK

`P_IPCA1`, `P_IPCA2`, `P_TDIPCA`, `P_TDPRE` em 10 datas cada: todas `OK`.

- IPCA+ com `isProjected=true` já em D (o mês de hoje, novembro, não tem variação publicada, M =
  2026-10) e `isEstimated=false` em todas as linhas.
- `P_IPCA2` (aporte 2026-11-03): `iofFree 2026-12-03` até 2026-12-02 (`d=29`, `iofRate 3`), `null` de
  2026-12-03 em diante (`d=30`); `next 20.0/2027-05-03`.
- Tesouro Direto: `P_TDIPCA` igual a `P_IPCA2` (mesmas datas, aporte 2026-11-03); `P_TDPRE` (aporte
  2026-11-16): `next 20.0/2027-05-16`, `iofFree 2026-12-16`. Recebe os dois campos como um CDB (RN-17).
- Projeção por título do `TDIPCA`: `netYield 4.56`, `nextTaxRate 20.0`, `nextTaxRateDate 2027-05-03`,
  `iofFreeDate 2026-12-03`, `totalNetYield 4.56`.

### QA13-A04, LCI e LCA nos três endpoints, `null` presente: OK

Executado em sem data, `referenceDate=2026-11-27` e `2026-11-30`, para posição, título e resumo:

- LCI (`LCICDI`, `MD2`): `nextTaxRate:null,nextTaxRateDate:null` e `iofFreeDate` igual a aporte + 30
  (`2026-12-16` e `2026-12-24`) enquanto `d` < 30.
- LCA (`LCA`, `MD3`): os três `null`, inclusive `P_LCA2` (aporte 2026-12-15) projetada na própria data
  sem `referenceDate` (`netYield 0.00`, três nulos).
- Nenhuma resposta com `AUSENTE`; o mesmo texto nos três endpoints (campo sempre presente).

### QA13-A05, Vencimento curto, véspera e fronteira com CDI projetado: OK

- `P_CDI30` (vence 2026-12-16): `iofFreeDate 2026-12-16` (= vencimento) até 2026-12-15 (`d=29`, `iofRate
  3`), `null` de 2026-12-16 em diante (`d=30`, depois fica em 30 pelo corte do vencimento); nenhuma
  `nextTaxRate` (2027-05-16 > vencimento). `isProjected` verdadeiro a partir de 2026-12-01.
- `P_CDI181` (vence 2026-11-29, domingo): `next 20.0/2026-11-29` (= vencimento) em D e em 2026-11-28;
  de 2026-11-29 em diante `taxRate 20.0 next null/null` (`d=181`).
- `P_CURTO`, `P_VESP1`, `P_VESP2`: nenhuma data em nenhuma linha; `P_VESP1` com `iofRate 96` a partir de
  2026-12-01 e `d` travado em 1 depois do vencimento; `P_VESP2` `d=0`, `netYield 0.00`.
- Projeção por título em 2027-01-15: `totalNetYield` presente (8.31; 53.42; 1.46; 0.02) e itens com os
  três campos `null`.

### QA13-A06, Posições vencidas: OK, com RISCO R1

- `P_VENC1`: em 2024-01-02 `20.0/2024-07-01`, `iofFree 2024-02-01`; em 2024-07-01 `17.5/2024-12-28`;
  de 2025-01-02 (vencimento) em diante, para 2025-06-19, T e 2050-12-30: `d=366`, `taxRate 17.5`, três
  campos `null` e **`netYield 82.85` igual em todas as chamadas**.
- `P_VENC2`: 2024-12-31 `iofRate 63`, `iofFreeDate null` (2025-01-19 cairia depois do vencimento);
  2025-01-02 em diante `d=13`, `iofRate 56`, `netYield 0.91` constante.
- Projeção por título (`VENC`) sem data, em T e em 2050-12-30: o mesmo `totalNetYield 83.76`.

**R1 (RISCO, baixa).** Numa posição vencida (e em qualquer posição em que a próxima data caia depois do
vencimento), `nextTaxRate`, `nextTaxRateDate` e `iofFreeDate` vêm `null`, que é o mesmo `null` de LCI e
LCA (isento), de IR já em 15% e de iofFree com `d` ≥ 30. O front só separa os quatro significados
olhando `assetType`, `maturityDate` e `elapsedDays`. O README cita "depois do vencimento" mas não diz
que, num título vencido, os três campos saem nulos. Defensável pela spec; só registro (decisão 5).

### QA13-A07, Aporte posterior à data padrão: OK

Sem data (D = 2026-11-27), 12 itens da carteira têm aporte posterior a D (incluindo todos os de
`QA-F13A` com aporte futuro, e aportes de QAs anteriores em 2030 e 2050). Todos com `referenceDate` igual
à data do aporte, `d=0`, `netYield 0.00`; `P_FUT`: `next 20.0/2027-06-14`, `iofFree 2027-01-14`;
`P_FUTV` e `P_LCA2`: nulos.

- `inv13` sem data: 384 itens, 0 divergentes, `totalNetYield 121027.19` fechando com líquido − investido
  e com a soma dos itens.
- Projeção por título sem data de `FUT`, `FUTV`, `LCA`, `VESP`: itens com as datas esperadas, `totalNetYield`
  0.00, 0.00, 2.74, 0.00.
- `coerencia13 --todos` sem data: 108 títulos, 0 divergentes, 0 faltando, **384 posições conferidas, 0
  divergentes** (os três endpoints iguais campo a campo, os quatro campos novos inclusive).
- `GET /api/positions/{P_FUT}/projection` sem data: **400**, `"A data de referência padrão (2026-11-27,
  o dia útil seguinte à última cotação de CDI) é anterior à data de aporte (2026-12-15). Informe
  referenceDate."`, `data` nulo (comportamento documentado). Com `referenceDate=2026-12-15`: 200 com os
  quatro campos.
- `byAsset` sem data: `FUT` 1, `FUTV` 1, `LCA` 2, `VESP` 2. Com `referenceDate=2026-11-27`: `FUT`,
  `FUTV`, `VESP` **fora**, `LCA` 1.

### QA13-A08, `netYield` negativo e zero ao vivo: OK, com RISCO R2

IPCA importado tem deflação em 2020-04, 2020-05, 2022-07 (−0.68), 2022-08, 2022-09, 2023-06, 2024-08,
2025-08, 2026-08. Usado `MD=2022-07-01`, `FIM=2022-07-29` (`d=28`).

```
GET /api/positions/5402cc34-.../projection?referenceDate=2022-07-29
-> 200 {"investedAmount":1000.00,"grossAmount":995.09,"grossYield":-4.91,"taxRate":22.5,"taxAmount":0,
        "netAmount":995.09,"elapsedDays":28,"iofRate":6,"iofAmount":0,"isProjected":false,
        "netYield":-4.91,"nextTaxRate":20.0,"nextTaxRateDate":"2022-12-29","iofFreeDate":"2022-07-31"}
```

`netYield` = `grossYield` = −4.91, IOF e IR zero, `nextTaxRate 20.0` com aporte + 181 e `iofFreeDate`
aporte + 30 mesmo sem rendimento (como manda a spec). No aporte: `netYield 0.00`. No resumo da mesma data:
`byAsset` do `DEFL` com `totalNetYield -4.91`, `totalGrossYield -4.91`, raiz fechando (`inv13` `OK`,
`totalNetYield 942.74`). Nenhum zero negativo.

**R2 (RISCO, baixa).** Texto do zero e escala: `netYield`/`totalNetYield` em itens saem `0.00`
(escala 2), enquanto `taxAmount` e `iofAmount` saem `0` quando não há rendimento (acima, e na projeção
por título `SEM`, e na carteira vazia, que traz `"totalNetYield":0`, `"totalInvestedAmount":0`). É o
`0.00` contra `0` da decisão 5: sem efeito de valor para o front, que lê número. Vê-se também em A14.

### QA13-A09, Datas depois da cobertura do calendário: OK

`P_CAL` (aporte 2050-12-20): 2050-12-30 `d=10`, `next 20.0/2051-06-19`, `iofFree 2051-01-19` (datas
fora do calendário, que não dependem dele); 2050-12-31 igual com `d=11`. Em 2051-01-02: 400 com `"Data
2051-01-02 fora do período coberto pelo calendário (2020-01-01 a 2050-12-31)."` (RN-08, o mesmo texto da
A17). Projeção por título em 2050-12-30: 200 com os quatro campos.

### QA13-A10, Varredura das regras na carteira inteira: OK

`inv13` em 10 datas (sem data, D, T, 2026-11-30, 2026-12-16, 2026-12-31, 2027-05-16, 2028-01-03,
2030-01-02, 2050-12-30): **0 itens e 0 títulos divergentes**, `totalNetYield` da raiz igual a líquido −
investido e à soma dos itens, `byAsset` na ordem da primeira aparição, soma do `byAsset` igual à raiz
(`positionCount` e totais) e `byAssetType` igual à soma do `byAsset` dos títulos do tipo, em todas.
`grep -c 'DIVERGE|item |título '` em `a10.txt`: **0**. Os três motivos de nulo aparecem (isento,
15%, vencimento): em 2028-01-03, por exemplo, 88 com próxima faixa, 79 nulos por vencimento, 131 isentos,
81 em 15%; em 2030-01-02 e 2050-12-30 as aplicações em 15% passam de 169.

### QA13-A11, Coerência entre os três endpoints: OK

`coerencia13` em sem data (`--todos`), D, T (`--todos`), 2026-11-30, 2026-12-31 e 2028-01-03:
0 títulos divergentes, 0 itens divergentes, 0 títulos fora de `byAsset`, 0 "outra `referenceDate`", **0
totais de mesmo valor e texto diferente** (o texto de `byAsset` e da projeção por título é idêntico), e
0 posições divergentes nas amostras (384 e 372 conferidas por inteiro, 38 por amostra nas outras
datas).

### QA13-A12, `byAsset`: sem aportes, todos depois da data, empates: OK

| referenceDate | `SEM` | `DEP` | posição de DEP, MD1, MD2, MD3 em `byAsset` | em `positions` |
|---|---|---|---|---|
| sem data (D) | fora | 2 | 14, 15, 12, 13 | 15, 16, 13, 14 |
| 2026-11-19 | fora | fora | -1 (todos) | -1 |
| 2026-11-20 | fora | 1 | 0 | 0 |
| 2026-11-23 | fora | 1 | 1 | 1 |
| 2026-11-24 | fora | 2 | 2, 3, 0, 1 | 2, 3, 0, 1 |
| 2026-11-27 (T) | fora | 2 | 3, 4, 1, 2 | 3, 4, 1, 2 |

A ordem relativa de `DEP`, `MD1`, `MD2`, `MD3` em `byAsset` é a da primeira aparição em `positions` na mesma
resposta, em todas as datas. Cinco chamadas iguais a `?referenceDate=2026-11-24`: **a mesma ordem dos oito
primeiros títulos nas cinco** (sem "piscar"). Projeção por título de `SEM`: 200,
`"positions":[]`, totais 0 e `"totalNetYield":0` no fim do objeto, `referenceDate 2026-11-27`.

### QA13-A13, `PUT` e `DELETE` de título: OK

- `PUT` da taxa de `DEP` (10 → 12): os totais de `DEP` em `byAsset` passaram de `totalNetYield 0.37`
  para `0.43` (`totalGrossAmount 2002.65 → 2003.15`) **junto** com os itens e com a projeção por título
  (`coerencia13` em T: 0 divergentes, 105 títulos). As datas novas dos aportes de `DEP` não mudaram
  (`2027-05-24/2026-12-24` e `2027-05-20/2026-12-20`).
- `PUT` de nome de `MD1`: `positions[].asset.name` mostra o novo nome; `byAsset` continua só com
  `assetId,positionCount,totalInvestedAmount,totalGrossAmount,totalGrossYield,totalTaxAmount,totalIofAmount,totalNetAmount,totalNetYield`.
- `PUT` de vencimento em `DEP` (tem aporte): 400, `"Não é possível alterar IssueDate ou MaturityDate
  de um título com aportes."` (RN-06). `PUT` de vencimento em `SEM` (sem aporte): 200; o aporte novo
  (2026-11-24) tem os três campos `null` (2027-05-23 e 2026-12-24 cairiam depois de 2026-12-20).
- `DELETE /api/assets/{DEL}` 204; a projeção do título responde 404. `DELETE` de `DEP` (tem aporte): 400
  `"Título possui aportes e não pode ser removido."`, e continua em `byAsset`.
- `inv13` no resumo "antes" (guardado) ainda lê sem divergência.

### QA13-A14, Serialização no texto: OK, com RISCO R2

Em cinco respostas (resumo sem data, em T, em 2050-12-30; projeção por título; projeção da posição):

- O número de ocorrências de `netYield`, `nextTaxRate`, `nextTaxRateDate`, `iofFreeDate` é **igual ao
  de `positionId`** em todas (385, 373, 385, 3, 1).
- `nextTaxRate` só com `null`, `20.0`, `17.5` e `15.0`; `taxRate` com `22.5`, `20.0`, `17.5`, `15.0` e
  `0` (LCI e LCA). Datas só `null` ou `"yyyy-MM-dd"` (a terceira saída ficou vazia em todas).
- Zeros: `"netYield":0.00,` e `"totalNetYield":0.00}` (itens e `byAsset`). Nenhum zero negativo nem
  notação exponencial.
- `GET /api/portfolio/summary?referenceDate=2000-01-01`: 200, `"positions":[]`, `"byAsset":[]`,
  `"totalNetYield":0`, `"byAssetType":[]`, `referenceDate` presente, `Content-Type:
  application/json; charset=utf-8`.
- Escala do `taxRate` existente (`20.0`, `17.5`, `15.0`, `22.5`): registrada aqui para a comparação
  com a `main` da Parte B (B01). **Não** há como comparar aqui (a `main` não roda na Parte A).

### QA13-A15, Swagger e o snapshot do front: OK, com observação O1

`swdiff` do snapshot `../renda-fixa-web/openapi/renda-fixa-api.json` (18/11/2026) contra o
`swagger.json` ao vivo: **nenhuma rota, schema ou propriedade antiga alterada** (mesmo `type`, `format`,
`nullable`, `$ref`, `items`, mesma ordem). Novos em relação ao snapshot:

- `PortfolioSummaryResponse`: `isProjected` (F12), `totalNetYield:number/double`,
  `byAsset:array?<AssetSummaryResponse>`.
- `PositionProjectionResponse`: `isProjected` (F12), `netYield:number/double`,
  `nextTaxRate:number/double?`, `nextTaxRateDate:string/date?`, `iofFreeDate:string/date?`.
- Schemas novos: `AssetProjectionResponse` (e o `...ApiResponse`), `AssetSummaryResponse`,
  `IndexAccumulationResponse` (e o `...ApiResponse`). Rotas novas: `/api/Assets/{id}/projection` e
  `/api/Indexes/{indexType}/accumulated`.
- `AssetSummaryResponse`: `assetId,positionCount,totalInvestedAmount,totalGrossAmount,totalGrossYield,totalTaxAmount,totalIofAmount,totalNetAmount,totalNetYield`
  (sem `isProjected` nem `isEstimated`, RN-18); `byAsset` é `array` de `AssetSummaryResponse`.
- `nullable` em `PositionProjectionResponse`: `nextTaxRate,nextTaxRateDate,iofFreeDate`; os campos de
  tipo coleção seguem o padrão já existente.
- As chaves reais da resposta de `GET /api/portfolio/summary?referenceDate=2026-11-27` batem com o
  schema, na ordem, para `PortfolioSummaryResponse`, `AssetSummaryResponse` e
  `PositionProjectionResponse`. `GET /swagger/index.html`: 200.

**O1 (observação, sem severidade, fora da fase).** O snapshot do front está desatualizado desde antes da
F12 (não tem `isProjected`, a projeção por título nem o acumulado de índice) e não tem nenhum campo da
F13. O front precisa regenerar o OpenAPI antes de usar `netYield`, `nextTaxRate`, `nextTaxRateDate`,
`iofFreeDate`, `totalNetYield` e `byAsset`.

### QA13-A16, `.http` e o README: OK, com observação O2

Reproduzidas na 28115 as três requisições do `FixedIncome.Api.http` que ganharam comentário da F13, com
`QA-F13A HTTP CDB` e `QA-F13A HTTP CDB CDI`:

- "Projeção do aporte" (`.../projection?referenceDate=2026-06-01`): 200 com `netYield 98.59`,
  `nextTaxRate null`, `nextTaxRateDate null`, `iofFreeDate null` (o vencimento é 2027-01-01).
- "Resumo consolidado da carteira" (`?referenceDate=2026-06-01` e sem data): 200 com `totalNetYield` e
  `byAsset` (54 títulos naquela data); item de `positions` com os quatro campos novos no fim.
- "Projeção por título com referenceDate" (`?referenceDate=2026-09-23`): 200 com `totalNetYield 33.08` e
  o item com `nextTaxRate 20.0`, `nextTaxRateDate 2026-11-29`. Também 200 na "Projeção de aporte com data
  futura" (2026-12-31: `isProjected true`, `nextTaxRate 17.5/2027-05-28`).

Conferência do README ("Ganho líquido, próxima faixa de IR e fim do IOF") contra as cenas:

| Afirmação do README | Onde foi vista | Resultado |
|---|---|---|
| exemplo `2.67`, `20.0`, `2025-09-29`, `2025-05-01`; segunda-feira e feriado | A01 | confere (2025-09-29 é segunda; 2025-05-01 é feriado) |
| exemplo de `byAsset` 2000 / 2009.86 / … / 5.34 | A01 | confere |
| `netYield` negativo e zero no dia do aporte | A08 | confere |
| `nextTaxRate` nulo para LCI e LCA, em 15% e depois do vencimento; igual ao vencimento é devolvida | A04, A05, A10 | confere |
| `iofFreeDate` nula com `d` ≥ 30, LCA e depois do vencimento | A03, A04, A05 | confere |
| datas em dias corridos, sem ajuste para dia útil | A02 (domingos), A01 (feriado) | confere |
| `byAsset` sem marca de estimado nem de projetado; carteira vazia devolve lista vazia | A14, A15 | confere |

**O2 (observação).** O exemplo do README traz `"investedAmount": 1000` e `"totalInvestedAmount":
2000` (formato abreviado); a resposta real traz `1000.00` e `2000.00`. Formato anterior à F13, só
registro.

### QA13-A17, Respostas de erro com os campos novos: OK

Todas com `data: null`, nenhum `byAsset`, `totalNetYield` ou `netYield` parcial:

| Requisição | Resposta |
|---|---|
| `GET /api/portfolio/summary?referenceDate=abc` | 400 `O valor do campo 'referenceDate' é inválido.` |
| `GET /api/portfolio/summary?referenceDate=2051-01-02` | 400 `Data 2051-01-02 fora do período coberto pelo calendário (2020-01-01 a 2050-12-31).` |
| `GET /api/positions/{P_FUT}/projection?referenceDate=2026-11-27` | 400 `A data de referência não pode ser anterior à data de aporte.` |
| `GET /api/assets/00000000-0000-0000-0000-000000000000/projection` | 404 `Título com id ... não encontrado.` |
| `GET /api/assets/{DEL removido}/projection` | 404 |
| `GET /api/assets/abc/projection`, `GET /api/positions/xyz/projection` | 400 `O valor do campo 'id' é inválido.` |
| posição inexistente | 404 `Posição com id ... não encontrada.` |
| `GET /api/assets/{CAL}/projection?referenceDate=2051-01-02` | 400 calendário (mesmo texto) |

Nenhum 500 em toda a Parte A.

### QA13-A18, Deriva de L e Compose intacto: OK

Repetido o passo 5 às 20h38 (BRT): L = 2026-11-26 (não mudou), D = 2026-11-27 (não mudou), M = 2026-10.
A importação de `qaf13a-fase` rodou só na partida e concluiu com `0 de CDI, 0 de CDI descartadas e 0 de
IPCA`. `index_rate` de `qaf13a-db` **idêntica** à foto inicial (diff vazio: nenhuma linha nova, nenhuma
alterada ou apagada). Como L não mudou, não houve motivo para repetir a A11.

Compose (`8080`), conferido pela API e por `select count(*)` em `fixedincome-db`: **101 títulos e 355
aportes**, `index_rate` idêntica à foto do Compose (`compose-index-rate-antes.txt`). Nenhuma escrita
no Compose.

## Exploração além do roteiro (sem achados)

Na `qaf13a-fase`, depois da A18: valores extremos e desempenho, tudo sobre a cópia.

- `POST /api/positions` com `applicationDate 9999-12-31` (título vencendo em 9999-12-31): 400
  `Data de aporte 9999-12-31 fora do período coberto pelo calendário (2020-01-01 a 2050-12-31).` Não há
  como chegar ao estouro de `DateOnly` nas datas novas.
- `investedAmount` 0.01, 0.05, 1234567.89 e 9999999999999 aceitos (201); 0.005 recusado
  (`InvestedAmount deve ter no máximo 2 casas decimais.`); 99999999999999999 recusado (limite de
  10^16). Com os quatro aportes no resumo: `inv13` 0 divergências em sem data, 2026-11-30 e 2030-01-02,
  totais na casa dos 10^12 fechando. Em `0.01` e `0.05`: `netYield 0.00` (rendimento arredonda a zero),
  `nextTaxRate 20.0` e `iofFreeDate` presentes.
- Desempenho do consolidado (391 aportes, 112 títulos, ~300 KB): mediana de 5 de 0,049 s sem data e 0,37 s em
  `referenceDate=2030-01-02` (CDI projetado em todos). Sem regressão perceptível; a comparação com a `main`
  é da B04.

## Lista de bugs

**Nenhum bug (alta, média ou baixa) encontrado na Parte A.**

## RISCOs e observações

Todos baixos, defensáveis pela spec e pelas decisões do Matheus (decisão 5):

- **R1 (RISCO, baixa): `null` com quatro significados.** Isento (LCI/LCA), já em 15%, faixa/data depois
  do vencimento e título vencido saem iguais: `"nextTaxRate":null,"nextTaxRateDate":null`. O front só os
  separa pelo tipo, `maturityDate` e `elapsedDays` do título; o README não cita o título vencido.
  Evidência: A06 (`P_VENC1` em 2025-06-19) e A04 (LCA). Teste que fixaria o comportamento, se o
  Matheus quiser documentar: `ProjecaoDeUmTituloVencido_DeveDevolverOsTresCamposDeDataNulos`.
- **R2 (RISCO, baixa): `0.00` contra `0`.** `netYield` e `totalNetYield` em itens saem `0.00`;
  `taxAmount`/`iofAmount` sem rendimento, e `totalNetYield` na carteira vazia e na projeção de título
  sem aporte, saem `0`. Sem efeito de valor. Evidência: A08, A12 e A14.
- Empate de ordem em `byAsset` entre aportes do mesmo dia: **não reproduzido**, a ordem não variou
  nas cinco chamadas (A12). Totais de mesmo valor com texto diferente entre `byAsset` e a projeção por
  título: **0 casos** (A11).
- **O1 (sem severidade):** snapshot do front desatualizado (A15).
- **O2 (sem severidade):** exemplos do README abreviam `1000.00` para `1000` (A16).

## NÃO EXECUTADO

Nenhum cenário da Parte A ficou sem executar. A comparação "só aditivo" contra a `main` (escala do
`taxRate` existente, texto dos erros, chaves novas por rota) é da Parte B (B01).

## Dados criados durante o QA

Tudo vive **só em `qaf13a-db`** (banco `qaf13`), que some com `docker rm -f qaf13a-fase qaf13a-db`. Nada foi
gravado no banco do Compose (`fixedincome-db`), nem nas cotações. Títulos (28 criados, 27 existem: `DEL`
foi removido na A13) e 36 aportes com prefixo `QA-F13A`. A API não remove título com aporte (RN-06) e
não tem `DELETE` de aporte, então só a limpeza do contêiner os apaga.

Títulos:

| Nome | Id |
|---|---|
| QA-F13A VAZIO (`VAZIO`) | `eb0a96b0-6de5-4beb-968c-987649a4cff4` |
| QA-F13A PRE10 (`PRE`) | `16486cff-1651-444c-a6c1-07ec9e32c83e` |
| QA-F13A LCI PRE10 (`LCI`) | `fe655cc2-c650-44fc-b371-c4e072f19df2` |
| QA-F13A CDI100 (`CDI`) | `202dad82-a2b7-42dc-b4fa-df7ab8b41008` |
| QA-F13A CDI100 VENCE+30 (`CDI30`) | `08154879-874d-48fe-8544-10a89c9ba65c` |
| QA-F13A CDI100 VENCE+181 (`CDI181`) | `7e085f7b-ca20-475f-9e3f-d70693f3c147` |
| QA-F13A PRE CURTO15 (`CURTO`) | `e9f8d660-7a9e-4479-ad29-f18ddae0a2e0` |
| QA-F13A PRE VESPERA (`VESP`) | `5f2cacf0-6e81-48de-85f6-2cee64bf5b90` |
| QA-F13A IPCA6 (`IPCA`) | `6696ef46-a309-4300-8d35-d99766a985c5` |
| QA-F13A TESOURO PRE11 (`TDPRE`) | `31d27812-d005-4d8d-bff0-cf252b548230` |
| QA-F13A TESOURO IPCA6 (`TDIPCA`) | `bc1e4d8c-a499-4ef3-a3d5-c425ee8e0a9c` |
| QA-F13A LCI CDI95 (`LCICDI`) | `64d1a23e-207d-4879-842d-c20768b2ed67` |
| QA-F13A LCA PRE9 (`LCA`) | `66c2bb73-5169-42c7-ae6e-ffab68c9e21e` |
| QA-F13A FUTURO PRE10 (`FUT`) | `035c7257-702d-400c-81d2-350bac756bb2` |
| QA-F13A FUTURO PERTO VENC (`FUTV`) | `e9a47852-0736-4ecc-b5f0-8be8657ac35a` |
| QA-F13A VENCIDO PRE (`VENC`) | `b6cbe11c-37f8-40c4-a5ed-4a29ef8fddda` |
| QA-F13A IPCA2 DEFLACAO (`DEFL`) | `577af054-b42c-44aa-ab2f-d652a32129dd` |
| QA-F13A PRE 2050 (`CAL`) | `ddaaf710-8ab0-4af7-9907-56318dfce91a` |
| QA-F13A SEM APORTE (`SEM`) | `1df10a06-ab9c-4cfd-8067-c5fef3c54414` |
| QA-F13A TODOS DEPOIS (`DEP`) | `6f7aebba-27d1-4b78-808e-b56fabd1540f` |
| QA-F13A MESMO DIA 1 renomeado (`MD1`) | `6c7f0259-72ef-4a4a-b345-609034633196` |
| QA-F13A MESMO DIA 2 (`MD2`) | `87f470fe-806f-4c62-9f7d-a130e40589ec` |
| QA-F13A MESMO DIA 3 (`MD3`) | `82ba2ff0-8248-4e3c-b2ed-a8d49d8caeb8` |
| QA-F13A REMOVIVEL (`DEL`), **removido na A13** | `ecf96737-dec8-442f-ae48-e060105dbab7` |
| QA-F13A HTTP CDB (`H1`) | `3bf9c784-fdc3-4bb1-9d2f-6883fedd86af` |
| QA-F13A HTTP CDB CDI (`H2`) | `7c9d715a-ddc5-4111-8ad2-8a404c558672` |
| QA-F13A EXTREMO 9999 (`X`, sem aporte) | `2cd6c779-c51e-4ce9-9a42-91ddedbfebb9` |
| QA-F13A EXTRA VALORES (`Y`) | `1e136f05-daaf-4b6a-9602-9ccbe91cc60d` |

Aportes:

| Nome | Id |
|---|---|
| `P_PRE1` (PRE, 2025-04-01) | `150b3da3-49b1-40c8-aa53-7ca916bece7e` |
| `P_PRE2` (PRE, 2025-04-01) | `919f7db1-bcb2-4a88-aad9-d4b26f9bade8` |
| `P_LCI` (2025-04-02, 1500) | `5d9e7707-8995-47e7-954a-fb5871c13684` |
| `P_CDI1` (2026-11-16) | `2d628b99-1183-4f9a-8c15-f7c27d45bf71` |
| `P_CDI2` (2026-06-01) | `764f2712-5f51-400f-884c-705b2fc03f8b` |
| `P_CDI3` (2026-05-30 = D − 181) | `75687e69-7fa4-48f6-9c13-1a222c0c5c7e` |
| `P_CDI30` | `722566e9-cd6d-4834-88d4-d49d73aed8ec` |
| `P_CDI181` | `620f86e4-ee40-4388-bc4e-0c80e73219b6` |
| `P_CURTO` | `9bcc0c1c-dac9-4174-95e3-e1d83445dded` |
| `P_VESP1` (2026-11-30) | `07c28ab8-e900-480d-94e3-6c2a75a8f6c9` |
| `P_VESP2` (2026-12-01, = vencimento) | `ec682b2e-6c18-45dc-b66a-a614d312173d` |
| `P_IPCA1` (2026-10-01) | `f68198c3-d864-4db6-a37f-df8d12073657` |
| `P_IPCA2` (2026-11-03) | `f864ae51-1ebf-409e-b285-2f5e9b880663` |
| `P_TDPRE` | `0ca6ef2a-e6e4-4674-83c8-305328731aa2` |
| `P_TDIPCA` | `7608b4d7-edaa-44eb-8045-3427b9316764` |
| `P_LCICDI` | `1744dbb7-b1fb-411f-9a6b-0683449aaf43` |
| `P_LCA1` (2026-11-16) | `6f5ba961-f6f5-48a9-99bd-2e4f5ae22fe4` |
| `P_LCA2` (2026-12-15, 500) | `b9af27cb-02bd-4e5c-90be-f94f234b2450` |
| `P_FUT` (2026-12-15) | `a1a979f2-0e06-4077-aa1e-c3f1cc5b4a2b` |
| `P_FUTV` (2026-12-15) | `ca0243f1-4967-4ebb-9645-8a3c439a66b4` |
| `P_VENC1` (2024-01-02) | `700bdc74-c390-41a9-87ef-b5f8457fa65e` |
| `P_VENC2` (2024-12-20) | `6f8fe9a9-24ba-49d3-a764-1f03951ca6cd` |
| `P_DEFL` (2022-07-01) | `5402cc34-c95d-4fff-8766-e38a6767c3c8` |
| `P_CAL` (2050-12-20) | `c5f19208-9d95-4059-81c0-71ddc197ebe7` |
| `P_DEP1` (2026-11-20) | `2d5b1564-82c4-4ae4-91db-3cf3f656deaf` |
| `P_DEP2` (2026-11-24) | `bb97e07d-31ec-4a2c-9c3c-aaa6cc651c99` |
| `P_MD1` | `b2af0aa9-c2b9-47ab-9bbd-9a009d5df5fb` |
| `P_MD2` | `7fd26f05-734a-4e9c-be32-2f6b75903c52` |
| `P_MD3` | `2e418c9b-5d1d-4563-bfe0-3edf16ee7c29` |
| `P_SEM` (2026-11-24) | `5d69c6b0-b50b-4cd6-a0b5-3115a69492b3` |
| `PH1` (HTTP CDB, 2025-06-01) | `39c829b5-3cf1-4af0-8ab4-cd2f680f7e36` |
| `PH2` (HTTP CDB CDI, 2026-06-01) | `8f192f79-eb2b-4a3a-a5d6-497f50db4b9e` |
| `P_Y` 9999999999999 | `0f073778-0e92-4da9-afa3-7a1e5a1842d7` |
| `P_Y` 1234567.89 | `ad534bc5-ea1d-400b-9ba2-b9ff914fa598` |
| `P_Y` 0.05 | `6ddd9069-ce1e-4209-80db-23385d8d7c33` |
| `P_Y` 0.01 | `8d53d52b-8c95-48d4-9439-28bc3f9915ae` |

Estado de `qaf13a-db` no fim: 128 títulos (101 + 27) e 391 aportes (355 + 36). Compose: 101 e 355,
inalterados. O arquivo bruto com as variáveis e os ids fica em `.../scratchpad/qaf13/a/ids.txt` e
`vars.sh`.
