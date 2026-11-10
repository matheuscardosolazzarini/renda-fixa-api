# QA exploratório — F7: Indexadores reais e dias úteis

## Cabeçalho

| Item | Valor |
|---|---|
| Data | 2026-10-30, entre 22h20 e 22h50 em Brasília (2026-10-31, 01h20 a 01h50 UTC) |
| Commit | `bd85c61` (branch `f7-indexadores`; árvore com alterações só em `docs/` e `.claude/`) |
| Ambiente | API em Docker (`fixedincome-api`, porta 8080, `Development`) e PostgreSQL 16 (`fixedincome-db`, porta 5432), preparados pelo orquestrador. Migrations `InitialCreate` e `AddHolidaysAndIndexRates` aplicadas |
| Dados de mercado | 139 feriados, 1714 cotações de CDI de 2020-01-02 a 2026-10-29 e 81 de IPCA de 2020-01 a 2026-09. **D = 2026-10-29**, e o dia útil seguinte (data padrão) é 2026-10-30 |
| Método | Caixa-preta: `curl` contra `http://localhost:8080`. O código de `src/` só foi aberto depois de reproduzir o risco de QA-05 e para explicar o `500` do achado B-01. Todo valor financeiro foi conferido por um modelo independente em Node (`calc.js`, no scratchpad da sessão: calendário lido da tabela `holiday`, cotações da tabela `index_rate`, regras RN-01 a RN-11 da especificação), nunca pelo número devolvido |
| Fora desta parte | QA-08 e QA-09 (derrubam a rede e esvaziam as cotações): ficam para a Parte B |
| Escritas no banco | Nenhuma em `index_rate` nem em `holiday` (continuam com 1795 e 139 linhas). Foram apagadas 6 posições `QA-` que eu mesmo criei, por id, para destravar o resumo (ver "Dados criados") |

Veredito de severidade fica com o orquestrador. Nos achados, "sugestão" é só uma indicação.

## Parte A — QA-01 a QA-07, QA-10, QA-11

### QA-01 — Contrato e documentação: **BUG** (baixa) e **RISCO**

O que testei e o resultado:

- **Swagger** (`GET /swagger/v1/swagger.json`, HTTP 200): 10 operações, as mesmas que o README anuncia
  ("Dez endpoints"): `POST/GET /api/Assets`, `GET/PUT/DELETE /api/Assets/{id}`, `POST/GET /api/Positions`,
  `GET /api/Positions/{id}/projection`, `GET /api/Portfolio/summary` e `GET /api/Indexes/{indexType}`
  (com `from`/`to` obrigatórios e o enum `IndexType` com `PreFixed|Cdi|Ipca`). OK.
- **`FixedIncome.Api.http`**, todas as requisições na ordem, apontando para a porta 8080 em vez da 5124
  (com nomes `QA-01 ...`): criar título 201; listar 200; detalhar 200; atualizar 200; registrar aporte 201;
  listar aportes 200; projeção (`referenceDate=2026-06-01`) 200 com `grossAmount 1129.45`, `taxAmount 22.65`,
  `netAmount 1106.80` (confere: 1000 × 1,13^(251/252) = 1129,45; IR 17,5% de 129,45 = 22,65); remover título com
  aporte 400 ("Título possui aportes e não pode ser removido."); resumo 200; cotações de CDI e IPCA 200;
  `PreFixed` 400 ("Só há cotação para CDI e IPCA."); CDB CDI 100% com projeção em 2026-09-23 200
  (`grossAmount 1042.68`, `businessDays 80`); CDB IPCA+6% com projeção em 2026-07-01 200 (`grossAmount 1028.56`,
  `isEstimated false`). Os dois últimos batem com o modelo independente. OK.
- **Exemplo do README**: `GET /api/indexes/Cdi?from=2026-09-14&to=2026-09-15` devolve exatamente o corpo documentado
  (`{"statusCode":200,"success":true,"message":null,"data":[{"indexType":"Cdi","referenceDate":"2026-09-14","value":0.051660},...]}`). OK.
- **Enums como texto**: `assetType: "Cdb"`, `indexType: "PreFixed"` nas respostas; no corpo, número (`"assetType":0`) é rejeitado
  com 400 e minúsculas (`"cdb"`, `"prefixed"`) são aceitas e normalizadas (201, devolve `"Cdb"`).
- **Formato `ApiResponse` nos erros**: 400 de GUID inválido, 404 de título inexistente, 400 de JSON malformado, de corpo
  vazio, de corpo `null`, de campos obrigatórios e de enum inválido seguem `{statusCode, success, message, data}`. OK.
  Exceções: ver B-06.

Achados deste roteiro: B-04 (README, migration e primeira importação), B-06 (415, 404 e 405 fora do envelope), B-07 (eco do POST),
B-09 (`DELETE` bem-sucedido loga exceção), R-03 (porta 5124 do `.http` contra 8080 do README), R-05 (`201` sem `Location`).

### QA-02 — Entradas inválidas em `/api/indexes`: **OK** (com observações)

Todas as requisições abaixo foram `GET http://localhost:8080/api/indexes/...`:

| Requisição | Resposta |
|---|---|
| `Cdi?from=2026-10-10&to=2026-10-01` | 400 "A data inicial não pode ser posterior à data final." |
| `Cdi?to=2026-10-01` | 400 "O campo 'from' é obrigatório." |
| `Cdi?from=2026-10-01` | 400 "O campo 'to' é obrigatório." |
| `Cdi` e `Cdi?from=&to=` | 400 "O campo 'to' é obrigatório. O campo 'from' é obrigatório." |
| `from=2026-13-01`, `to=2026-13-01`, `from=2026-02-30` | 400 "O valor do campo 'from' (ou 'to') é inválido." |
| `from=31/12/2026`, `to=ontem` | 400 "O valor do campo ... é inválido." |
| `PreFixed?...` | 400 "Só há cotação para CDI e IPCA." |
| `Selic`, `99`, `-1`, `Cdi,Ipca` | 400 "O valor do campo 'indexType' é inválido." |
| `0?...` | 400 "Só há cotação para CDI e IPCA." (0 é PreFixed) |
| `cdi?from=2026-09-14&to=2026-09-14` (minúsculo) | **200** com a cotação de CDI |
| `1?from=2026-09-14&to=2026-09-14` (número) | **200** com a cotação de CDI |
| `Cdi?from=1990-01-01&to=2100-12-31` | 200, 1714 cotações, **16 ms** |
| `Ipca?from=1990-01-01&to=2100-12-31` | 200, 81 cotações, 9 ms |
| `Cdi?from=0001-01-01&to=9999-12-31` | 200, 15 ms |
| `Cdi?from=2026-09-12&to=2026-09-13` (fim de semana) | 200, `data: []` |
| `Ipca?from=2026-01-15&to=2026-02-15` | 200, só fevereiro (janeiro fica de fora, como o README documenta) |
| `from=2026-9-1&to=2026-9-5` e `from=2026-09-14T00:00:00` | 200 (parser tolerante) |

Nenhum 500. Observações sem veredito de bug: letras minúsculas e o número do enum na rota são aceitos (o corpo de `POST /api/assets` rejeita
número, ver B-08), e o roteiro listava `cdi` como entrada a rejeitar. O período enorme é barato porque os dados são limitados
(cerca de 250 cotações de CDI por ano, sem paginação); só vira risco se a série crescer muito.

### QA-03 — Datas de borda na projeção: **OK** nos valores, **BUG** na carteira (B-02) e **BUG** de mensagem (B-05)

Títulos `QA-03`: CDB PreFixed 10% e CDB 100% do CDI. Cada projeção foi comparada com o modelo independente (bruto, IOF, IR,
líquido, dias úteis e dias corridos): todas iguais.

| Caso | PreFixed 10% | CDI 100% |
|---|---|---|
| Aporte 2026-09-07 (feriado), ref 2026-10-15 | `gross 1009.88`, DU 26, 38 dias | `gross 1013.35`, DU 26 |
| Aporte 2026-09-12 (sábado), ref 2026-10-15 | `gross 1008.36`, DU 22, 33 dias | `gross 1011.26`, DU 22 |
| Ref domingo 2026-10-11 (aporte 2026-09-01) | `gross 1010.65`, DU 28 | `gross 1014.42`, DU 28 |
| Ref domingo 2026-09-20 (aporte 2026-09-01) | `gross 1004.93`, `iofRate 36`, `iof 1.77` | `gross 1006.72`, `iof 2.42` |
| Ref = aporte (2026-09-14 e 2026-09-12) | `gross 1000.00`, `yield 0.00`, IOF 0, IR 0, `elapsedDays 0` | idem |
| Ref anterior ao aporte | 400 "A data de referência não pode ser anterior à data de aporte." | idem |
| Vence 2026-09-30, ref 2026-10-20 / 2031-06-01 / 2035-06-01 | todas limitadas ao vencimento: `gross 1007.59`, 29 dias, `iofRate 3` (RN-02) | `gross 1010.30`, igual ao resultado da Calculadora do BC para 01/09 a 30/09 |
| Vence 2026-09-30, ref 9999-12-31 | 200 limitado ao vencimento | 200 idem |
| Ref 0001-01-01 | 400 "não pode ser anterior à data de aporte" | idem |

Fora do calendário (títulos `QA-03 ... 2019-2032`, emissão 2019, vencimento 2032):

| Requisição | Resposta |
|---|---|
| Aporte 2019-06-03, qualquer ref (2031-01-01, 2026-01-02, 2019-12-31, 2020-01-02) | 400 "Data 2019-06-03 anterior à cobertura do calendário, 2020-01-01." |
| Aporte 2020-01-02, ref 2031-06-01 | 400 "Data 2031-05-31 posterior à cobertura do calendário, 2030-12-31." |
| Aporte 2020-01-02, ref 2031-01-01 | 200 (`gross 2835.90`, DU 2756; confere com 1000 × 1,1^(2756/252)); o fim de 2030 é aceito porque a data final é excluída |
| Aporte 2030-12-30, ref 2031-01-01 | 200 PreFixed (`gross 1000.76`, `iofRate 93`); CDI: 400 "Cotação de CDI ausente para 2030-12-30." |
| Aporte 2030-12-30, ref 2031-01-02 | 400 "Data 2031-01-01 posterior à cobertura do calendário, 2030-12-31." |
| Aporte 2020-01-01 (feriado, primeira data do calendário), ref 2020-01-02 | 200, `yield 0` |
| Aporte 2020-01-01, sem `referenceDate` | 200, 1714 dias úteis (igual ao número de cotações de CDI) |
| CDI, aporte 2020-01-02, ref 2031-01-01 | 400 "Cotação de CDI ausente para 2026-10-30." (primeira data sem cotação, como a RN-09 manda) |

Nenhum 500. O pedido do roteiro (400 citando o período coberto) é atendido pela metade: ver B-05. O problema de verdade está fora da
projeção: o `POST /api/positions` aceitou o aporte de 2019 e, a partir daí, `GET /api/portfolio/summary` passou a devolver 400 para a
carteira inteira (B-02).

### QA-04 — IOF e tributação: **OK**

Quatro títulos PreFixed 12% a.a. (CDB, LCI, LCA e Tesouro Direto), aportes de R$ 1.000 em 2026-10-28, 2026-10-14, 2026-09-30 e
2026-09-29 (1, 15, 29 e 30 dias antes de D), projeção em 2026-10-29. Resultados (todos conferidos com o modelo):

| Tipo | Dias | `iofRate` | `grossAmount` | `iofAmount` | `taxAmount` | `netAmount` |
|---|---|---|---|---|---|---|
| CDB | 1 | 96 | 1000.45 | 0.43 | 0.00 | 1000.02 |
| CDB | 15 | 50 | 1004.96 | 2.48 | 0.56 | 1001.92 |
| CDB | 29 | 3 | 1009.03 | 0.27 | 1.97 | 1006.79 |
| CDB | 30 | 0 | 1009.49 | 0.00 | 2.14 | 1007.35 |
| LCI | 1 / 15 / 29 / 30 | 96 / 50 / 3 / 0 | 1000.45 / 1004.96 / 1009.03 / 1009.49 | 0.43 / 2.48 / 0.27 / 0.00 | 0 em todos | 1000.02 / 1002.48 / 1008.76 / 1009.49 |
| LCA | 1 / 15 / 29 / 30 | 0 em todos | idem LCI | 0.00 em todos | 0 em todos | igual ao bruto |
| Tesouro | 1 / 15 / 29 / 30 | 96 / 50 / 3 / 0 | idem CDB | idem CDB | idem CDB | idem CDB |

- `IofRate` segue a RN-11 (96, 50, 3, 0). LCA tem IOF zero. LCI tem IOF maior que zero até o dia 29 e IR zero.
- Varredura de identidades: para cada uma das posições do banco que a data de referência permitia projetar, conferi
  `netAmount = grossAmount − iofAmount − taxAmount` exato e comparei os cinco valores com o modelo independente: **0 divergências**
  em 45, depois em 261 projeções. O líquido do centavo vem da subtração de valores já arredondados, como o README documenta.
