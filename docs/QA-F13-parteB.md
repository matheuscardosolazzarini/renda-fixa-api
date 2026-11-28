# QA exploratório da F13 — Parte B (fase contra `main`, pilha isolada)

Execução do roteiro `docs/QA-F13-ROTEIRO.md`, cenários QA13-B00 a QA13-B07. Caixa-preta: nenhum
arquivo de `src/` foi aberto.

## Ambiente

| Item | Valor |
|---|---|
| Data e hora | 27/11/2026, de cerca de 20h30 a 20h45 em Brasília (cabeçalho `Date` da API: 23:33 a 23:44 UTC) |
| T / P / M / L | T = 2026-11-27 (sexta), P = 2026-11-26, M = 2026-10, L = 2026-11-26 (Cdi: 1732 cotações; Ipca: 82, última 2026-10-01) |
| D lido | `referenceDate` do resumo sem data em `qaf13-fase` = **2026-11-27** (D = T). As âncoras "em D" do roteiro (escritas para D = 2026-11-26) foram recalculadas por regra pelos scripts (`inv13`, `linha`), não copiadas |
| Fase | `qaf13-fase` em `http://localhost:28113`, imagem `qaf13-fase:latest` (id 19f21760d833, igual à `renda-fixa-api-api:latest`), `Server: Kestrel`, importação desligada (log: "Importação de cotações desligada") |
| Main | `qaf13-main` em `http://localhost:28114`, imagem `qaf13-main:latest` (156e9de42bb5) montada de `git archive origin/main`, commit **91743453dd1395a38754f5256e9fc8aa0c10a216**; o `swagger.json` da main não tem `netYield` e o da fase tem |
| Banco | `qaf13-db` (postgres:16-alpine, banco `qaf13`), compartilhado pelas duas APIs; esquema e mercado copiados do Compose; carteira copiada no B00 (101 títulos, 355 aportes) |
| Repositório | `HEAD` = `origin/main` = 9174345 (a fase só na árvore de trabalho) |
| Node | v26.13.1 (`JSON.parse` com `context.source` funciona; os scripts leem o texto dos números) |
| Scripts e saídas brutas | `…/scratchpad/qaf13/js` e `…/scratchpad/qaf13/b` (`b00.txt`, `b01-full.txt`, `b02.txt`, `b03.txt`, `b04*.txt`, `b05.txt`, `b06a.txt`, `b06b.txt`, `b07.txt`, `b-extra1.txt`, `b-extra2.txt`) |

Regras de classificação usadas (decisões do Matheus no roteiro): campo antigo com valor ou texto
diferente da `main`, inclusive escala do `taxRate`, é BUG; `0.00` contra `0`, os quatro significados de
`null`, empate de ordem em `byAsset` e totais de mesmo valor com texto diferente são RISCO (baixa).

## Veredito

**SEM BUGS BLOQUEANTES.** Nenhum BUG em B00 a B07. Dois RISCOs de baixa severidade (desempenho do
consolidado e o conjunto `0`/`0.00` + `null`), registrados abaixo.

## Resumo por cenário

| Cenário | Resultado |
|---|---|
| QA13-B00 Carteira vazia e carga | OK |
| QA13-B01 Só aditivo, `main` x fase | OK (1793 rotas, 0 divergentes) |
| QA13-B02 Swagger `main` x fase | OK |
| QA13-B03 Muitos títulos e um título com 252 aportes | OK |
| QA13-B04 Desempenho do consolidado | OK com RISCO (baixa): fase 9% a 31% mais lenta que a `main` nos resumos com projeção grande |
| QA13-B05 IPCA+ estimado e projetado | OK |
| QA13-B06 Importação parada e CDI projetado no limite | OK |
| QA13-B07 Data padrão resolvida duas vezes | OK (ressalva da D-5, só registro) |
| Extras (títulos sem aporte, aportes futuros, `PUT`, `DELETE`) | OK |

## QA13-B00 — Carteira vazia e carga da carteira: OK

Feito: `GET /api/portfolio/summary` e `?referenceDate=2026-11-30` nas duas APIs com o banco só com
esquema e mercado; `compara.mjs`; depois `carteira.sql` por `psql`.

Resposta da fase, carteira vazia (as duas datas):