- Resumo da carteira: com 47 posições, `totalGrossAmount 51390.38`, `totalTaxAmount 687.10`, `totalIofAmount 12.15`,
  `totalNetAmount 50691.13`; 51390,38 − 687,10 − 12,15 = 50691,13. A soma dos `netAmount` individuais (48691,13) mais as 2 posições
  futuras (2000,00) é 50691,13. Repeti com 268 posições (R$ 821.000 investidos): bruto 920498,46 = soma individual 913498,46 + 7000, e
  o líquido fecha igual. A soma de `byAssetType[].totalNetAmount` também fecha com o líquido total.

### QA-05 — Data padrão e fuso horário: **OK** (data padrão) e **RISCO** (fuso)

**Data padrão.** Posição PreFixed de 2026-10-28, sem `referenceDate`: `elapsedDays 2`, `businessDays 2`, `iofRate 93`, o mesmo resultado
de `referenceDate=2026-10-30`. Ou seja, a data usada é o dia útil seguinte a D (2026-10-30), e `referenceDate=2026-10-31` dá
`elapsedDays 3`. O resumo sem data fecha com a soma das projeções em 2026-10-30. OK. Não foi possível testar o dia útil seguinte
quando D cai numa sexta ou antes de um feriado, porque D é fixo e não altero as tabelas de mercado.

**Fuso.** A sessão rodou na janela de risco: `docker exec fixedincome-api date -u` = `Sat Oct 31 01:25 UTC 2026`, enquanto o relógio de
Brasília marcava `Fri Oct 30 22:25`. Evidência direta, no log da API da importação feita nessa janela:

```
Sending HTTP request GET https://api.bcb.gov.br/dados/serie/bcdata.sgs.12/dados?formato=json&dataInicial=01/01/2020&dataFinal=31/10/2026
Sending HTTP request GET https://api.bcb.gov.br/dados/serie/bcdata.sgs.433/dados?formato=json&dataInicial=01/01/2020&dataFinal=31/10/2026
```

A janela do job usa `dataFinal=31/10/2026`, um dia à frente do dia de Brasília (30/10). Depois de ver isso, li o código: os três pontos
que usam `DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime)` são `ImportIndexRatesUseCase.cs:44`,
`GetPositionProjectionUseCase.cs:74` e `GetPortfolioSummaryUseCase.cs:103`. Efeitos:

1. **Job (reproduzido):** data final um dia no futuro. Hoje é inofensivo, porque o SGS devolve só o que já publicou e a importação é
   incremental (`último + 1` até "hoje"). Nenhum erro observado.
2. **Data padrão sem nenhuma cotação (não reproduzido):** o caminho `?? hoje UTC` só roda com a tabela `index_rate` vazia, que eu não podia
   esvaziar nesta parte. Entre 21h e 23h59 em Brasília, a data usada seria a de amanhã. Para PreFixed e IPCA+, que seguem calculando
   sem cotação, o rendimento sai de um dia a mais e o IOF cai uma faixa. Como verificar na Parte B, com a tabela vazia e dentro da janela
   (até cerca de 00h em Brasília, ou seja, 03h UTC): projetar sem data uma posição PreFixed de 2026-10-28; `elapsedDays 3` indica data
   UTC (2026-10-31), `elapsedDays 2` indica data de Brasília (2026-10-30).

Classificação: `RISCO` (impacto baixo, depende de tabela vazia). Sugestão de teste: `Importacao_entre_21h_e_meia_noite_em_brasilia_usa_a_data_de_brasilia`
(relógio stub em 2026-10-31T01:25Z, esperado `dataFinal` = 2026-10-30).

### QA-06 — Aporte recente: **OK** (resumo) e **BUG** (mensagem da projeção, B-03)

- Aportes com data depois de D em um título PreFixed: 2026-10-31 (sábado), 2026-11-01 e 2026-11-02, mais um em 2026-10-30 (a própria
  data padrão), e aportes de 2026-11-03 em títulos CDI e IPCA+.
- `GET /api/portfolio/summary` (sem data): **200** com todas as posições. Os aportes futuros entram com rendimento zero: o bruto sobe
  exatamente R$ 1.000 por posição, e IR e IOF não mudam (`totalGrossAmount 51390.38` → `55390.38` com 4 posições e → `57390.38` com
  mais 2 aportes, `totalTaxAmount 687.10` e `totalIofAmount 12.15` constantes). Não houve falha por falta de cotação para os aportes de CDI.
- `GET /api/positions/{id}/projection` sem data, aporte em 2026-10-31 (e em 11-01, 11-02, 11-03 de CDI e IPCA+): **400**
  `"A data de referência não pode ser anterior à data de aporte."`. O aporte de 2026-10-30 (igual à data padrão) dá 200, `elapsedDays 0`.
- Com `referenceDate` explícita igual à data do aporte futuro (2026-11-03): 200, rendimento zero. Com 2026-11-04 em CDI: 400
  `"Cotação de CDI ausente para 2026-11-03."`, correto pela RN-09.
- A mensagem do 400 sem data **não explica o motivo**: quem não informou `referenceDate` lê "a data de referência não pode ser anterior à
  data de aporte" sem saber que a API usou 2026-10-30 por conta própria (ver B-03).

### QA-07 — IPCA: **OK**

Todas as projeções conferidas com o modelo (fator IPCA por mês com pro rata em dias úteis e taxa real em DU/252).

| Caso | Resultado |
|---|---|
| IPCA+6%, aporte 2026-03-01, ref 2026-07-01 (mar a jun publicados) | `gross 1042.91`, `isEstimated false` |
| IPCA+6%, R$ 5.000, aporte 2026-01-05, ref 2026-09-01 | `gross 5354.94`, `isEstimated false` |
| Aporte 2026-09-01, ref 2026-10-01 (outubro não entra) | `gross 1013.11`, `isEstimated false` |
| Aporte 2026-09-01, ref 2026-10-02 (entra 1 dia útil de outubro, ainda sem IPCA) | `gross 1013.74`, **`isEstimated true`** |
| Aporte 2026-09-01, ref 2026-10-30 | `gross 1025.75`, `isEstimated true` |
| Aporte 2026-09-15, ref 2026-10-15 (meses parciais) | `gross 1013.11`, `isEstimated true` |
| Aporte 2026-10-01, ref 2026-10-29 (só outubro) | `gross 1011.85`, `isEstimated true`, `iofRate 6`, `iofAmount 0.71` |
| Aporte 2026-06-01, ref 2027-06-01 | `gross 1139.02`, `isEstimated true` |
| IPCA+0,5% CDB, aporte 2026-08-03, ref 2026-08-31 (deflação de −0,32%) | `gross 997.35`, `grossYield −2.65`, **`iofAmount 0`, `taxAmount 0`**, `netAmount 997.35` |
| Idem, ref 2026-08-05 | `grossYield −0.27`, `iofRate 93`, `iofAmount 0`, `taxAmount 0` |
| Idem, ref 2026-09-01 (29 dias, `iofRate 3`) | `grossYield −2.79`, IOF e IR zero |
| IPCA+0,5%, R$ 100.000, aporte 2026-08-01, ref 2026-08-31 | `grossYield −265.31`, IOF e IR zero |
| LCI e LCA IPCA+0,5%, mesmo período | rendimento −2.65, `iofAmount 0`, `taxAmount 0` |

Não executado neste roteiro: a RN-10 "mês anterior ao último publicado sem cotação é falha de importação" (projeção deve falhar
informando o mês). Exige um buraco em `index_rate`, e não posso alterar essa tabela nesta parte. `NÃO EXECUTADO` (cobre a Parte B ou um
teste de integração da F9).

### QA-10 — Volume: **OK**

Criei 12 títulos `QA-10` (CDB, LCI, LCA e Tesouro Direto × PreFixed, CDI e IPCA+) e 200 posições distribuídas entre eles (R$ 1.000 a
R$ 5.500, datas entre fevereiro de 2024 e outubro de 2026). Criação das 200 posições: 37 s (`POST` sequencial, 185 ms cada em média).

| Medida | Resultado |
|---|---|
| `GET /api/portfolio/summary` com 68 posições (antes do volume) | 24 a 26 ms |
| `GET /api/portfolio/summary` com 268 posições | **28 a 37 ms** (limite do roteiro: 2 s) |
| Projeção individual | 15 ms |
| 50 resumos simultâneos | todos 200, o mais lento levou 0,23 s |
| Consultas SQL por resumo e por projeção | 8 em cada (não cresce com o número de posições; sem N+1) |

Correção do resumo com 268 posições: 261 projeções individuais batem com o modelo independente e os totais batem com a soma das parcelas
(ver QA-04).

### QA-11 — Conferência externa num período novo: **OK**

Calculadora do Cidadão do BC, `POST https://www3.bcb.gov.br/CALCIDADAO/publico/corrigirPeloCDI.do?method=corrigirPeloCDI` com
`aba=5`, `dataInicial`, `dataFinal`, `valorCorrecao`, `percentualCorrecao`.

Caso pedido: CDB 105% do CDI, R$ 5.000,00, aplicação 2025-03-10, referência 2025-06-10 (63 dias úteis).

- API: `grossAmount 5177.40` (`grossYield 177.40`, `taxAmount 39.92`, `netAmount 5137.48`).
- Calculadora: índice de correção 1,03548002 e valor corrigido **R$ 5.177,40**.
- Diferença: **R$ 0,00** (limite: R$ 0,05 sobre R$ 5.000).

Seis casos adicionais, todos com diferença **R$ 0,00**:

| Aplicação → referência | Valor | % do CDI | API | Calculadora |
|---|---|---|---|---|
| 2025-12-15 → 2026-01-20 (virada de ano) | 1.000 | 100 | 1013.32 | 1.013,32 |
| 2025-02-10 → 2025-03-10 (Carnaval) | 10.000 | 100 | 10088.64 | 10.088,64 |
| 2024-01-02 → 2026-01-02 | 20.000 | 90 | 24757.71 | 24.757,71 |
| 2026-04-01 → 2026-10-29 | 3.000 | 120 | 3286.48 | 3.286,48 |
| 2025-06-02 → 2025-06-03 (1 dia útil) | 1.000 | 100 | 1000.54 | 1.000,54 |
| 2023-03-10 → 2025-03-10 | 1.000 | 100 | 1250.20 | 1.250,20 |

## Achados

Ordem por impacto percebido; a classificação final é do orquestrador.

### B-01 — `500` ao gravar valores que não cabem nas colunas numéricas (sugestão: alta)

`POST /api/assets` e `PUT /api/assets/{id}` com `rate` a partir de 1e12 (coluna `numeric(18,6)`), e `POST /api/positions` com
`investedAmount` a partir de 1e16 (coluna `numeric(18,2)`), passam pela validação do domínio e estouram no banco.

Passos:

```
POST http://localhost:8080/api/assets
{"name":"QA-X bigrate2","issuer":"x","assetType":"Cdb","indexType":"PreFixed","rate":1e15,"issueDate":"2026-01-01","maturityDate":"2027-01-01"}
```

```
POST http://localhost:8080/api/positions
{"assetId":"<id de um título QA->","investedAmount":1e16,"applicationDate":"2026-10-01"}
```

```
PUT http://localhost:8080/api/assets/<id>
{"name":"QA-12 put2","issuer":"y","rate":1e15,"issueDate":"2026-01-01","maturityDate":"2028-01-01"}
```

- Esperado: 400 com mensagem útil ("Rate deve ser menor que ..."), como acontece com `rate: -1` e com `investedAmount: 1e30`
  (este último já devolve 400 por não caber em `decimal`).
- Obtido: **HTTP 500** `{"statusCode":500,"success":false,"message":"Ocorreu um erro interno. Tente novamente mais tarde.","data":null}`;
  o log registra `DbUpdateException ... 22003: numeric field overflow` no `ExceptionHandlingMiddleware`.
- Limites que passaram: `rate 999999999999` (201) e `investedAmount 99999999999999.99` (201).
- Testes sugeridos: `Cadastrar_titulo_com_taxa_acima_do_limite_da_coluna_retorna_400`,
  `Atualizar_titulo_com_taxa_acima_do_limite_da_coluna_retorna_400`, `Registrar_aporte_com_valor_acima_do_limite_da_coluna_retorna_400`.

### B-02 — Um aporte fora do calendário (2020 a 2030) derruba o resumo da carteira inteira, sem como corrigir pela API (sugestão: alta ou média)

O `POST /api/positions` aceita uma data que depois nenhuma projeção consegue calcular. O resumo é tudo ou nada, e a API não tem
`DELETE` de aporte (decisão do README), então a carteira só se recupera mexendo no banco.

Passos:

```
POST http://localhost:8080/api/assets
{"name":"QA-03 PRE10 2019-2032","issuer":"QA Issuer","assetType":"Cdb","indexType":"PreFixed","rate":10,"issueDate":"2019-01-01","maturityDate":"2032-01-01"}

POST http://localhost:8080/api/positions
{"assetId":"<id acima>","investedAmount":1000,"applicationDate":"2019-06-03"}      -> 201

GET http://localhost:8080/api/portfolio/summary
```

- Esperado: o `POST /api/positions` rejeita com 400 dizendo que o calendário cobre 2020 a 2030, ou o resumo marca/ignora essa posição
  sem derrubar as outras (a RN-08 exige "erro, nunca suposição" para o cálculo, não prescreve que um item ruim apague a visão consolidada).
- Obtido: aporte 201; resumo 400 `"Data 2019-06-03 anterior à cobertura do calendário, 2020-01-01."` para todas as 31 posições da
  carteira. A mensagem não diz qual posição causou. O mesmo ocorre na outra ponta: aporte em 2031-06-01 (título com vencimento 2040)
  gera 400 `"Data 2031-06-01 posterior à cobertura do calendário, 2030-12-31."` no resumo sem data.
- Testes sugeridos: `Registrar_aporte_fora_do_calendario_retorna_400` (Application) e `Resumo_com_aporte_fora_do_calendario_nao_derruba_as_demais_posicoes`.

### B-03 — Mensagem da projeção individual sem data não explica o motivo (sugestão: baixa)

```
GET http://localhost:8080/api/positions/b4ef6757-a88d-40ba-95df-fc5ccb8bee43/projection
```

Posição de 2026-10-31, sem `referenceDate`. Esperado (roteiro QA-06): mensagem que explica por que não calcula, por exemplo que a data
padrão é 2026-10-30 (dia útil seguinte à última cotação de CDI) e é anterior ao aporte, e que o usuário deve informar `referenceDate`.
Obtido: 400 `"A data de referência não pode ser anterior à data de aporte."`, a mesma frase de quando o usuário digita a data errada.
Teste sugerido: `Projecao_sem_data_com_aporte_posterior_a_data_padrao_informa_a_data_padrao_usada`.

### B-04 — README: o caminho "Com Docker, sem instalar nada" não funciona como escrito (sugestão: baixa)

- `docker compose exec api dotnet ef database update` (README, "Com Docker") falha: a imagem é só runtime.
  Saída: `Could not execute because the specified command or file was not found ... The application 'ef' does not exist ... No .NET SDKs were found.`
- Além disso, a primeira importação das cotações roda ao subir a API, antes da migration. O log da sessão mostra
  `relation "index_rate" does not exist` e `Importação de cotações terminou com falha`. O README não diz que a API precisa ser reiniciada
  depois da migration; sem isso, as projeções de CDI e IPCA+ falham até a próxima execução do job (6 horas depois). O roteiro de ambiente
  precisou do `docker compose restart api`.
- Esperado: README com um comando que funcione e o passo de reiniciar (ou o job tentando de novo mais cedo depois de falha).
- Teste sugerido: não é de código; conferir o README num clone limpo.

### B-05 — Mensagens de calendário citam uma só ponta e a data errada (sugestão: baixa)

```
GET http://localhost:8080/api/positions/<aporte 2020-01-02>/projection?referenceDate=2031-06-01
```

Esperado (roteiro QA-03): mensagem que diga o período coberto, por exemplo "o calendário cobre 2020-01-01 a 2030-12-31".
Obtido: `"Data 2031-05-31 posterior à cobertura do calendário, 2030-12-31."` A data citada é a véspera da referência (2031-05-31), não a
que o usuário enviou (2031-06-01); com `referenceDate=2031-01-02` a mensagem diz 2031-01-01. E cada mensagem traz só um limite.
Teste sugerido: `Projecao_com_referencia_fora_do_calendario_informa_a_data_enviada_e_o_periodo_coberto`.

### B-06 — Respostas de erro fora do envelope `ApiResponse` (sugestão: baixa)

- `POST /api/assets` com corpo JSON e **sem** `Content-Type`: HTTP 415 com `ProblemDetails`
  (`{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.16","title":"Unsupported Media Type","status":415,"traceId":"..."}`).
- `GET /api/foo` (404) e `PATCH /api/assets` (405, com `Allow: GET, POST`): corpo vazio.
- O README afirma que o envelope é mantido nos erros de validação; estes casos escapam dele.
- Teste sugerido: `Requisicao_sem_content_type_retorna_resposta_no_formato_api_response`.

### B-07 — O eco do `POST` mostra valores que o banco não guarda (sugestão: baixa)

- `POST /api/assets` com `rate 12.123456789`: resposta 201 `"rate":12.123456789`; `GET /api/assets/{id}` depois devolve `12.123457`
  (coluna com 6 casas).
- `POST /api/positions` com `investedAmount 10.999`: resposta `10.999`; o banco guarda `11.00` (consultado por `psql`).
  Sem validação de casas decimais.
- Mesma origem: formatos diferentes para o mesmo valor (`rate:12` no POST e `rate:12.000000` no GET, `1000` e `1000.00`).
- Esperado: rejeitar mais casas do que a coluna guarda, ou ecoar o valor persistido.
- Teste sugerido: `Registrar_aporte_com_mais_de_duas_casas_decimais_retorna_400`.

### B-08 — Rota de cotações aceita o número do enum e rejeita o mesmo número no corpo (sugestão: baixa)

`GET /api/indexes/1?from=2026-09-14&to=2026-09-14` devolve as cotações de CDI (200), enquanto `"assetType":0` no corpo é 400, e
`/api/indexes/0` responde "Só há cotação para CDI e IPCA.". O README fala em texto. Documentar ou rejeitar número.
Teste sugerido: `Cotacoes_com_indexador_numerico_na_rota_retorna_400`.

### B-09 — Todo `DELETE /api/assets/{id}` bem-sucedido registra uma exceção no log (sugestão: baixa)

```
DELETE http://localhost:8080/api/assets/<id de título sem aportes>
```

O cliente recebe 204 correto, mas o log da API traz um `fail:` do `ExceptionHandlingMiddleware`:
`System.InvalidOperationException: Writing to the response body is invalid for responses with status code 204.` (o controller devolve
`StatusCode(204, response)` com corpo). Esperado: nenhum erro no log em caminho feliz (`NoContent()` ou `StatusCode(204)` sem corpo).
Teste sugerido: `Remover_titulo_sem_aportes_responde_204_sem_corpo` (teste de integração na F9).

## Riscos

- **R-01 (QA-05):** a data "hoje" vem do UTC. Reproduzido no job (`dataFinal=31/10/2026` às 22h25 de 30/10 em Brasília); o caminho com tabela vazia
  não foi reproduzido. Ver QA-05.
- **R-02:** `GET /api/portfolio/summary?referenceDate=<data>` com `referenceDate` explícita anterior ao aporte de qualquer posição devolve 400 para a
  carteira inteira ("A data de referência não pode ser anterior à data de aporte."). A spec só trata a data padrão, e a consulta "como estava em
  15/10" fica indisponível assim que existir um aporte mais novo. A decisão de ignorar ou rejeitar é de produto.
- **R-03:** o `.http` usa a porta 5124 (`dotnet run`) e o README manda abrir o Swagger na 8080 (Docker). Quem segue o README com Docker e abre o `.http`
  recebe conexão recusada (confirmado: a 5124 não respondia). Basta a variável do arquivo ou uma nota.
- **R-04:** `/health` responde 404 (a spec marca como F8; não é bug de F7).
- **R-05:** `201 Created` sem cabeçalho `Location`.
- **R-06:** `percentage` do `byAssetType` arredondado em duas casas: em uma amostra a soma deu 100,01 (76,20 + 7,94 + 7,94 + 7,93). Cosmético.

## Dados criados durante o QA

Todos os títulos têm o prefixo `QA-` no nome. No fim, o banco tem 50 títulos `QA-` (nenhum sem prefixo) e 279 posições.

| Grupo | Títulos | Posições | Observação |
|---|---|---|---|
| `QA-01 ...` | 5 | 3 | fluxo do `.http`, README e enums (`QA-01 CDB XYZ`, `... Prefixado`, `... 100% CDI`, `... IPCA+6%`, `QA-01 loc`, `QA-01 lower`) |
| `QA-03 ...` | 8 | 28 | borda de datas: `PRE10 feriado`, `PRE10 sabado`, `CDI100 feriado`, `CDI100 sabado`, `PRE10 vence 0930`, `CDI100 vence 0930`, `PRE10 2019-2032`, `CDI100 2019-2032`, `PRE 2031` |
| `QA-04 ...` | 4 | 16 | IOF e tributação (um por tipo, PreFixed 12%) |
| `QA-05 PRE12` | 1 | 4 | aportes em 2026-10-30, 10-31, 11-01 e 11-02 |
| `QA-06 ...` | 2 | 2 | CDI e IPCA+ com aporte em 2026-11-03 |
| `QA-07 ...` | 4 | 15 | IPCA+6%, IPCA+0,5% (CDB, LCI, LCA) |
| `QA-10 ...` | 12 | 200 | volume |
| `QA-11 ...` | 7 | 7 | Calculadora do Cidadão |
| `QA-12 ...` | 2 | 1 | `PUT`/`DELETE` |
| `QA-X ...` | 5 | 3 | valores extremos e validações (`posbase`, `bigrate*`, `prec`, `ext`) |

Ids por título, completos, vêm de `SELECT id, name FROM fixed_income_asset WHERE name LIKE 'QA-%' ORDER BY name;`. Ids citados nos achados:

- Posição de B-03: `b4ef6757-a88d-40ba-95df-fc5ccb8bee43` (título `QA-05 PRE12`, `fd69d080-1fea-4fb8-bbb6-fd589526dedb`).
- Títulos de B-02: `ee22a7fa-e366-4bfc-8f26-6901eee8a507` (PreFixed) e `a649c984-8092-44d5-9745-be923939cbed` (CDI); `QA-03 PRE 2031`: `42557f0f-5225-4f6e-bc58-cef102f358bb`.
- Título do `DELETE` de B-09: criado e apagado (`ffccb331-6c0e-4350-9f3c-72f7537b82c9`), não resta no banco.

Posições `QA-` que eu apaguei do banco, uma a uma por id (o harness negou um `DELETE ... WHERE application_date < '2020-01-01'` por escopo
indeterminado e aceitou os deletes por id restritos a títulos `QA-`). Motivo: o resumo da carteira estava inutilizável (B-02) e não há
`DELETE` de aporte na API.

| Posição | Motivo |
|---|---|
| `e7d18e47-8751-4a87-ae83-f2d376506cf9` e `cdc9cf75-9b85-46e6-bfe2-cf5687402659` | aportes de 2019-06-03 (B-02) |
| `cddc8386-e7cd-4528-aef2-1d31548d968d` | aporte de 2031-06-01 (B-02, outra ponta) |
| `4ca40589-4614-48ec-95a9-86e7b8d4df09`, `fa26b365-6679-42e8-acfe-e337a60cdf0b` e `1b2d9d04-439e-41fa-83dd-9a2e8ef71e64` | valores extremos (R$ 99.999.999.999.999 e taxas de 1e11 a 1e12) que distorciam os totais (R$ 2e21) |

Limpeza completa sugerida ao final da fase, quando o orquestrador quiser:

```sql
DELETE FROM position WHERE asset_id IN (SELECT id FROM fixed_income_asset WHERE name LIKE 'QA-%');
DELETE FROM fixed_income_asset WHERE name LIKE 'QA-%';
```

Tabelas `index_rate` e `holiday`: intactas (1795 e 139 linhas).

## Resumo

| Roteiro | Veredito | Bugs e riscos |
|---|---|---|
| QA-01 | BUG (baixa) e RISCO | B-04, B-06, B-07, B-09, R-03, R-04, R-05 |
| QA-02 | OK | observação B-08; minúsculas e número do enum aceitos na rota |
| QA-03 | OK nos valores; BUG | B-02 (carteira quebra por aporte fora do calendário), B-05 (mensagem) |
| QA-04 | OK | nenhum; identidades e IOF fechados em 261 projeções |
| QA-05 | OK (data padrão); RISCO | R-01 (UTC contra Brasília; reproduzido só no job) |
| QA-06 | OK; BUG (baixa) | B-03 (mensagem), R-02 |
| QA-07 | OK; sub-item `NÃO EXECUTADO` | mês anterior sem cotação exige alterar `index_rate` |
| QA-10 | OK | summary 28 a 37 ms com 268 posições; nenhum RISCO |
| QA-11 | OK | diferença R$ 0,00 nos 7 casos |
| (transversal) | BUG | B-01 (`500` por overflow numérico em título e aporte) |

## Parte B — QA-08 e QA-09

Executada em 2026-10-31, de 01h36 a 01h54 UTC (22h36 a 22h54 de 30/10 em Brasília), no commit `bd85c61`. Método: caixa-preta, `curl`.
A API do compose (`fixedincome-api`, porta 8080) ficou ligada à rede o tempo todo; o SGS "fora do ar" foi simulado com uma instância
extra (`dotnet run`, porta 5124, `ASPNETCORE_ENVIRONMENT=Development`, connection string do banco do compose, `HTTPS_PROXY=http://127.0.0.1:9`,
proxy morto). O código de `src/` não foi aberto. Antes de tudo: `DELETE FROM index_rate;` (1795 linhas apagadas; `holiday` com 139 e
`fixed_income_asset` com 51 títulos `QA-`, intactos). A tabela é reimportada pelo próprio job; o estado final foi conferido (ver "Estado final").

### R-01 — Fuso horário com a tabela vazia: **RISCO confirmado, com efeito em valor** (decisão documentada na spec)

Instância 5124 com proxy morto e `index_rate` vazia. Hora da execução: `date -u` = `Sat Oct 31 01:37:42 UTC 2026`, ou seja, 22h37 de 30/10 em
Brasília (Brasil sem horário de verão; o `date` do Git Bash não aplica `TZ`, a hora de Brasília é UTC−3).