```json
{"statusCode":200,"success":true,"message":null,"data":{"totalInvestedAmount":0,"totalGrossAmount":0,"totalGrossYield":0,"totalTaxAmount":0,"totalIofAmount":0,"totalNetAmount":0,"positionCount":0,"byAssetType":[],"referenceDate":"2026-11-27","positions":[],"isProjected":false,"totalNetYield":0,"byAsset":[]}}
```

- `totalNetYield` sai como `0` (e não `0.00`), igual aos outros totais da raiz da mesma resposta e da
  `main`. Ver RISCO R2.
- `compara.mjs`: `rotas 2, divergentes 0`; chave nova na raiz exatamente `totalNetYield,byAsset`.
- Depois da carga: 355 aportes e 101 títulos nas duas APIs, o mesmo que a foto do Compose.

## QA13-B01 — Só aditivo, `main` contra fase: OK

Feito: `urls.mjs` sobre a carteira copiada, com as datas D = 2026-11-27, T = 2026-11-27, 2026-12-31,
2028-01-03 e 2050-12-30, mais as 13 rotas de `fixos.txt` (erros, 404, `/api/foo`, índices): **1793
rotas** (resumo sem data e em 5 datas; cada um dos 101 títulos em detalhe e projeção em 6 datas; cada
um dos 355 aportes em 3 datas). `node compara.mjs $BM $BF < urls.txt`:

```
rotas 1793, divergentes 0
chaves novas por rota e caminho:
   /api/portfolio/summary $.data -> totalNetYield,byAsset
   /api/portfolio/summary $.data.positions[].projection -> netYield,nextTaxRate,nextTaxRateDate,iofFreeDate
   /api/assets/{id}/projection $.data -> totalNetYield
   /api/assets/{id}/projection $.data.positions[] -> netYield,nextTaxRate,nextTaxRateDate,iofFreeDate
   /api/positions/{id}/projection $.data -> netYield,nextTaxRate,nextTaxRateDate,iofFreeDate
```

- Mesmo status em todas as rotas (amostra de 315: 305 em 200/200, 7 em 400/400, 3 em 404/404; as
  mensagens de erro são idênticas no texto, por exemplo `"O valor do campo 'referenceDate' é
  inválido."` e `"Título com id 0000… não encontrado."`, `data` nulo).
- Chaves novas exatamente as da lista esperada, sempre depois das antigas; nada novo em
  `/api/assets`, `/api/assets/{id}`, `/api/positions` e `/api/indexes/...`.
- **Escala do `taxRate`: nenhuma diferença.** Contagem do texto em 4 respostas de resumo e de
  projeção: `main` e fase com os mesmos números (`22.5`: 662; `0`: 1059; `20.0`: 210; `17.5`: 447;
  `15.0`: 614), ou seja, a tabela antiga já escrevia `20.0`, `17.5` e `15.0`. Zero linhas
  `taxRate: main 20 x fase 20.0`.
- `inv13.mjs` sobre a carteira real copiada (355 aportes, 87 títulos com aporte; os outros 14 sem
  aporte), em 6 datas (sem data, 2026-11-30, 2026-12-31, 2027-05-16, 2028-01-03, 2030-01-02,
  2050-12-30): **0 itens divergentes em todas**, raiz fechando (`totalNetYield` = líquido − investido =
  soma dos itens), `byAsset` na ordem da primeira aparição, 87 títulos com 0 divergentes, `byAssetType`
  consistente. Motivos de `null` observados: "isento", "15%" e "vencimento", os três em várias datas
  (por exemplo 2030-01-02: 64 vencimento, 163 em 15%, 125 isentos). Em 2026-11-27 há 20 itens com
  `iofFreeDate`; 2026-11-30, 13; 2026-12-31, 2.

## QA13-B02 — Swagger, `main` contra fase: OK

`node swdiff.mjs sw-main.json sw-fase.json`:

```
novos em AssetProjectionResponse -> totalNetYield:number/double
novos em PortfolioSummaryResponse -> totalNetYield:number/double, byAsset:array?<#/components/schemas/AssetSummaryResponse>
novos em PositionProjectionResponse -> netYield:number/double, nextTaxRate:number/double?, nextTaxRateDate:string/date?, iofFreeDate:string/date?
schema novo AssetSummaryResponse -> assetId:string/uuid, positionCount:integer/int32, totalInvestedAmount:number/double, totalGrossAmount:number/double, totalGrossYield:number/double, totalTaxAmount:number/double, totalIofAmount:number/double, totalNetAmount:number/double, totalNetYield:number/double
rotas novas: nenhuma
nenhuma rota, schema ou propriedade antiga alterada
```