```
POST http://localhost:5124/api/assets
{"name":"QA-R01","issuer":"QA-Banco","assetType":"Cdb","indexType":"PreFixed","rate":12,"issueDate":"2026-01-01","maturityDate":"2030-12-31"}
-> 201, id 0657db15-a3fa-407a-9f58-44246cb998e7

POST http://localhost:5124/api/positions
{"assetId":"0657db15-a3fa-407a-9f58-44246cb998e7","investedAmount":1000,"applicationDate":"2026-10-28"}
-> 201, id cf16bc87-f09e-4395-9b53-c13c16874f9b

GET http://localhost:5124/api/positions/cf16bc87-f09e-4395-9b53-c13c16874f9b/projection
-> 200 {"grossAmount":1001.35,"grossYield":1.35,"taxRate":22.5,"taxAmount":0.03,"netAmount":1000.10,"elapsedDays":3,"businessDays":3,"iofRate":90,"iofAmount":1.22,"isEstimated":false}

GET .../projection?referenceDate=2026-10-30      (mesma posição, mesmo instante)
-> 200 {"grossAmount":1000.90,"grossYield":0.90,"taxRate":22.5,"taxAmount":0.01,"netAmount":1000.05,"elapsedDays":2,"businessDays":2,"iofRate":93,"iofAmount":0.84,"isEstimated":false}
```

- **Resultado:** `elapsedDays 3`: a data usada foi a UTC (2026-10-31), um dia à frente de Brasília (2026-10-30). Resposta 200 (PreFixed não depende de
  cotação, por isso não há 400).
- **Efeito em valor:** o rendimento sai de um dia a mais (R$ 1.001,35 contra R$ 1.000,90 bruto) e o IOF cai uma faixa (90% em vez de 93%).
  Entre 21h e 24h de Brasília, uma consulta sem data com a tabela vazia devolve o valor "de amanhã".
- **Contraste:** com a tabela já recomposta (01h39 UTC), a mesma projeção sem data devolveu `elapsedDays 2`, `grossAmount 1000.90`, porque a data
  padrão passa a ser o dia útil seguinte à última cotação (2026-10-30). O problema só existe sem nenhuma cotação de CDI.
- **Expectativa:** a data "de hoje" do investidor é a de Brasília. A `SPEC-F7-EXECUCAO.md` (linhas 93 a 97) registra a escolha de UTC como
  "simplificação", então é divergência de bom senso e não de spec: classifico como `RISCO`, não `BUG`. O README não avisa dessa janela.
- Sugestão de teste (padrão `testes-xunit`): `Projecao_sem_data_e_sem_cotacao_entre_21h_e_meia_noite_em_brasilia_usa_a_data_de_brasilia` (relógio
  stub em 2026-10-31T01:37:42Z, aporte PreFixed em 2026-10-28, esperado `ElapsedDays` 2).
- Observação: a janela UTC≠Brasília se fecha às 03h UTC; o mesmo vale para o `dataFinal` do job (já visto em QA-05).

### QA-08 — Banco Central fora do ar: **OK** (com um achado paralelo, PB-02)

Instância 5124 com proxy morto, tabela vazia, iniciada às 01h36 UTC. Dados `QA-` criados: título CDI 100% `QA-R01-CDI` (id
`46bd4624-5fbf-4d28-bb86-d576b06c710e`, emissão 2026-01-01, vencimento 2030-12-31) e aporte de R$ 1.000 em 2026-09-01 (id
`af80831e-9efe-4ba7-b6b1-49912b144d72`).

| Requisição (porta 5124) | Resposta | Veredito |
|---|---|---|
| `GET /api/assets` | 200 | OK: API de pé sem o SGS |
| `GET /api/indexes/Cdi?from=2026-10-26&to=2026-10-30` | 200 `{"data":[]}` | OK (tabela vazia, lista vazia) |
| `GET /api/positions/af80831e-.../projection?referenceDate=2026-10-15` | **400** `"Cotação de CDI ausente para 2026-09-01."` | OK: cita a primeira data sem cotação (o dia do aporte, dia útil), nunca 500 |
| `GET /api/positions/af80831e-.../projection` (sem data) | **400** `"Cotação de CDI ausente para 2026-09-01."` | Comportamento observado, igual ao de data explícita (sem cotação, a data padrão é "hoje" e o 400 aponta a primeira data faltante) |
| `GET /api/portfolio/summary` (sem data) | **400** `"Não há cotação de IPCA publicada para estimar a projeção."` | Comportamento observado: o resumo para na primeira posição que falha (aqui, uma posição IPCA+ da Parte A). Mensagem não cita CDI nem data; ver nota abaixo |
| `GET /api/portfolio/summary?referenceDate=2026-10-15` | 400 `"A data de referência não pode ser anterior à data de aporte."` | R-02 da Parte A (aportes de 28/10 e 30/10), não é efeito do SGS |

Nota: com a tabela vazia, o resumo da carteira inteira fica indisponível (400) enquanto houver qualquer posição CDI ou IPCA+, como esperado
pela spec ("sem cotações, a projeção de CDI e IPCA+ falha"); a mensagem do resumo depende de qual posição falha primeiro e não diz o que fazer.
Não é bug, é fragilidade de mensagem (o mesmo padrão de B-03/B-05).

**Logs da instância sem rede (arquivo `qa08-proxy.log` no scratchpad da sessão):**

- Para CDI (série 12) e depois IPCA (série 433): 4 tentativas cada (`Attempt: '0'`, `'1'`, `'2'`, `'3'`, cada uma `Handled: 'True'`, ~2,03 s
  de tempo de execução porque a recusa do proxy no Windows demora 2 s), 3 eventos `OnRetry` por série (6 no total), a última tentativa como
  `fail: Polly[3]`. O espaçamento entre as tentativas (backoff) não é medível: o log não tem carimbo de hora; a configuração é a do
  `AddStandardResilienceHandler` (backoff exponencial), já descrita em `VALIDACAO-F7.md`, critério 4.
- Em seguida `fail: ...ImportIndexRatesUseCase: Falha ao importar as cotações de Cdi.` e `... de Ipca.` (com a `HttpRequestException`) e
  `warn: ...IndexRatesImportJob: Importação de cotações terminou com falha: Falha ao importar as cotações de Cdi e Ipca.`
- O CDI falhar não impediu a tentativa do IPCA. O processo não caiu: continuou atendendo (as requisições da tabela acima vieram depois da
  falha) e `GET /api/assets` seguiu em 200.

**Recuperação.** Encerrei a instância, subi outra sem proxy (01h38m26s UTC, tabela ainda vazia). Às 01h38m56s: `Importação de cotações concluída:
1714 de CDI, 0 de CDI descartadas e 81 de IPCA.`; banco: `Cdi|1714|2020-01-02|2026-10-29` e `Ipca|81|2020-01-01|2026-09-01`;
`count(*) = 1795` e `count(distinct (index_type, reference_date)) = 1795`. As projeções e o `GET /api/indexes/Cdi` voltaram a responder 200.
Limite do teste: recuperei reiniciando o processo (a importação roda ao iniciar). Esperar a "execução seguinte do job" de 6 h não foi feito
(`NÃO EXECUTADO` para esse trecho literal). Consequência prática, derivada de README linhas 71 e 279: uma API que falha por falta do SGS
fica até 6 h sem cotações novas, sem tentar de novo antes disso (RISCO operacional, não bug).

### QA-09 — Duas instâncias importando ao mesmo tempo: **BUG** (média) de concorrência, sem perda nem duplicação de dados

Procedimento de cada tentativa (script `qa09.sh` no scratchpad): encerrar a instância local, `DELETE FROM index_rate;`, `docker restart
fixedincome-api` e `dotnet FixedIncome.Api.dll --urls http://localhost:5124` (sem proxy, importação ligada, mesmo banco), com o intervalo indicado
entre as duas partidas; esperar 40 a 75 s; ler `docker logs -t` e o log da instância local; conferir o banco. Oito tentativas (a contagem
"N de CDI/IPCA" do log é o que o SGS devolveu, não o que foi inserido):

| # | Partida (início) | Contêiner | Instância local | Banco no fim |
|---|---|---|---|---|
| t1 | docker e local no mesmo instante (~01h40) | importou 0/0 (série já pronta) | 1714 CDI, 81 IPCA | 1795 / 1795, sem erro |
| t2 | docker; local +2,1 s (01h42m54) | 1714 CDI, 81 IPCA (concluiu às 01h42m58) | 0/0 | 1795 / 1795, sem erro |
| t3 | local; docker +0,09 s (01h44m46) | 0/0 | 1714 CDI, 81 IPCA | 1795 / 1795, sem erro |
| t4 | local; docker +1,1 s (01h46m05) | 0 CDI, 81 IPCA (sobreposição na série de IPCA) | 1714 CDI, 81 IPCA | 1795 / 1795, sem erro |
| t5 | local; docker +2,1 s (01h47m25) | 0/0 | 1714 CDI, 81 IPCA | 1795 / 1795, sem erro |
| t6 | docker; local +1,15 s (01h49m55) | 1714 CDI, 0 IPCA (sobreposição na série de CDI) | 1714 CDI, 81 IPCA | 1795 / 1795, sem erro |
| **t7** | **docker; local +1,6 s (01h50m39)** | **FALHOU** em CDI e IPCA com `23505 duplicate key value violates unique constraint "IX_index_rate_index_type_reference_date"` | concluiu (1714 CDI, 81 IPCA) | 1795 / 1795 |
| t8 | docker; local +0,6 s (01h51m25) | CDI sem erro; IPCA falhou, mas por outro motivo (PB-02, não é o índice único) | concluiu | 1795 / 1795 |

**Colisão reproduzida em t7** (1 de 8, na faixa em que os dois processos importam com ~1 s de diferença; o contêiner demora ~3,5 s do
`restart` até importar, a instância local ~2 s do start). Log do contêiner (`qa09-t7-container.log`, horários UTC de 31/10):

```
01:50:43.472 fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
01:50:43.516 fail: Microsoft.EntityFrameworkCore.Update[10000]
             Npgsql.PostgresException: 23505: duplicate key value violates unique constraint "IX_index_rate_index_type_reference_date"
01:50:43.532 fail: FixedIncome.Application.UseCases.ImportIndexRatesUseCase[0]  Falha ao importar as cotações de Cdi.
01:50:43.675 fail: ...ImportIndexRatesUseCase[0]                                  Falha ao importar as cotações de Ipca.
01:50:43.680 warn: ...IndexRatesImportJob  Importação de cotações terminou com falha: Falha ao importar as cotações de Cdi e Ipca.
```

- **O que a API devolve:** nada ao usuário. A importação é um serviço em segundo plano; o processo não caiu e os endpoints seguiram em 200
  (`GET /api/assets` 200 e `GET /api/indexes/Cdi?...` 200 com cotações logo depois).
- **Dados:** sem duplicação (o índice único segurou: `count(*) = count(distinct ...) = 1795` em todas as 8 tentativas). Sem cotação faltando
  nestes testes, porque a instância vencedora importou a série inteira; o `SaveChanges` é um lote só, então o lote perdedor foi descartado por
  inteiro e não deixou linhas parciais.
- **Impacto real:** a importação da instância perdedora é abandonada até a próxima rodada (6 h). Em produção com mais de uma réplica, todo
  início simultâneo (deploy, escala) tende a gerar `fail` e um `warn` de "terminou com falha" mesmo sem problema nenhum, o que polui o alerta que a
  F8 pretende ter sobre falha de importação. Se o perdedor tivesse buscado uma janela mais nova que a do vencedor (por exemplo, uma cotação publicada
  entre as duas leituras), essa cotação só entraria na rodada seguinte, até 6 h depois. Não observado, só possível.
- **Expectativa:** importação idempotente também sob concorrência (a spec e o teste `Executar_duas_vezes_com_os_mesmos_dados_nao_duplica...` cobrem
  só a execução sequencial). Esperado: nenhuma falha registrada quando outra instância já gravou a mesma cotação.

### PB-02 — SGS responde HTTP 200 com corpo de erro para janela sem cotações, e a importação trata como falha: **BUG** (baixa a média)

Descoberto em t8 e reproduzido direto no SGS. Para uma janela sem dados, o SGS devolve o mesmo corpo `{"erro":{"statusCode":404,"detail":"...SGSNegocioException:
Value(s) not found"}}`, mas com status HTTP **alternando entre 404 e 200** (backends diferentes). O provedor trata o 404 como "sem cotações" (log
`SGS série 12 respondeu 404 ...; tratado como sem cotações.`), mas o 200 com esse corpo cai na desserialização.

```
GET https://api.bcb.gov.br/dados/serie/bcdata.sgs.433/dados?formato=json&dataInicial=01/10/2026&dataFinal=31/10/2026
  -> 12 chamadas seguidas: HTTP 404 e HTTP 200 alternados, corpo sempre {"erro":{"statusCode":404,"detail":"...Value(s) not found"}}
```