Igual à lista esperada (critério 5 do ROADMAP).

## QA13-B03 — Muitos títulos e um título com muitos aportes: OK

`carga.mjs` contra a fase: 150 títulos `QA-F13B T001..T150` (300 aportes) e `QA-F13B MUITOS CDI` com
252 aportes. `erros 0` (nenhuma criação recusada). No banco: 158 títulos `QA-F13B` e 552 aportes
neste momento da carga (os 150 + MUITOS + 300 + 252).

Resultado, sobre 907 itens e 238 títulos em `byAsset`:

| Data | `inv13` | `coerencia13` (40 posições conferidas) |
|---|---|---|
| sem data (D = 2026-11-27) | itens 907, 0 divergentes; raiz OK; `byAsset` 238 títulos, 0 divergentes | 0 títulos e 0 itens divergentes, 0 outra `referenceDate`, 0 totais de mesmo valor e texto diferente, 0 faltando |
| 2026-11-30 | itens 901, 0 divergentes; raiz OK | idem |
| 2030-01-02 | itens 904, 0 divergentes; raiz OK (os 15 vencidos 2026-12-10 entram como "vencimento") | idem |

- Hash MD5 da ordem de `byAsset` nas 5 chamadas idênticas: `533a744073ced5a154d9195b91176b87` nas cinco
  (238 itens). Ordem estável, sem RISCO de "pisca".
- `GET /api/assets/{MUITOS}/projection`: `"positionCount":252`, `"totalNetYield":4553.13`, igual à soma
  dos aportes aceitos e fechando com `inv13` no resumo (valores de 100,37 sem divergência de soma).
- `compara.mjs` extra sobre a carteira crescida: resumo em 3 datas e projeção do título MUITOS em 2
  datas, 5 rotas, 0 divergentes contra a `main`.

## QA13-B04 — Desempenho do consolidado com `byAsset`: OK com RISCO (baixa)

Carteira medida: 907 itens (355 reais + 552 `QA-F13B`), 238 títulos em `byAsset`. As duas APIs no mesmo
banco, na mesma máquina, com a pilha da Parte A rodando em paralelo (há ruído; por isso repeti em ordem
alternada).

Medianas (s):

| Rota | Execução 1 (roteiro, 5 medições) main / fase | Execução 2 (9 rodadas, main primeiro) main / fase | Execução 3 (11 rodadas, fase primeiro) main / fase |
|---|---|---|---|
| resumo sem data | 0,074 / 0,077 (+4%) | 0,072 / 0,073 (+1%) | — |
| resumo 2026-11-30 | 0,072 / 0,077 (+7%) | — | — |
| resumo 2030-01-02 | 1,546 / 2,022 (+31%) | 1,776 / 2,240 (+26%) | 1,555 / 1,761 (+13%) |
| resumo 2050-12-30 | 1,883 / 2,185 (+16%) | 1,808 / 1,969 (+9%) | 1,669 / 1,954 (+17%) |
| título MUITOS sem data | 0,044 / 0,051 (+16%) | — | — |
| título MUITOS 2050-12-30 | 0,410 / 0,465 (+13%) | 0,471 / 0,502 (+7%) | — |

- Tamanho do resumo sem data: `main` 555.071 bytes, fase 686.894 bytes: **+131.823 bytes (+23,8%)**, cerca
  de **145 bytes por item** (907 itens: os quatro campos novos por item mais `totalNetYield` e `byAsset`
  com 238 títulos). Em 2030-01-02: 555.213 contra 683.883. (O `%{size_download}` do `curl` do Git Bash
  devolveu 0 com `-o`; o tamanho foi medido com `wc -c`.)
- Concorrência: 200 chamadas com 20 em paralelo ao resumo sem data, todas 200; mais 40 chamadas com 10
  em paralelo a `?referenceDate=2050-12-30`, todas 200. `docker logs`: **0 exceções** (`exception|fail:`) em
  `qaf13-fase` e em `qaf13-main`.