Log da API (contêiner, t8, 01:51:28 UTC): `Received HTTP response headers after 52.0941ms - 200` seguido de `fail: ...ImportIndexRatesUseCase: Falha ao
importar as cotações de Ipca.` com `System.Text.Json.JsonException: The JSON value could not be converted to System.Collections.Generic.List`1[...SgsItem]`
e `warn: ... Importação de cotações terminou com falha`.

- **Esperado:** janela sem cotações é "sem novidades" (nem `fail` nem `warn`), qualquer que seja o status HTTP, porque o corpo é o mesmo.
- **Obtido:** em cerca de metade das execuções incrementais sem cotação nova (finais de semana, manhã antes da divulgação, IPCA fora do mês de
  divulgação) a importação registra falha. Sem perda de dados (não há o que importar), mas o log de erro/aviso passa a ter falso positivo
  frequente, o que mina qualquer alerta de "importação falhou". Reproduzido hoje na janela IPCA 01/10 a 31/10/2026 (a série mensal só vai a 2026-09).
- Sugestão de teste: `Resposta_200_com_corpo_de_erro_do_sgs_e_tratada_como_sem_cotacoes` (em `SgsIndexRateProviderTests`, `HttpMessageHandlerStub` devolvendo
  200 com `{"erro":{"statusCode":404,...}}`; esperado lista vazia, sem exceção).

### Bugs e riscos da Parte B (sem classificação de severidade; sugestões)

| Id | Tipo | Resumo | Sugestão de teste |
|---|---|---|---|
| PB-01 | BUG (média) | Duas instâncias iniciando juntas: uma das importações falha com `23505` no índice único, sem duplicar nem perder dados (1 de 8 tentativas, janela de ~1 s) | `Importacao_concorrente_com_a_mesma_serie_nao_falha_por_violacao_do_indice_unico` (repositório contra PostgreSQL real, dois contextos gravando a mesma cotação; esperado: segunda gravação ignorada ou atualizada, sem exceção) |
| PB-02 | BUG (baixa a média) | SGS devolve 200 com corpo `{"erro":...}` para janela vazia; importação registra falha | `Resposta_200_com_corpo_de_erro_do_sgs_e_tratada_como_sem_cotacoes` |
| R-01 | RISCO | Sem nenhuma cotação, a data padrão é a UTC; às 22h37 de Brasília, `elapsedDays 3` (devia ser 2), rendimento e IOF diferentes | `Projecao_sem_data_e_sem_cotacao_entre_21h_e_meia_noite_em_brasilia_usa_a_data_de_brasilia` |
| PB-03 | RISCO | Falha de importação (rede ou concorrência) só é repetida na rodada seguinte, até 6 h depois | (decisão de produto: nova tentativa mais curta após falha) |

### Dados criados na Parte B

- Título `QA-R01` (PreFixed 12%): `0657db15-a3fa-407a-9f58-44246cb998e7`; aporte `cf16bc87-f09e-4395-9b53-c13c16874f9b` (R$ 1.000, 2026-10-28).
- Título `QA-R01-CDI` (CDI 100%): `46bd4624-5fbf-4d28-bb86-d576b06c710e`; aporte `af80831e-9efe-4ba7-b6b1-49912b144d72` (R$ 1.000, 2026-09-01).
- `index_rate` foi esvaziada 9 vezes (uma no início de QA-08 e uma por tentativa t1 a t8 de QA-09) e reimportada pelo job; nada além de
  `index_rate` foi apagado. Logs no scratchpad da sessão: `qa08-proxy.log`, `qa08-recovery.log`, `qa09-t1..t8-container.log` e `-local.log`.

### Estado final conferido (01h54 UTC)

- `index_rate`: `Cdi 1714 (2020-01-02 a 2026-10-29)` e `Ipca 81 (2020-01-01 a 2026-09-01)`; `count(*) = 1795`, `count(distinct (index_type, reference_date)) = 1795`.
- `holiday` 139; `fixed_income_asset` 53 (todos `QA-`: os 51 da Parte A mais os 2 desta parte).
- API do compose: `fixedincome-api` "Up", `GET http://localhost:8080/api/assets` 200 e `GET /api/indexes/Cdi?from=2026-10-26&to=2026-10-30` 200 com
  4 cotações (26, 27, 28 e 29/10, valor 0.050788). `fixedincome-db` healthy.
- Porta 5124 sem processo; nenhum `dotnet` meu (resta só o do VS Code, `csdevkit`, que não é meu; os nós do MSBuild foram encerrados com
  `dotnet build-server shutdown`); nenhuma variável de proxy no ambiente do shell nem em `User`/`Machine`.

### Resumo da Parte B

| Roteiro | Veredito | Achados |
|---|---|---|
| R-01 (fuso, tabela vazia) | RISCO confirmado (`elapsedDays 3` às 22h37 de Brasília) | decisão documentada na spec; efeito em valor e IOF |
| QA-08 (SGS fora do ar) | OK | 200 em `/api/assets`; 400 com data da primeira cotação faltante; retry 4 tentativas e erro no log; recuperação com 1795 linhas; PB-02 paralelo; PB-03 (6 h) |
| QA-09 (duas instâncias) | BUG (média) | PB-01: `23505` em 1 de 8 tentativas, sem duplicata nem perda; contêiner continuou de pé |

## Triagem e veredito (orquestrador, 30–31/10/2026)

> Reteste após as correções: ver a seção final "Reteste após as correções (2026-11-02)" no fim deste arquivo.

Cada BUG foi reproduzido pelo orquestrador na API do compose (`localhost:8080`) com a requisição registrada, em 31/10/2026 às 01h55–02h05
UTC. Posições de teste do orquestrador (2019 e 2026-10-31) foram apagadas por id ao final; o resumo voltou a 200.

**Veredito: COM RESSALVAS.** Nenhum valor financeiro divergiu (Calculadora do Cidadão, 7 casos com diferença de R$ 0,00; 261 projeções
contra o modelo independente). Há **1 bug de severidade alta** (B-01) e **3 de severidade média** (B-02, PB-01, PB-02). Nenhum afeta o
cálculo de valores. Nada foi corrigido durante o QA.

| Id | Severidade | Reprodução do orquestrador | Origem |
|---|---|---|---|
| B-01 | **alta** (500) | Reproduzido: POST `/api/assets` com `rate` 1e15, POST `/api/positions` com `investedAmount` 1e16 e PUT `/api/assets/{id}` com `rate` 1e15 deram 500 e `22003: numeric field overflow` no log | v1 (não é regressão da F7) |
| B-02 | **média** | Reproduzido: aporte de 2019-06-03 aceito (201); depois `GET /api/portfolio/summary` deu 400 `Data 2019-06-03 anterior à cobertura do calendário, 2020-01-01.` para a carteira inteira | F7 (calendário RN-08 sem validação na entrada) |
| PB-01 | **média** | Confirmado por log e código, **não reproduzido por mim**: `qa09-t7-container.log` tem 12 ocorrências de `23505` no índice único e `terminou com falha`, e o `UpsertAsync` lê e depois insere sem tratar conflito (corrida check-then-insert). Intermitente: 1 de 8 tentativas, sem duplicata nem perda | F7 |
| PB-02 | **média** | Reproduzido na origem: 12 chamadas seguidas ao SGS (série 433, 01/10 a 31/10/2026) devolveram `404 200 200 200 200 200 200 404 200 ...` com o mesmo corpo `{"erro":{"statusCode":404,...}}`; o 200 com esse corpo vira `JsonException` e a importação registra falha (`qa09-t8`) | F7 (P7: período sem dado = lista vazia) |
| B-03 | baixa | Reproduzido: projeção sem data de aporte de 2026-10-31 devolve 400 `A data de referência não pode ser anterior à data de aporte.` sem citar a data padrão usada | F7 |
| B-04 | baixa | Reproduzido: `docker compose exec api dotnet ef database update` falha (`The application 'ef' does not exist`) | v1/F6 (README) |
| B-05 | baixa | Reproduzido: `referenceDate=2031-06-01` devolve `Data 2031-05-31 posterior à cobertura ... 2030-12-31.` (data errada, um só limite) | F7 |
| B-06 | baixa | Reproduzido: POST sem `Content-Type` devolve 415 `application/problem+json`; `GET /api/foo` devolve 404 sem corpo | v1 |
| B-07 | baixa | Reproduzido: `rate 12.123456789` ecoado no 201; o banco guarda 6 casas | v1 |
| B-08 | baixa | Reproduzido: `/api/indexes/1` devolve as cotações de CDI | F7 |
| B-09 | baixa | Reproduzido: `DELETE` de título sem aportes devolve 204 e o log traz `Writing to the response body is invalid for responses with status code 204` | v1 |

Riscos (não viram correção sem decisão do Matheus):

- **R-01 (fuso).** Reproduzido pelo QA às 22h37 de Brasília, com a tabela de cotações vazia: `elapsedDays 3` (data UTC) contra `2` (Brasília),
  `grossAmount 1001.35` contra `1000.90`. O código confirma: `GetPositionProjectionUseCase.cs:74` e `GetPortfolioSummaryUseCase.cs:103`
  usam `GetUtcNow()`. Só acontece antes da primeira importação de CDI e na janela de 21h às 24h em Brasília; a spec registra como
  simplificação (`SPEC-F7-EXECUCAO.md`, decisão "Data de referência omitida").
- **R-02.** Resumo com `referenceDate` explícita anterior ao aporte de qualquer posição devolve 400 para a carteira toda.
- **R-03 a R-06, PB-03.** Porta do `.http` (5124) contra a do README (8080); `/health` ainda não existe (F8); `201` sem `Location`; soma
  dos percentuais do resumo pode dar 100,01; falha de importação só é repetida na rodada seguinte (até 6 h).
- **Não executado.** QA-07, falha de "mês anterior ao último publicado sem cotação" (exigia abrir um buraco em `index_rate`; coberto por
  teste de unidade); rodada seguinte de 6 h do job (a recuperação foi feita reiniciando).

## Reteste após as correções (2026-11-02)

### Ambiente do reteste

| Item | Valor |
|---|---|
| Data | 2026-11-02, de 04h15 a 04h50 UTC (01h15 a 01h50 em Brasília) |
| Código | árvore de trabalho sobre `bd85c61` com as correções ainda não commitadas; API do compose (`fixedincome-api`, porta 8080) reconstruída pelo orquestrador (`docker compose up -d --build api`), banco e volumes intactos |
| Dados de mercado | os mesmos do QA: 1714 cotações de CDI (2020-01-02 a 2026-10-29) e 81 de IPCA (2020-01 a 2026-09), 139 feriados. **D continua 2026-10-29**: o SGS respondeu "sem dados" para 30/10 a 02/11 em todas as ~42 importações deste reteste, então não saiu cotação nova |
| Método | Caixa-preta com `curl` contra `http://localhost:8080`. Instâncias extras (portas 5124 e 5125) rodaram a mesma DLL publicada, extraída do contêiner (`docker cp fixedincome-api:/app`), ou seja, o código corrigido. Os valores financeiros foram comparados com os do QA (que passaram pelo modelo independente) e a Calculadora do Cidadão foi consultada de novo, ao vivo |
| Escritas no banco | `index_rate` esvaziada 13 vezes (1 no QA-08 e 1 por tentativa do PB-01, 12) e reimportada pelo job; nada além de `index_rate` foi apagado em massa. Um banco temporário `qa_tmp` foi criado e removido no B-04 |
| Janela de fuso | O reteste caiu fora da janela 21h-24h de Brasília (01h15 em Brasília, mesma data que em UTC). O R-01 não pôde ser reproduzido (ver tabela) |

### Resultado por achado

| Id | Requisição usada | Esperado | Obtido | Veredito |
|---|---|---|---|---|
| B-01 | `POST /api/assets` com `rate 1e15`; `POST /api/positions` com `investedAmount 1e16`; `PUT /api/assets/{id}` com `rate 1e15` | 400 | Os três: **400** (`"Rate deve ser menor que 1000000000000."` e `"InvestedAmount deve ser menor que 10000000000000000."`), nenhum 500 | CORRIGIDO |
| B-01 (limites) | `rate 999999999999` e `investedAmount 99999999999999.99`; `rate 1e12` e `investedAmount 10000000000000000` | 201 e 201; 400 e 400 | 201 e 201; 400 e 400 | CORRIGIDO |
| B-07 | `rate 12.123456789`, `rate 12.1234567` (POST e PUT), `investedAmount 10.999` e `1000.001`; dentro do limite: `rate 12.123456`, `12.1234560`, `investedAmount 10.99` e `1000.00` | 400 com mais casas; 201 dentro do limite | 400 `"Rate deve ter no máximo 6 casas decimais."` e `"InvestedAmount deve ter no máximo 2 casas decimais."`; os de 6 e 2 casas: 201 | CORRIGIDO |
| B-02 (QA-03) | `POST /api/positions` com `applicationDate` 2019-06-03, 2019-12-31, 2031-01-01 e 2031-06-01 (título 2019-2040); depois `GET /api/portfolio/summary` | 400 citando o período e a data enviada; resumo segue 200 | 400 `"Data de aporte 2019-06-03 fora do período coberto pelo calendário (2020-01-01 a 2030-12-31)."` (idem para as outras três datas); aportes de borda 2020-01-01 e 2030-12-31 aceitos (201); resumo 200 | CORRIGIDO |
| B-05 (QA-03) | `GET /api/positions/{aporte 2020-01-01}/projection?referenceDate=2031-06-01`, `=2031-01-02`, `=2035-06-01` e `=2031-01-01` | 400 com a data enviada e o período inteiro; 2031-01-01 continua válido | 400 `"Data 2031-06-01 fora do período coberto pelo calendário (2020-01-01 a 2030-12-31)."` (idem `2031-01-02` e `2035-06-01`); `2031-01-01`: 200. Também `GET /api/portfolio/summary?referenceDate=2031-06-01`: 400 com a mesma mensagem | CORRIGIDO |
| B-03 (QA-06) | Posição de 2026-11-05 (PreFixed): `GET /api/positions/9cc0adf2-0753-47c1-8133-e336183a9f19/projection` sem data | 400 com a data padrão, a explicação e o convite a informar `referenceDate` | 400 `"A data de referência padrão (2026-10-30, o dia útil seguinte à última cotação de CDI) é anterior à data de aporte (2026-11-05). Informe referenceDate."` | CORRIGIDO |
| B-03 (data explícita) | Mesma posição, `?referenceDate=2026-10-30` | continua 400 | 400 `"A data de referência não pode ser anterior à data de aporte."` | CORRIGIDO (comportamento mantido) |
| R-02 (resumo sem data) | `GET /api/portfolio/summary` com a posição de 2026-11-05 e outras futuras | 200, posição com rendimento zero | 200; ao criar a posição de R$ 1.000 o bruto subiu exatamente R$ 1.000, com IR e IOF inalterados | CORRIGIDO |
| R-02 (resumo com data) | `GET /api/portfolio/summary?referenceDate=2026-10-15`; um script soma as projeções individuais (`?referenceDate=2026-10-15`) de cada posição com aporte até essa data | 200; só as posições com aporte até a data; total igual à soma das projeções | `positionCount 266` de 286 (20 ficaram fora); investido, bruto, rendimento, IR, IOF e líquido idênticos à soma de 266 projeções (`846031.99`, `953738.66`, `107706.67`, `10258.50`, `75.51`, `943404.65`), 0 falhas, 0 violações da identidade líquido = bruto − IOF − IR. Com `referenceDate=2020-01-01`: 3 posições, 3000.00, igual à soma | CORRIGIDO |
| R-02 (data anterior a todos) | `GET /api/portfolio/summary?referenceDate=2019-12-31` | 200 zerado | 200 `{"totalInvestedAmount":0,...,"positionCount":0,"byAssetType":[]}` | CORRIGIDO |
| R-01 / QA-05 | Cenário da Parte B (tabela vazia, instância extra) dentro da janela 21h-24h de Brasília | `elapsedDays` pela data de Brasília | Hora do reteste: 04h42 UTC = 01h42 em Brasília de 2026-11-02, fora da janela (a próxima abre às 00h00 UTC). O cenário que distingue UTC de Brasília **não foi executado**. Confirmado só o que não depende da janela: o log do job usa `dataFinal=02/11/2026` (igual em UTC e em Brasília agora, portanto não prova a correção); com a tabela vazia (instância 5124) a projeção sem data de uma posição PreFixed de 2026-10-28 deu `elapsedDays 5` (data 2026-11-02); com a tabela cheia, a mesma projeção sem data dá `elapsedDays 2` (2026-10-30, o dia útil seguinte a D), idêntica a `referenceDate=2026-10-30` | NÃO EXECUTADO (fora da janela). A correção só tem o teste de unidade `BrasiliaTimeProviderTests.cs` como evidência, que não rodei |
| PB-02 | 21 reinícios da API contra o SGS real (42 chamadas; as 12 tentativas do PB-01 e a instância extra também não tiveram `JsonException`); janelas de CDI 30/10 a 02/11 e de IPCA 01/10 a 02/11; log de cada importação | nenhum `fail`/`JsonException`/"terminou com falha" quando o SGS responde 200 com corpo de erro | De ~42 chamadas à janela vazia, 40 vieram 404 e **2 vieram 200** com o corpo de erro; as duas viraram `SGS série 12 respondeu 200 sem dados para 2026-10-30 a 2026-11-02; tratado como sem cotações.` e a importação concluiu normalmente. 0 ocorrências de `JsonException`, `Falha ao importar` e `terminou com falha` | CORRIGIDO |
| QA-08 (SGS fora do ar) | Instância 5124 (DLL corrigida) com `HTTPS_PROXY=http://127.0.0.1:9` e `index_rate` vazia: `GET /api/assets`; `GET /api/indexes/Cdi?from=2026-10-26&to=2026-10-30`; projeção do aporte CDI de 2026-09-01 com e sem data; resumo; depois instância sem proxy | 200; lista vazia; 400 citando a primeira data faltante; retry; log de erro; recuperação | `/api/assets` 200; cotações `[]` 200; projeção CDI com e sem data: 400 `"Cotação de CDI ausente para 2026-09-01."`; resumo 400 `"Não há cotação de IPCA publicada para estimar a projeção."` (igual ao QA, não é regressão). Log: 4 tentativas por série (3 `OnRetry` cada), `fail: Falha ao importar as cotações de Cdi.` e `de Ipca.`, `warn: ... terminou com falha`. Recuperação: a instância sem proxy importou 1714 de CDI e 81 de IPCA, e a projeção voltou a 200 | CORRIGIDO (comportamento mantido) |
| PB-01 | `race.sh` (no scratchpad): a cada tentativa, `DELETE FROM index_rate`, `docker restart fixedincome-api` e `dotnet FixedIncome.Api.dll --urls http://localhost:5124` com intervalo entre as partidas de 0,6 a 2,0 s, ordem alternada; 50 s de espera; logs e `count(*)` contra `count(distinct (index_type, reference_date))` | sem falha registrada; banco sem duplicata | **12 tentativas** (intervalos 0,6; 0,8; 1,0; 1,0; 1,2; 1,3; 1,3; 1,4; 1,6; 1,6; 1,9 e 2,0 s). **A colisão reproduziu 1 vez, na t12** (contêiner primeiro, instância local 0,8 s depois): a instância local gravou em lote, recebeu `23505 ... IX_index_rate_index_type_reference_date` (duas vezes no log) e **terminou com `Importação de cotações concluída: 1714 de CDI ...`, sem `Falha ao importar` nem `terminou com falha`**. Nas outras 11 não houve `23505`. Em todas as 12: banco 1795/1795, sem duplicata, nenhuma importação registrou falha | CORRIGIDO, com evidência de execução real: a corrida reproduziu e a importação não falhou. Isto não vem de teste automatizado nem de "a corrida não reproduziu"; a colisão aconteceu de fato. Ver NB-01 sobre o ruído de log |
| B-06 | `GET /api/foo`; `PATCH /api/assets`; `POST /api/assets` sem `Content-Type` (`-H 'Content-Type:'`), com `application/x-www-form-urlencoded` e com `text/plain`; `GET /api/assets/00000000-0000-0000-0000-000000000001`; `GET /api/positions/00000000-0000-0000-0000-000000000001/projection` | 404, 405 (com `Allow`) e 415 em `{statusCode, success, message, data}`; 404 de título e posição com a mensagem do caso de uso | 404 `"Recurso não encontrado."`; 405 `"Método HTTP não permitido para este recurso."` com `Allow: GET, POST`; 415 `"Tipo de conteúdo não suportado. Envie o corpo como application/json."`; todos `application/json`. Título inexistente: 404 `"Título com id ... não encontrado."`; posição: `"Posição com id ... não encontrada."` | CORRIGIDO |
| B-08 | `GET /api/indexes/{x}?from=2026-09-14&to=2026-09-15` com `x` = `1`, `0`, `99`, `-1`, `cdi`, `CDI`, `Cdi`, `ipca`, `PreFixed`, `Selic` | `1`, `0`, `99` e `-1` com 400; `cdi` e `CDI` com 200 | `1`, `0`, `99`, `-1` e `Selic`: 400 `"O valor do campo 'indexType' é inválido."`; `cdi`, `CDI`, `Cdi` e `ipca`: 200; `PreFixed`: 400 `"Só há cotação para CDI e IPCA."` | CORRIGIDO |
| QA-02 (regressão) | A tabela inteira do QA-02 (22 requisições da Parte A) | sem mudança | Idêntica: 400 para `from > to`, ausentes, malformadas, `PreFixed`, `Selic`, `99`, `-1` e `Cdi,Ipca`; 200 para `cdi`, intervalo enorme (11 ms), fim de semana (`[]`), IPCA parcial (só fevereiro), `2026-9-1` e `2026-09-14T00:00:00`. A mensagem de data inválida agora cita só o campo errado (`'from'` ou `'to'`) | OK (sem regressão) |
| B-09 | `DELETE /api/assets/ec13e4d1-8be4-45f2-b139-63cdfd0f7ff5` (título sem aportes); contagem de `fail:` em `docker logs fixedincome-api` antes e depois | 204 sem corpo; nenhum `fail:` | `HTTP/1.1 204 No Content`, sem corpo e sem `Content-Type`; `fail:` antes 0, depois 0. `DELETE` de título com aportes: 400 `"Título possui aportes e não pode ser removido."`; inexistente: 404; ainda 0 `fail:` | CORRIGIDO |
| B-04 | Trecho "Com Docker" do README (ver "B-04: detalhes") | migration funciona; a ordem migration, depois restart, faz a primeira importação ter sucesso | O comando do README, literal, aplicou as duas migrations num banco temporário, exit 0. Antes da migration a importação falha com `42P01 relation "index_rate" does not exist`; depois dela, com a instância reiniciada, importou 1714 de CDI e 81 de IPCA | CORRIGIDO |

#### B-04: detalhes

- O comando do README, rodado em PowerShell (`docker run --rm --network container:fixedincome-db -v "${PWD}:/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 sh -c 'dotnet tool restore && dotnet ef database update --project src/FixedIncome.Infrastructure --startup-project src/FixedIncome.Api'`), funciona: restaura o `dotnet-ef 8.0.11`, compila e aplica `20260818070004_InitialCreate` e `20261030222641_AddHolidaysAndIndexRates`.
- Para não tocar o banco `fixedincome` precisei de um banco temporário, e o comando **não aceita outra conexão**: `FixedIncomeDbContextFactory` lê só `src/FixedIncome.Api/appsettings.Development.json` e ignora `ConnectionStrings__DefaultConnection`. A primeira tentativa, com `-e ConnectionStrings__DefaultConnection=...Database=qa_tmp`, bateu no `fixedincome` ("No migrations were applied. The database is already up to date.") e o `qa_tmp` seguiu vazio. Contornei rodando o comando, sem alteração, a partir de uma cópia descartável do repositório (no scratchpad) cujo `appsettings.Development.json` apontava para `qa_tmp`. É uma observação, não achado: o README já diz que a conexão é a do `appsettings.Development.json`.
- Efeito colateral no repositório: o `docker run` da primeira tentativa montou a raiz do repositório e compilou lá dentro, então `obj/` (ignorado pelo git) recebeu artefatos de Linux. Se o próximo `dotnet build` no Windows reclamar do `project.assets.json`, um `dotnet restore` resolve. `git status` não mostra arquivo novo.
- Ordem: com o banco `qa_tmp` ainda sem tabelas, a primeira importação registra `42P01` e `Falha ao importar as cotações de Cdi.`; depois da migration, ao reiniciar, importa 1714 CDI e 81 IPCA, e `holiday` tem 139 linhas. Para fechar o trecho literal do README rodei `docker compose restart api` no banco principal: API 200 e 0 `fail:` no log.

### Regressão de valores

| Roteiro | Resultado |
|---|---|
| QA-04 (IOF) | CDB, LCI, LCA e Tesouro (`TreasuryBond`), PreFixed 12%, R$ 1.000, aportes 1, 15, 29 e 30 dias antes de D, projeção em 2026-10-29: `iofRate` 96, 50, 3 e 0 (LCA sempre 0); `grossAmount` 1000.45, 1004.96, 1009.03 e 1009.49; `iofAmount` 0.43, 2.48, 0.27 e 0.00 (LCA 0 em todos); `taxAmount` de CDB e Tesouro 0.00, 0.56, 1.97 e 2.14 (LCI e LCA 0); líquido do CDB 1000.02, 1001.92, 1006.79 e 1007.35; da LCI 1000.02, 1002.48, 1008.76 e 1009.49. **Os 16 casos idênticos aos do QA.** Identidade líquido = bruto − IOF − IR exata nos 16 e nas 266 projeções do resumo de 2026-10-29; resumo `TotalNetAmount = TotalGrossAmount − TotalTaxAmount − TotalIofAmount` (987576.36 − 10778.17 − 66.12 = 976732.07), igual à soma das projeções. **OK** |
| QA-11 (Calculadora do Cidadão) | 7 casos, API contra a Calculadora consultada ao vivo: 5000 a 105% (2025-03-10 a 2025-06-10) 5177.40 = R$ 5.177,40; 1000 a 100% (2025-12-15 a 2026-01-20) 1013.32 = 1.013,32; 10000 a 100% (2025-02-10 a 2025-03-10) 10088.64 = 10.088,64; 20000 a 90% (2024-01-02 a 2026-01-02) 24757.71 = 24.757,71; 3000 a 120% (2026-04-01 a 2026-10-29) 3286.48 = 3.286,48; 1000 a 100% (2025-06-02 a 2025-06-03) 1000.54 = 1.000,54; 1000 a 100% (2023-03-10 a 2025-03-10) 1250.20 = 1.250,20. **Diferença de R$ 0,00 nos 7. OK** |
| QA-07 (IPCA) | IPCA+6%, R$ 1.000, 2026-03-01 a 2026-07-01: bruto 1042.91, `isEstimated false`; 2026-09-01 a 2026-10-02: 1013.74, `isEstimated true`; 2026-09-01 a 2026-10-01: 1013.11, `false`. Deflação, IPCA+0,5%, 2026-08-03 a 2026-08-31: bruto 997.35, rendimento −2.65, `iofAmount 0`, `taxAmount 0`, líquido 997.35; até 2026-09-01: −2.79 com IOF e IR zero; até 2026-08-05: −0.27 (`iofRate 93`) com IOF e IR zero; com R$ 100.000: −265.31, IOF e IR zero; LCI e LCA iguais (−2.65). **Todos idênticos aos do QA. OK** |
| QA-10 (volume) | Com 316 posições, `GET /api/portfolio/summary` em 50, 50 e 59 ms (limite de 2 s); projeção individual em 31 ms. **OK** |