- Risco 11 da F12 (resumo de 262 posições em 2050-12-30: 2,7 a 3,0 s): aqui o resumo de 907 posições na
  mesma data leva 1,7 a 2,2 s (na fase) e 1,6 a 1,9 s (na `main`). A máquina é outra e o risco 11 da F12 não
  piorou na fase em termos absolutos; a fase continua dentro do mesmo patamar da `main` mais um custo de
  10% a 30% que só aparece onde o cálculo é grande.

Classificação: fase de 9% a 31% mais lenta que a `main` nas linhas pesadas (as leves, sem projeção
grande, ficam em 1% a 7%). A regra do roteiro manda **RISCO (baixa)** para 10% a 100%, e BUG (média) só
acima de 2x. Variância alta entre as execuções (o mesmo resumo de 2030 deu +13%, +26% e +31%), então o
valor firme é "entre 10% e 30%". Ver R1.

## QA13-B05 — IPCA+ estimado e projetado juntos: OK

`restaura; tira Ipca 2026-10-01`: as duas APIs passaram a terminar o IPCA em 2026-09-01 (conferido por
`GET /api/indexes/Ipca?from=2026-01-01&to=2026-12-31` nas duas, sem precisar reiniciar contêiner; sem
cache). Títulos `QA-F13B IPCA6` (aportes 2026-09-01 e 2026-11-03) e `QA-F13B TESOURO IPCA6` (aporte
2026-11-16). `linha.mjs` nas datas T (2026-11-27), 2026-12-15 e 2027-03-31:

```
BP1 2026-11-27 d=87  proj=true est=true  taxRate=22.5 next=20.0/2027-03-01 iofRate=0  iofFree=null       netYield=26.23 OK
BP1 2026-12-15 d=105 proj=true est=true  taxRate=22.5 next=20.0/2027-03-01 ...                           netYield=30.12 OK
BP1 2027-03-31 d=211 proj=true est=true  taxRate=20.0 next=17.5/2027-08-28 ...                           netYield=55.83 OK
BP2 2026-11-27 d=24  proj=true est=false taxRate=22.5 next=20.0/2027-05-03 iofRate=20 iofFree=2026-12-03 netYield=4.50  OK
BP3 2026-11-27 d=11  proj=true est=false taxRate=22.5 next=20.0/2027-05-16 iofRate=63 iofFree=2026-12-16 netYield=0.98  OK
(demais datas OK nas três posições)
```

- `isEstimated` e `isProjected` verdadeiros juntos no `BP1`; datas novas as da regra, iguais com e sem
  estimativa.
- `compara.mjs` em 5 rotas (resumo sem data e em 2026-12-15, projeção do título, projeção do título do
  Tesouro em 2027-03-31, posição `BP1` em 2026-12-15): **0 divergentes**, campos antigos idênticos.
- `restaura` no fim.

## QA13-B06 — Importação parada e CDI projetado no limite: OK

Título `QA-F13B CDI100` (aporte 2026-11-16). `cdi_ate 2026-11-21` removeu 4 linhas (11-23 a 11-26), o que
deixou L = 2026-11-19 (2026-11-20 é feriado). As duas APIs viram a remoção.

- Quatro rotas (`summary?referenceDate=2026-12-31`, projeção do título e da posição na mesma data, e o
  resumo sem data): **0 divergentes** entre `main` e fase. Status 400 nas três com data futura, mesma
  mensagem nas duas APIs e `data: null`, sem `byAsset` parcial:

  ```
  {"statusCode":400,"success":false,"message":"Posição d29ecd96-… (título 'QA-F13B T119'): Cotação de CDI ausente para 2026-11-24. A importação de cotações pode estar desligada (IndexRatesImport:Enabled) ou ainda não ter rodado.","data":null}
  ```

  O resumo sem data segue 200 nas duas.
- `restaura` (L = P = 2026-11-26, `sintetico` não foi necessário): `compara` das 4 rotas, 0 divergentes;
  resumo de 2026-12-31 em 200 com `isProjected` verdadeiro, `inv13` com 241 títulos e 0 divergentes, raiz
  OK. Posição `BP4` em 2026-12-31: `isProjected: true`, `isEstimated: false`, `netYield 12.29`,
  `nextTaxRate 20.0`, `nextTaxRateDate 2027-05-16`, `iofFreeDate null`.