### Bugs novos encontrados

Nenhum que reprove uma correção. Duas observações:

- **NB-01 (baixa, ruído de log).** Quando a colisão do PB-01 acontece (t12), a importação se recupera, mas o próprio EF Core registra em nível `fail:` dois eventos com a pilha do `23505` (`Microsoft.EntityFrameworkCore.Database.Command[20102]` "Failed executing DbCommand ..." e `Microsoft.EntityFrameworkCore.Update[10000]` "An exception occurred in the database while saving changes ..."), o primeiro com o `INSERT` de 4.000 parâmetros. O log de aplicação não registra falha (nem `Falha ao importar`, nem `terminou com falha`), mas um alerta baseado em "qualquer `fail:`" ou em `PostgresException` ainda dispararia. Reprodução: `race.sh` (scratchpad), tentativa t12; log em `race-12-local.log`. Esperado: nenhum `fail:` no caminho tratado (por exemplo, filtrando essas categorias para o caso, ou evitando o `INSERT` duplicado). Teste sugerido (integração, F9): `Importacao_concorrente_nao_registra_erro_no_log_quando_a_violacao_e_tratada`. Não bloqueia.
- **Observação sobre o SGS.** Do host, a série 433 sem dados respondeu `404 200 200 200 200 200 200 404 200 200 200 200` (maioria 200); de dentro do contêiner, 40 de 42 foram 404. Os dois status existem na prática, o que reforça o teste do PB-02; a proporção varia com o caminho de rede.

### Dados criados no reteste

Todos os títulos têm o prefixo `QA-R`. Ao final, `fixed_income_asset` tem 75 linhas (todas `QA-`) e `position` tem 316.

| Título | Id | Posições | Uso |
|---|---|---|---|
| `QA-R base` | `ba6330fe-e298-48e9-8621-b0e91037a44d` | 2: `9de7ab27-face-4d77-b5fc-00277a20d1f8` (10.99) e `2f7be0b4-186b-4ed3-a24f-9a4c64725cb7` (1000.00) | B-01, B-07 |
| `QA-R dec6`, `QA-R dec6b` | `ceaa91b8-6d87-4b93-ab93-bfb89ada3915` (o `dec6b` pelo nome) | 0 | B-07 (rate com 6 casas) |
| `QA-R wide PRE` | `e5394726-ab8b-4bca-9352-012f9808d44a` | 3: `23cd1936-b75d-4dea-adfa-37c0d0b144c3` (2020-01-01), `b3624de0-706d-440d-8f68-91ae4dd5c21e` (2030-12-31) e `9cc0adf2-0753-47c1-8133-e336183a9f19` (2026-11-05) | B-02, B-05, B-03, R-02 |
| `QA-R04 Cdb`, `Lci`, `Lca`, `TreasuryBond` | `8400d44a-ab1e-4a6c-ab9c-5abbe6890d9c`, `f70bc062-6a70-41d7-b210-a50ec8d7c684`, `5e21ac66-2da2-425a-b2bf-5ba427b9b1af`, `8004e75b-245e-488f-9c71-64e4f739f60d` | 4 cada | QA-04 |
| `QA-R05 PRE12` | `40508bd3-23d1-4a2e-95d9-bb0bbe785b97` | 1: `43591f56-4afa-4768-9041-da1673d17f48` (2026-10-28) | data padrão |
| `QA-R07 IPCA6`, `IPCA0.5`, `LCI`, `LCA` | `1055ecca-d1de-41fe-958c-e46a466c45c0`, `bb23d316-e81a-49f1-a112-1fb74db412fb`, `8fa4ff98-5f40-4039-a069-cecf89207837`, `cd0c3a29-ffa2-4584-878b-f50bac7a7b09` | 2, 2, 1 e 1 | QA-07 |
| `QA-R11 c1` a `c7` | `d7dfb1f7-197f-4e80-890c-5a70f017ef51`, `310dbd20-5706-4ff5-8e04-306698b816b4`, `dcb8cf91-3754-4b3c-8b99-bbfd20aeca3c`, `a7abf377-f472-4f78-a419-feaea0857c95`, `8b59fbe7-6bd4-4d4e-9882-f43745218585`, `d488236a-e5b0-4543-8b77-6c87776f0c0c`, `0d95ff98-8954-4070-9317-65b13c34b0d7` | 1 cada | QA-11 |

Criados e já removidos por mim: o título `QA-R lim rate max` (rate 999999999999, id `34d36977-98d0-4072-9b05-b37bc20856ff`, removido pela API), o título `QA-R del` (`ec13e4d1-8be4-45f2-b139-63cdfd0f7ff5`, removido pela API no teste do B-09) e uma posição de R$ 99.999.999.999.999,99 (`76e91fcf-bba7-44d9-890d-52929ee0b2ba`, apagada do banco por id porque distorcia os totais do resumo). Nenhuma posição restante deixa a carteira inconsistente: o resumo responde 200 sem data e com `referenceDate` explícita. A limpeza SQL sugerida na seção "Dados criados durante o QA" remove também os `QA-R*`.

### Estado final do ambiente

- `index_rate`: `Cdi 1714 (2020-01-02 a 2026-10-29)` e `Ipca 81 (2020-01-01 a 2026-09-01)`; `count(*) = 1795 = count(distinct (index_type, reference_date))`. Mesmos números do QA (o SGS não tinha cotação nova). `holiday` 139.
- API do compose: `fixedincome-api` "Up", `GET /api/assets` 200; `fixedincome-db` healthy. Reiniciada por último com `docker compose restart api` (0 `fail:` no log).
- Portas 5124 e 5125 sem processo; banco `qa_tmp` removido; sem variável de proxy deixada no ambiente; nenhum `docker compose down -v`.
- Arquivos temporários, fora do repositório, no scratchpad da sessão: `race.sh`, `race-summary.txt`, `race-1..12-{docker,local}.log`, `qa08-proxy.log`, `b04-before.log`, `b04-after.log`, `app/` (DLL extraída do contêiner) e `repo-copy/` (cópia do B-04).

### Veredito do reteste

**SEM BUGS BLOQUEANTES.** Corrigidos: B-01, B-02, B-03, B-04, B-05, B-06, B-07, B-08, B-09, R-02, PB-01 (comprovado em corrida real: 1 colisão em 12 tentativas, sem falha registrada) e PB-02. QA-04, QA-07, QA-08, QA-10 e QA-11 sem regressão (valores idênticos aos do QA; Calculadora com diferença de R$ 0,00 nos 7 casos).

Pendente: **R-01 NÃO EXECUTADO** (o reteste ocorreu às 01h42 de Brasília, fora da janela 21h-24h). Para fechar, repetir à noite (21h a 24h em Brasília) com `index_rate` vazia e uma posição PreFixed de 2026-10-28, esperando que `elapsedDays` use a data de Brasília. Até lá a correção só tem o teste de unidade como evidência. O NB-01 é ruído de log, de severidade baixa e não bloqueante.

## Correção do NB-01 (upsert atômico), 02/11/2026

Decisão do Matheus após o reteste: em vez de capturar o 23505, reler e repetir, o `UpsertAsync` grava com
`INSERT ... ON CONFLICT (index_type, reference_date) DO UPDATE SET value = EXCLUDED.value`, num único comando por
lote (arrays do Npgsql com `unnest`). Escolhi `DO UPDATE` e não `DO NOTHING` porque a spec exige que a importação
atualize o valor de uma cotação já gravada. Cotação repetida no mesmo lote vale pela última (o PostgreSQL rejeita,
21000, um `DO UPDATE` que atinge a mesma linha duas vezes). O tratamento do 23505 e a repetição foram removidos.

**O que o teste automatizado prova e o que não prova.** O provedor InMemory não executa SQL. Os testes novos
(`IndexRateRepositoryTests`, 4) usam o provedor do PostgreSQL sem banco, com interceptadores que impedem a conexão e
capturam o comando: provam o texto do SQL (`INSERT ... ON CONFLICT ... DO UPDATE`), um único comando por lote, os
parâmetros (id, indexador como texto, data, valor), o colapso de duplicatas e que a coleção vazia não executa nada.
Eles falharam antes da mudança (a implementação anterior lia e salvava) e passam depois. **O efeito no banco não é
provado por teste automatizado**: sai de duas conferências manuais abaixo e passa a ter teste com Testcontainers na F9.
Os 4 testes de repositório que verificavam idempotência, atualização do valor, inserção só das datas novas e
independência entre indexadores em InMemory (mais o de coleção vazia, reescrito, e os 5 do tratamento do 23505) foram removidos, porque exercitavam o código
antigo e não rodam com SQL bruto. Os testes de leitura do repositório passaram a semear os dados direto no contexto.

**Semântica do SQL no PostgreSQL real** (transação com `ROLLBACK`, sobre tabela temporária com o mesmo esquema de
`index_rate`; `index_rate` real intacta com 1795 linhas): primeira carga insere; segunda carga com a mesma chave, id
novo e valor novo **mantém o id original e atualiza o valor**, e a data nova entra. Resultado: `2025-06-02 | id 1111... |
0.070000` e `2025-06-03 | id 3333... | 0.080000`.

**Corrida das duas instâncias** (`race2.sh` no scratchpad; a mesma lógica do `race.sh` com a contagem de linhas
`fail:` por instância; contêiner reconstruído e `app/` republicado com o código novo). 12 tentativas, intervalos de 0,6
a 2,0 s, ordem alternada. A primeira execução do script **não valeu** para o lado do contêiner: o `docker logs --since`
recebeu um horário UTC sem `Z` e devolveu log vazio. O script foi corrigido (sufixo `Z` e aviso se o log vier vazio) e a
corrida refeita; os números abaixo são da segunda execução, com os 24 logs não vazios.

| Verificação | Resultado nas 12 tentativas |
|---|---|
| `23505`, `DbUpdateException`, `PostgresException` ou `fail:` de `Microsoft.EntityFrameworkCore` em qualquer log (contêiner e local) | **0** |
| Banco no fim (`count(*)` / `count(distinct (index_type, reference_date))`) | 1795 / 1795 em todas |
| Importações que se sobrepuseram | t1, t5 e t12 (os dois lados buscaram a série de CDI inteira) |
| Linhas `fail:` em geral | **3**, nenhuma do banco (ver abaixo) |

**O critério "0 linhas `fail:`" não foi atingido ao pé da letra**, por causa do Banco Central, não do upsert:

- **t5, contêiner e instância local, ao mesmo tempo:** `fail: Polly[0] ... OnTimeout` (a tentativa da série 12 passou do
  timeout de 10 s do SGS) seguido de `warn: Polly[3]` com retry; a importação concluiu na tentativa seguinte (`1714 de
  CDI`). O `fail:` é o evento de timeout do pipeline de resiliência.
- **t2, instância local:** `fail: ...ImportIndexRatesUseCase: Falha ao importar as cotações de Cdi.` com
  `JsonException: '<' is an invalid start of a value`: o SGS devolveu uma página HTML no lugar do JSON. Ver NB-02.

Sem essas três linhas, a contagem do que é do banco ou do EF Core é zero. Não repeti a corrida para tentar uma rodada
sem timeouts do SGS, porque isso não mudaria a conclusão sobre o upsert.

### NB-02 (nova, baixa): o SGS pode responder com HTML e a importação falha sem nova tentativa

Na t2 o SGS respondeu com corpo HTML à série 12 (`'<' is an invalid start of a value`). Não é o caso do PB-02 (corpo
JSON de erro): o provedor desserializa, lança `JsonException` e o `ImportIndexRatesUseCase` registra falha; o retry do
Polly só trata falha de transporte e status HTTP de erro. A importação seguinte só vem 6 h depois (PB-03). Impacto: baixo,
sem perda de dados. Pendência para decisão do Matheus: tratar corpo que não é JSON como falha transitória (retry) ou
manter. Teste sugerido: `Resposta_com_corpo_html_do_sgs_e_tratada_como_falha_transitoria`.

### Situação do NB-01

**CORRIGIDO na origem**: o `INSERT ... ON CONFLICT` não gera violação de índice único, então não há mais o que o EF Core
registre em `fail:` por colisão. Nas 12 tentativas com sobreposição real nenhum 23505 nem log de erro do EF Core
apareceu (antes da mudança, 1 colisão em 12, que gerava dois `fail:`). Limite da evidência: a colisão em si não
reproduziu nesta rodada, por construção (o banco resolve o conflito), então a prova é a ausência de erro com
sobreposição comprovada, somada à semântica verificada no PostgreSQL.