- `restaura` no fim.

## QA13-B07 — Data padrão resolvida duas vezes (ressalva da D-5): OK (só registro)

`restaura`; resumo sem data: `referenceDate` D1 = 2026-11-27. `sintetico 2026-11-27 0.050788` (simula a
importação rodando entre as duas chamadas do front). Depois:

- projeção por título do `QA-F13B CDI100` sem data: `"referenceDate":"2026-11-30"`, `totalGrossAmount 1004.58`;
- `byAsset` do resumo guardado: raiz com `2026-11-27`, `totalGrossAmount 1004.07`, `totalNetYield 1.17`;
- resumo refeito sem data: `referenceDate 2026-11-30`, `totalNetYield 1.67` (igual à projeção por título); e
  com `?referenceDate=2026-11-27`, os valores do resumo antigo.

Todas as respostas trazem a `referenceDate` que de fato usaram. É a ressalva aceita pela D-5.
`restaura` no fim.

## Extras (fora do roteiro, dentro do espírito da B03 e da A12/A13 na pilha isolada)

Títulos `QA-F13B SEM APORTE`, `TODOS DEPOIS` (aportes 2026-11-20 e 2026-11-24), `FUTURO` (aporte
2026-12-15) e `LCA FUTURA` (aporte 2026-12-15):

- `byAsset` em cada data: SEM sempre fora; DEP fora em 2026-11-19, com 1 em 2026-11-20 e com 2 de
  2026-11-24 em diante e sem data; FUT e LCAF fora até 2026-11-27 com data explícita e presentes sem
  data (item projetado na própria data 2026-12-15, `d` = 0, `netYield 0.00`, FUT com
  `nextTaxRate 20.0 / 2027-06-14` e `iofFreeDate 2027-01-14`; LCA com os três `null`).
- Projeção por título de SEM: 200, zeros, `positions []`, `totalNetYield 0`.
- `compara.mjs` em 10 rotas desses títulos: 0 divergentes. `coerencia13` e `inv13`: 0 divergências em
  sem data, 2026-11-24 e 2026-12-15.
- `PUT` de taxa em DEP (aceito) e de vencimento em SEM (aceito, sem aportes), aporte novo em SEM: em
  2026-11-27 `nextTaxRate`, `nextTaxRateDate` e `iofFreeDate` nulos (2027-05-24 e 2026-12-24 cairiam depois
  do vencimento 2026-12-20); `compara` e `coerencia13` sem divergência depois do `PUT`.
- `DELETE` de título sem aporte: 204, depois 404 igual nas duas APIs; `DELETE` de DEP (com aporte): 400
  `"Título possui aportes e não pode ser removido."`.

## RISCOs

### R1 — Consolidado com datas distantes é de 9% a 31% mais lento que na `main` (baixa)

- Rota: `GET http://localhost:28113/api/portfolio/summary?referenceDate=2030-01-02` contra
  `http://localhost:28114/...` com os 907 itens da carteira da B03.
- Obtido: medianas fase 1,76 a 2,24 s contra `main` 1,55 a 1,78 s (+13%, +26%, +31% em três execuções);
  2050-12-30: 1,95 a 2,19 s contra 1,67 a 1,88 s (+9% a +17%). Resposta 23,8% maior (+145 bytes por item).
- Esperado: até 10% (mais 30 ms), pelo bom senso de que a F13 só agrupa e soma o que já estava calculado.
- Fonte: roteiro B04 e a faixa "entre 10% e 100%" é RISCO (baixa); o risco 11 da F12 continua aberto.
- Sem impacto na correção; sem 500 sob concorrência (240 chamadas paralelas, 0 exceções).

### R2 — Formatação do zero e significado de `null` (baixa)

- `0` contra `0.00`: carteira vazia devolve `"totalNetYield":0` (e todos os totais da raiz como `0`),
  enquanto itens com valor zero saem como `"netYield":0.00`. No mesmo item de `d` = 0 convivem
  `"taxAmount":0`, `"iofAmount":0` e `"netYield":0.00` (os dois primeiros já são assim na `main`). Nenhum
  zero negativo e nenhuma notação exponencial nas respostas do resumo.