### Estado final do ambiente

`index_rate`: `Cdi 1714 (até 2026-10-29)` e `Ipca 81 (até 2026-09-01)`, 1795 linhas e 1795 distintas; API do compose
"Up" com a imagem nova; porta 5124 livre.

## Correção do NB-02 e cobertura de Infrastructure, 02/11/2026

### NB-02: corpo que não é JSON vira falha transitória

**CORRIGIDO.** Resposta de sucesso do SGS cujo corpo não começa com `{` ou `[` (HTML, texto, corpo vazio) agora faz o
Polly tentar de novo.

- **Por que um handler e não o provedor.** O Polly só enxerga o que passa pelo pipeline do `HttpClient`. A
  `JsonException` lançada pelo `SgsIndexRateProvider` depois de ler a resposta nunca disparava retry. O
  `SgsNonJsonResponseHandler` (novo) fica dentro do pipeline: em `DependencyInjection` ele é registrado **depois** do
  `AddStandardResilienceHandler()`, e o primeiro handler registrado é o mais externo. Ele bufferiza o corpo, confere o
  primeiro caractere e, se não for JSON, lança `HttpRequestException` (status e início do corpo, até 100 caracteres na
  mensagem), que o retry padrão trata como transitória. Status de erro (404, 5xx, 400...) passa sem alteração para o
  provedor decidir, como antes.
- **Testes, escritos antes** (`SgsNonJsonResponseHandlerTests`, 14 casos). Com um esqueleto do handler sem
  comportamento, 7 falharam pelo motivo certo (`JsonException` escapando do pipeline, sem repetir) e 7 já passavam como
  guardas; depois da implementação os 14 passam.
  - `Resposta_com_corpo_html_do_sgs_e_tratada_como_falha_transitoria`: pelo registro de produção
    (`AddInfrastructure`, trocando só o handler primário por um stub e zerando o intervalo do retry), a 1ª tentativa
    recebe HTML e a 2ª o JSON; o provedor devolve a cotação com 2 requisições. É o teste que prova a **ordem** do pipeline.
  - HTML em todas as tentativas: `HttpRequestException` depois de 4 requisições (1 + 3 repetições).
  - JSON malformado que começa com `[` ou `{` **não** é repetido (1 requisição, `JsonException`).
  - Handler isolado: JSON e o corpo de erro 404 passam sem alteração e continuam legíveis; `""`, `"   "`, texto solto e
    `<!DOCTYPE html>` com 200 lançam; 404, 502 e 400 com HTML passam intactos; a mensagem é limitada.
- **Smoke contra o SGS real** (API reconstruída): a importação com o pipeline novo concluiu as duas séries (404 tratado
  como "sem cotações"), 0 linhas `fail:` no log, `index_rate` em 1795/1795. **Limite:** o HTML do SGS é intermitente e não
  consegui provocá-lo no SGS real; a prova do retry em HTML é o teste automatizado, não uma reprodução ao vivo.
- **Efeito colateral aceito:** um 200 com corpo vazio também passa a ser repetido (antes era `JsonException` sem retry).

### Cobertura de Infrastructure recuperada

Testes adicionados para os desvios que faltavam (método do CI, um projeto por vez, Release):

| Desvio | Teste |
|---|---|
| `SgsIndexRateProvider`: `?? new List` quando o JSON é `null` | `Resposta_200_com_json_null_devolve_lista_vazia` |
| `SgsIndexRateProvider`: objeto sem `erro`, `erro` que não é objeto, sem `statusCode`, `statusCode` texto, `statusCode` que não cabe em int | `Resposta_200_com_objeto_que_nao_e_o_corpo_de_erro_404_continua_sendo_falha` (5 casos) |
| `DependencyInjection` (nunca executado em Infrastructure) | `DependencyInjectionTests`: registro de repositórios e provedor; job registrado só com `Enabled=true`; desligado com `false`, `talvez`, vazio e ausente |
| `IndexRatesImportJob`: cancelamento durante a execução do caso de uso | `Cancelamento_durante_a_execucao_do_caso_de_uso_encerra_o_job_sem_log_de_erro` |

Também removi do `IsNotFoundErrorBody` a checagem `RootElement.ValueKind == Object`, que era inalcançável (o texto já
começa com `{`, então a raiz só pode ser objeto); o comportamento não muda.

| Camada | Testes | Linha | Branch | Antes (linha / branch) |
|---|---|---|---|---|
| Domain | 292 | 95,2% | 98,1% | 95,2% / 98,1% |
| Application | 105 | 92,5% | 98,9% | 92,5% / 98,9% |
| **Infrastructure** | **110** | **95,6%** | **98,1%** | 88,9% / 78,3% |
| Api | 51 | 96,3% | 89,1% | 96,3% / 89,1% |

Total: **558 testes**, `dotnet build` com 0 avisos. **O que sobra descoberto em Infrastructure:** um único desvio, o
`false` de `while (await timer.WaitForNextTickAsync(...))` em `IndexRatesImportJob` (só ocorre se o timer for
descartado, o que o código nunca faz; não é testável sem alterar o código). O `FixedIncomeDbContextFactory` (fábrica de
design-time do `dotnet ef`) continua sem execução nos testes e entra na conta de linha.

## R-01 ao vivo (fechamento da pendência), 09/11/2026

Executado no commit `e9c93ca` (branch `f7-r01-ao-vivo`, working tree limpo), entre 01h00 e 01h01 UTC de 10/11, ou seja, 22h00 de 09/11 em
Brasília: dentro da janela 21h-24h, com as duas datas diferentes (UTC 2026-11-10, Brasília 2026-11-09).

**Ambiente (descartável):** contêiner `qa-r01-db` (`postgres:16-alpine`, `--rm`, sem volume nomeado, porta 55432 só no loopback, banco `qar01`).
Migrations aplicadas com `dotnet ef database update`: `index_rate` com **0 linhas** (conferido por `count(*)` antes e depois das chamadas) e 139 feriados
(semeados pela migration). API do working tree via `dotnet run --no-launch-profile` na porta 5190, `IndexRatesImport__Enabled=false`; o log de
inicialização confirma "Importação de cotações desligada" e a tabela continuou vazia até o fim. O `fixedincome-db` e o `fixedincome-api` do
desenvolvedor não foram tocados.

```
POST /api/assets      {"name":"QA-R01-LIVE","issuer":"QA-Banco","assetType":"Cdb","indexType":"PreFixed","rate":12,"issueDate":"2026-01-01","maturityDate":"2030-12-31"}
-> 201, id f20551e2-ea89-4459-ac0d-f33180bda720
POST /api/positions   {"assetId":"f20551e2-...","investedAmount":1000,"applicationDate":"2026-10-28"}
-> 201, id 95c7d888-4325-4c17-807d-39478de37beb
```

| Chamada | Hora | elapsedDays | businessDays | grossAmount | iofRate | netAmount |
|---|---|---|---|---|---|---|
| `GET /api/positions/{id}/projection` (sem data) | 01:00:11Z UTC / 22:00:12 Brasília | **12** | 7 | **1003.15** | 60 | 1000.98 |
| mesma, `referenceDate=2026-11-09` (Brasília) | idem | 12 | 7 | 1003.15 | 60 | 1000.98 |
| mesma, `referenceDate=2026-11-10` (UTC) | idem | 13 | 8 | 1003.60 | 56 | 1001.22 |
| `GET /api/portfolio/summary` (sem data) | 01:00:12Z UTC / 22:00:12 Brasília | - | - | **totalGrossAmount 1003.15** | totalIofAmount 1.89 | totalNetAmount 1000.98 |
| mesma, `referenceDate=2026-11-09` (Brasília) | idem | - | - | 1003.15 | 1.89 | 1000.98 |
| mesma, `referenceDate=2026-11-10` (UTC) | idem | - | - | 1003.60 | 2.02 | 1001.22 |

Todas as respostas foram 200. **Resultado: OK.** A projeção e o resumo sem `referenceDate` bateram, valor a valor, com a data explícita de Brasília
(2026-11-09) e divergiram da data UTC (2026-11-10), que daria um dia a mais de rendimento (R$ 1003,60 contra R$ 1003,15) e uma faixa de IOF
abaixo (56% contra 60%). É o oposto do que o QA de 31/10 mostrou com o código anterior (`elapsedDays 3` às 22h37 de Brasília, data UTC); a correção
(`BrasiliaTimeProvider` + `GetLocalNow()` nos dois casos de uso) fecha o R-01 também fora do teste de unidade.

**Limpeza (primeira execução):** API encerrada (porta 5190 sem listener); contêiner `qa-r01-db` parado e removido pelo `--rm`, junto com o volume anônimo e todos os dados
(título e posição `QA-R01-LIVE` incluídos). Permanecem apenas `fixedincome-api` e `fixedincome-db`, e `git status` ficou limpo antes desta edição.

### Repetição com a imagem do Dockerfile (host em UTC)

Mesmo roteiro, mesmo commit (`e9c93ca`), entre 01h35:03 e 01h35:06 UTC de 10/11 (22h35 de 09/11 em Brasília), para separar o `BrasiliaTimeProvider` do
fuso do host (a máquina da primeira execução tem o Windows em Brasília, então o resultado dela sozinho não distinguia as duas coisas).

**Ambiente (descartável):** imagem `qa-r01-api:test` construída com `docker build .` (Dockerfile da raiz, sem alteração), contêiner `qa-r01-api`
(`--rm`, `127.0.0.1:5190->8080`) e banco `qa-r01-db` (`postgres:16-alpine`, `--rm`) na rede própria `qa-r01-net`. A API usa
`ConnectionStrings__DefaultConnection=Host=qa-r01-db;...`, `IndexRatesImport__Enabled=false` e `ASPNETCORE_ENVIRONMENT=Development`. Migrations aplicadas
com `dotnet ef` pelo host (porta 55432 no loopback). Antes das chamadas: `index_rate` com 0 linhas e 139 feriados; depois: 0 linhas. O log da API
confirma "Importação de cotações desligada".

```
$ docker exec qa-r01-api date
Tue Nov 10 01:34:54 UTC 2026
$ docker exec qa-r01-api date -u
Tue Nov 10 01:34:54 UTC 2026
$ docker exec qa-r01-api sh -c 'echo "TZ=[$TZ]"; ls -l /etc/localtime'
TZ=[]
lrwxrwxrwx 1 root root 27 Oct 27 00:00 /etc/localtime -> /usr/share/zoneinfo/Etc/UTC
```

O contêiner está em UTC (`date` igual a `date -u`, `TZ` vazio, `/etc/localtime` aponta para `Etc/UTC`) e a imagem traz o tzdata (`/usr/share/zoneinfo`
existe). Título `QA-R01-LIVE-UTC` (CDB PreFixed 12%) e posição de R$ 1.000 com aporte em 2026-10-28.

| Chamada | Hora (relógio do contêiner) | elapsedDays | businessDays | grossAmount | iofRate | netAmount |
|---|---|---|---|---|---|---|
| `GET /api/positions/{id}/projection` (sem data) | 01:35:04Z UTC / 22:35:04 Brasília | **12** | 7 | **1003.15** | 60 | 1000.98 |
| mesma, `referenceDate=2026-11-09` (Brasília) | idem | 12 | 7 | 1003.15 | 60 | 1000.98 |
| mesma, `referenceDate=2026-11-10` (UTC) | idem | 13 | 8 | 1003.60 | 56 | 1001.22 |
| `GET /api/portfolio/summary` (sem data) | 01:35:05Z UTC / 22:35:05 Brasília | - | - | **totalGrossAmount 1003.15** | totalIofAmount 1.89 | totalNetAmount 1000.98 |
| mesma, `referenceDate=2026-11-09` (Brasília) | idem | - | - | 1003.15 | 1.89 | 1000.98 |
| mesma, `referenceDate=2026-11-10` (UTC) | idem | - | - | 1003.60 | 2.02 | 1001.22 |

Todas as respostas foram 200. **Resultado: OK.** Com o host em UTC, a projeção e o resumo sem `referenceDate` continuam batendo valor a valor com a data
de Brasília (2026-11-09) e divergindo da data UTC (2026-11-10). Os números são idênticos aos da execução no host Windows. Como o fuso local do
contêiner é UTC, um `GetLocalNow()` sem o `BrasiliaTimeProvider` daria 2026-11-10; quem levou a 2026-11-09 foi o `BrasiliaTimeProvider`. A pendência
fica fechada em host UTC e em host com fuso de Brasília.

**Limpeza (repetição em contêiner):** contêineres `qa-r01-api` e `qa-r01-db` parados e removidos pelo `--rm` (com o volume anônimo e todos os dados, título
e posição incluídos), rede `qa-r01-net` removida e imagem `qa-r01-api:test` apagada. Sobraram apenas `fixedincome-api` e `fixedincome-db`, as mesmas
redes e imagens de antes e o mesmo número de volumes (3). Portas 5190 e 55432 sem listener. As camadas de cache do build (`docker build`) são de imagens
base que já existiam na máquina e não foram podadas.