- `null` em `nextTaxRate`/`nextTaxRateDate` com quatro significados (LCI/LCA, já em 15%, faixa depois do
  vencimento, título vencido): contagem real na carteira em 2030-01-02: 125 isentos, 163 em 15% e 64 por
  vencimento. O README descreve três deles e diz "quando a data cairia depois do vencimento" sem citar
  explicitamente o título já vencido. O front só separa os casos olhando tipo, `taxRate` e vencimento do
  título.
- Fonte: decisão 5 do roteiro (RISCO, baixa).

## Observações sem severidade

- No `summary?referenceDate=2051-01-02`, as duas APIs respondem 200 (calendário não é consultado com a
  carteira copiada); comportamento idêntico na `main`, não é da F13. Os 400 do calendário da projeção
  de posição ficam com a Parte A (A09).
- Itens do consolidado sem data com aporte posterior à data padrão trazem `referenceDate` do item igual ao
  dia do aporte, com a raiz em D: conforme RN-14, igual em `byAsset`, projeção por título e posição.
- `curl -w '%{size_download}'` com `-o` não funciona neste Git Bash (devolve 0): para a B04 o tamanho
  foi medido com `wc -c`.

## Bugs

Nenhum.

## Dados criados durante o QA

Todos ficam **só em `qaf13-db`** (o banco do Compose não recebeu nenhuma escrita desta parte) e somem com
o contêiner. Prefixo `QA-F13B`; dentro do banco: 158 títulos e 561 aportes `QA-F13B` no fim (o `DELETE`
apagou o `REMOVIVEL`).

- Carga da B03: 150 títulos `QA-F13B T001..T150` e 300 aportes (ids em `…/qaf13/b/carga.txt`);
  `QA-F13B MUITOS CDI` = `f0101e8e-c997-42b7-9088-a75dc0a75276` com 252 aportes.
- B05: `QA-F13B IPCA6` `f1aaf0e5-df03-48dd-b284-97a6fdb56a33` (aportes `3186d07b-0a30-4088-a8a3-0cf589b298be`,
  `f844cb40-aaf6-4854-bb9c-9844760197f5`); `QA-F13B TESOURO IPCA6` `8d58e5b8-fc11-43ae-bab2-9ada1fa78e29`
  (aporte `44d2df6f-0b62-42af-b2a4-91e55ccb2ef1`).
- B06 e B07: `QA-F13B CDI100` `e7270cae-a8b8-492f-8271-b5ff57c1313b` (aporte `8bfc590d-6bad-4e9f-b1b6-88ee80a21ce4`).
- Extras: `QA-F13B SEM APORTE` `7f0376d6-40cf-47b9-99f9-5188b1b59b8a` (aporte
  `d211dc73-4ce8-43a0-a0c3-ef70592b2fee`); `QA-F13B TODOS DEPOIS` `caf8fdc2-cfd2-4ac9-847d-a05915ed6854`
  (aportes `977b3066-dfdd-4c18-a25b-d5baba0fce1c`, `4210d57a-826b-4aa1-b845-36ba78426332`);
  `QA-F13B FUTURO` `4c27c457-2634-4ec4-a564-8ffb16e91be8` (aporte `5b3164d6-f59a-48c0-b29b-c32b20ab1682`);
  `QA-F13B LCA FUTURA` `20f67273-aecf-4617-800e-d9d490833a6e` (aporte `1e08ffdf-275f-479c-8804-3ec268cd39a9`);
  `QA-F13B REMOVIVEL` `5a37d530-ba35-4612-ae71-46df76072df4` (removido pelo `DELETE`).
- A carteira copiada do Compose (101 títulos, 355 aportes) também ficou só em `qaf13-db`.

## Restauração das cotações

Último `restaura` ao fim (20h44 em Brasília): saída `cotações restauradas`; `index_rate` de `qaf13-db`
com Cdi 1732 até 2026-11-26 e Ipca 82 até 2026-10-01, igual a `base-index-rate.txt`; 1814 linhas em
`index_rate` e 1814 em `qa_index_rate_bkp`. As duas APIs mostram a última cotação de CDI em 2026-11-26.
Os contêineres e imagens não foram limpos (a limpeza é do coordenador).
