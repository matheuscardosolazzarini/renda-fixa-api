# QA exploratório — F12, Parte B (pilha isolada, cotações controladas)

Relatório do `qa-exploratorio`. Roteiro: `docs/QA-F12-ROTEIRO.md` (Parte B, QA12-B00 a QA12-B14). O
coordenador consolida este arquivo com o da Parte A em `docs/QA-F12.md`.

## Ambiente

- **Data:** 27/11/2026 (sexta), de 07:28 a 07:37 UTC (04:28 a 04:37 em Brasília).
- **Commit-base:** `26eec24` (branch `f12-contrato-front-v2`), com as alterações não commitadas da F12 na
  árvore. A imagem `renda-fixa-api-api:latest` foi montada com esse código pela preparação comum.
- **API:** contêiner `qaf12-api` (porta 28112, `Server: Kestrel`, `IndexRatesImport__Enabled=false`,
  proxy inválido para a rede). Banco: `qaf12-db` (banco `qaf12`), cópia de `holiday`, `index_rate`
  e `__EFMigrationsHistory` do Compose, backup em `qa_index_rate_bkp`. O Compose (`fixedincome-*`,
  porta 8080) não foi tocado.
- **Leituras no início e no fim da Parte B** (iguais nas duas):

| Símbolo | Valor |
|---|---|
| T (hoje em Brasília) | 2026-11-27 (sexta); cabeçalho `Date` da API `Fri, 27 Nov 2026 07:36:59 GMT` |
| P (dia útil anterior) | 2026-11-26 |
| L (última cotação de CDI) | 2026-11-25, taxa 0,050788 (16/11 em diante) |
| D (data padrão) | 2026-11-26 (`assets/{VAZIO}/projection` sem data) |
| M (último IPCA) | 2026-10 (0,14); μ = 0,38 |

- **Manipulação de `index_rate`:** só em `qaf12-db`, com as funções `restaura`, `cdi_ate`, `tira` e
  `sintetico` do roteiro. Cada cenário começou e terminou com `restaura`, e a prova é a saída
  `cotações restauradas` (a função compara `select index_type, count(*), max(reference_date)` com a
  linha de base `Cdi|1731|2026-11-25` e `Ipca|82|2026-10-01`, 1.813 linhas no total). A cotação
  sintética de 26/11 (CDI 0,050788) só existe no B01, marcada como tal.
- **Compose intocado:** a foto de `index_rate` do Compose depois da Parte B é idêntica à de antes
  (`Cdi|1731|2020-01-02|2026-11-25|17355e0298441e2b3a73641df5a7546c` e
  `Ipca|82|2020-01-01|2026-10-01|66fcca300c66e3602ff7be8e5e788ab9`). `git status --short` igual ao de
  antes da sessão.
- **Conferência dos números:** os esperados da tabela do roteiro (T = 27/11, L = 25/11) valeram sem
  recálculo. O recálculo independente foi feito com `cdi.mjs` e `ipca.mjs` (leem o texto do número).

## Resumo

| Classificação | Cenários |
|---|---|
| OK | B00, B01, B02, B03, B04 (com RISCO), B05 (com RISCO), B06, B07 (com RISCO), B08, B09, B10 (com RISCO), B11, B14 |
| BUG | nenhum |
| RISCO (baixa) | R-B1 a R-B4, abaixo |
| NÃO EXECUTADO | B12 (janela 21h-24h), B13 (dia útil); o sub-caso 6-bis do B06 (T em dezembro) |

Contagem de cenários: **13 OK** (4 deles com um RISCO de baixa gravidade anotado), **0 BUG**,
**2 NÃO EXECUTADO** (B12 e B13), mais um sub-caso do B06. Contagem de RISCO: **4**, todos de
severidade baixa (decisão 6 do roteiro: caso sem regra na spec).

**Veredito da Parte B: SEM BUGS BLOQUEANTES.**

## QA12-B00 — Linha de base e carteira da Parte B: OK

Manipulação: nenhuma (`restaura` no início: `DELETE 1813, INSERT 0 1813`, `cotações restauradas`).
Títulos e aportes criados com o prefixo `QA-F12B` (ids na seção "Dados criados").

| Requisição | Resposta (trecho) | Esperado |
|---|---|---|
| `GET /api/assets/{BVAZIO}/projection` | 200, `referenceDate 2026-11-26`, `positionCount 0`, totais 0, `positions []` | D = 26/11 |
| `GET /api/positions/{BP1}/projection?referenceDate=2026-11-30` | 200, bruto 1004.58, IOF 2.43, IR 0.48, líquido 1001.67, 14/9, `isProjected true` | igual à Parte A |
| `GET /api/indexes/Cdi/accumulated?from=2026-09-01&to=2026-10-01` | 200, `effectiveTo 2026-10-01`, `1.081673` | `1.081673` |
| `GET /api/positions/{BP3}/projection?referenceDate=2026-11-03` | 1006.27, `isProjected false` | 1.006,27, false |
| `GET /api/positions/{BP3}/projection?referenceDate=2026-11-04` | 1006.71, `isProjected true` | 1.006,71, true |

## QA12-B01 — Mudança de comportamento 2 com L controlado: OK

Requisições: as quatro colunas (`2026-11-30`, `2026-11-27`, `2026-11-26`, `2026-11-25`, sem data) em
`/api/positions/{BP1}/projection`, `/api/assets/{BCDI}/projection`, `/api/portfolio/summary` e, para
a regra "PreFixed nunca falha", `/api/assets/{BPRE}/projection`. Mais o acumulado
`Cdi/accumulated?from=2026-11-16&to=2026-11-30`.

**L = 26/11 (T−1), `sintetico 2026-11-26 0.050788`** (`Cdi|1732|2026-11-26`, 1.814 linhas; a
listagem da API mostrou a linha nova):

| `referenceDate` | posição | título | resumo |
|---|---|---|---|
| 2026-11-30 | 200 1004.58, true | 200, `P1E0` | 200, 3022.10, `isProjected true` |
| 2026-11-27 (T) | 200 1004.07, **false** | 200, `P0E0` | 200 |
| 2026-11-26 (P) | 200 1003.56, false | 200 | 200 |
| sem data | 200, `referenceDate 2026-11-27` (D), 1004.07, false | 200, ref 27/11 | 200, ref 27/11 |

O acumulado respondeu 200, `effectiveTo 2026-11-27`, `0.407027`. `restaura` ok.

**L = 25/11 (T−2), cópia como está:**

| `referenceDate` | posição | título | resumo |
|---|---|---|---|
| 2026-11-30 | 200 1004.58, true | 200 true | 200 |
| 2026-11-27 (T) | 200 1004.07, **true** | 200 `P1E0` | 200 |
| 2026-11-26 (P) | 200 1003.56, false | 200 false | 200 |
| sem data | 200, `referenceDate 2026-11-26`, 1003.56, false | 200 | 200 |

Acumulado: `effectiveTo 2026-11-26`, `0.356058`. `restaura` ok.

**L = 24/11 (T−3), `cdi_ate 2026-11-24`** (`Cdi|1730|2026-11-24`, 1.812 linhas):

| `referenceDate` | posição | título (CDI100) | resumo |
|---|---|---|---|
| 2026-11-30, 11-27, 11-26 | **400** "Cotação de CDI ausente para 2026-11-25. A importação de cotações pode estar desligada (IndexRatesImport:Enabled) ou ainda não ter rodado." | **400** "Posição 21278677-1d76-4a66-8cab-2f2c5743291c (título 'QA-F12B CDI100'): Cotação de CDI ausente para 2026-11-25. A importação … ainda não ter rodado." | 400, mesma mensagem do título |
| 2026-11-25 e sem data (D = 25/11) | 200 1003.05, false | 200 false | 200 false |

O título PreFixed `QA-F12B PRE10` respondeu 200 em todas as linhas (sem falha, `P0E0`). Acumulado:
200, `effectiveTo 2026-11-25`, `0.305115` (o acumulado não olha T). `restaura` ok.

**L = 19/11 (antes do feriado), `cdi_ate 2026-11-19`** (`Cdi|1728|2026-11-19`, 1.810 linhas):
`referenceDate` 11-30, 11-27, 11-26 e 11-24: 400 citando **2026-11-23** em posição, título (com
posição e título) e resumo; PreFixed 200. `11-23` e sem data (D = 23/11): 200 (bruto 1002.03, IOF 1.54,
IR 0.11, líquido 1000.38, `isProjected false`); `11-20` (feriado): 200. Acumulado: 200,
`effectiveTo 2026-11-23`, `0.203307`. `restaura` ok.

Esperado atendido em todas as linhas: nenhuma divergência da tabela do roteiro, o 400 cita a posição e
o título no resumo e na projeção por título, e o aviso da importação aparece nos três.

## QA12-B02 — Feriado logo depois da última cotação: OK

Manipulação: `cdi_ate 2026-11-19` (L = 19/11, quinta; 20/11 feriado; D = 23/11).

| Requisição | Resposta | Esperado |
|---|---|---|
| `Cdi/accumulated?from=2026-11-16&to=2026-11-20` | 200 `effectiveTo 2026-11-20`, `0.203307` | idem |
| `…&to=2026-11-21` | 200 `effectiveTo 2026-11-21`, `0.203307` | idem |
| `…&to=2026-11-23` | 200 `effectiveTo 2026-11-23`, `0.203307` | idem |
| `…&to=2026-11-24`, `…&to=2026-11-25`, `…&to=2026-11-26` | 200 `effectiveTo 2026-11-23`, `0.203307` | corte em D |
| `…&to=2051-01-02` | 200 `effectiveTo 2026-11-23` | sem erro |
| `from=2026-11-23&to=2026-11-26` | 400 "Não há cotação de CDI publicada no período de 2026-11-23 a 2026-11-26." | período sem cotação |
| `from=2026-11-24&to=2026-12-10` | 400 "Não há cotação de CDI publicada no período de 2026-11-24 a 2026-12-10." (não a do calendário; lacuna O-1 fechada) | idem |
| `from=2026-11-16&to=2026-11-19` | 200 `0.152441` (sem o dia 19, fim exclusivo) | — |
| `positions/{BP1}/projection` sem data | 200 `referenceDate 2026-11-23`, 1002.03, IOF 1.54, IR 0.11, líquido 1000.38, 7/4, `isProjected false` | caso 3a |
| `positions/{BP1}/projection?referenceDate=2026-11-24` | 400 "Cotação de CDI ausente para 2026-11-23. …" | 400 citando 23/11 |

`restaura` ok (`DELETE 1810, INSERT 0 1813`).

## QA12-B03 — Lacunas antes da última cotação: OK

Posição de apoio: `BP1` (CDI100, 16/11). Respostas na ordem da tabela do roteiro.

| Montagem | Chamada | Resposta |
|---|---|---|
| `tira Cdi 2026-11-18` (`Cdi|1730|2026-11-25`) | `positions/{BP1}/projection?referenceDate=2026-11-19` | 400 "Cotação de CDI ausente para 2026-11-18. A importação de cotações pode estar desligada (IndexRatesImport:Enabled) ou ainda não ter rodado." |
| idem | `…?referenceDate=2026-11-18` | 200 (bruto 1001.02, 2/2), `isProjected false` |
| idem | `…?referenceDate=2026-11-17` | 200 1000.51, false |
| idem | `…?referenceDate=2026-11-30` e sem data | 400 citando 2026-11-18 (a lacuna antes de L não vira projeção) |
| idem | `Cdi/accumulated?from=2026-11-16&to=2026-11-19` | 400 "Cotação de CDI ausente para 2026-11-18." |
| idem | `…&to=2026-11-18` | 200 `0.101602` |
| idem | `…&to=2026-11-30`, título sem data, resumo sem data | 400 citando 2026-11-18 (título e resumo com a posição e o título) |
| `restaura; tira Cdi 2026-11-24` | `…?referenceDate=2026-11-30`, `…11-25` e sem data | 400 citando 2026-11-24 |
| idem | `…?referenceDate=2026-11-24` | 200 (bruto 1002.54) |
| `restaura; tira Cdi 2023-05-10` | `positions/{BP1}/projection?referenceDate=2026-11-30` | 200, igual ao B00 (1004.58, true) |
| idem | `Cdi/accumulated?from=2023-05-01&to=2023-06-01` | 400 "Cotação de CDI ausente para 2023-05-10." |
| idem, com aporte antigo | ver nota | ver nota |

Nota do aporte antigo: o roteiro manda `BOLD=$(aporte $BCDI 1000 2023-05-02)`, mas o `QA-F12B CDI100`
tem emissão em 2026-01-01 e o aporte foi recusado (400 "ApplicationDate deve estar entre a emissão e o
vencimento do título."), comportamento correto. Substituição: título `QA-F12B CDI ANTIGO` (emissão
2020-01-01, vencimento 2030-01-02) com um aporte em 2023-05-02 (`BOLD`) e outro em 2026-11-16 (`BOLD2`).
Com `tira Cdi 2023-05-10`:

| Requisição | Resposta |
|---|---|
| `positions/{BOLD2}/projection?referenceDate=2026-11-30` (aporte recente) | 200, 1004.58, true |
| `positions/{BOLD}/projection?referenceDate=2026-11-30` (aporte antigo) | 400 "Cotação de CDI ausente para 2023-05-10. …" |
| `assets/{BOLDA}/projection?referenceDate=2026-11-30` e `portfolio/summary?referenceDate=2026-11-30` | 400 "Posição 25a70498-… (título 'QA-F12B CDI ANTIGO'): Cotação de CDI ausente para 2023-05-10. …" |
| `assets/{BOLDA}/projection?referenceDate=2023-05-09` e `2023-05-10` | 200 (o intervalo não inclui o dia 10) |
| `assets/{BOLDA}/projection?referenceDate=2023-05-11` | 400 citando 2023-05-10 |

Nenhum 200 com lacuna dentro do intervalo; nenhum 400 por lacuna fora do intervalo.

## QA12-B04 — CDI antes da primeira cotação importada: OK, com RISCO (R-B2)

Manipulação: `delete from index_rate where index_type='Cdi' and reference_date < '2020-02-03'` (22
linhas; `Cdi|1709`).

| Requisição | Resposta |
|---|---|
| `Cdi/accumulated?from=2020-01-02&to=2020-03-02` | 400 "Cotação de CDI ausente para 2020-01-02." |
| `Cdi/accumulated?from=2020-02-03&to=2020-03-02` | 200 `0.293729` |
| `Cdi/accumulated?from=2020-01-31&to=2020-02-05` | 400 "Cotação de CDI ausente para 2020-01-31." |
| aporte em 2020-01-02 (`QA-F12B CDI ANTIGO2`), projeção em 2020-03-02 | 400 "Cotação de CDI ausente para 2020-01-02. A importação … ainda não ter rodado." (resumo e título com a posição e o título) |

RISCO R-B2 (ver abaixo): a mensagem não diz que a série começa depois. `restaura` ok.

## QA12-B05 — Banco sem nenhuma cotação de CDI: OK, com RISCO (R-B2)

Manipulação: `delete from index_rate where index_type='Cdi'` (1.731 linhas; só `Ipca|82` ficou; a
listagem `GET /api/indexes/Cdi?from=2026-11-01&to=2026-11-30` devolveu `data: []`).

| Chamada | Resposta |
|---|---|
| `assets/{BVAZIO}/projection` sem data | 200, `referenceDate 2026-11-27` (T) |
| `assets/{BPRE}/projection` sem data | 200, `referenceDate 2026-11-27`, 1003.03 |
| `positions/{BP2}/projection` (PreFixed) sem data; com `2026-12-31` | 200 ref 27/11 (1003.03); 200 ref 31/12 (1011.79) |
| `positions/{BP3}/projection` (IPCA+) sem data | 200, 1013.67, `isProjected true`, 57/38 (data padrão T, novembro no intervalo) |
| `positions/{BP1}/projection` sem data, com `2026-12-31`, com `2026-11-17` | 400 "Cotação de CDI ausente para 2026-11-16. A importação … ainda não ter rodado." |
| `positions/{BP1}/projection?referenceDate=2026-11-16` (dia do aporte) | 200 com rendimento zero (não exige cotação) |
| `Cdi/accumulated?from=2026-09-01&to=2026-10-01` | 400 "Não há cotação de CDI publicada no período de 2026-09-01 a 2026-10-01." |
| `Ipca/accumulated?from=2026-09-01&to=2026-10-01` | 200 `0.961150` (sem efeito) |
| resumo sem data e com `2026-12-31` | 400 "Posição 21278677-… (título 'QA-F12B CDI100'): Cotação de CDI ausente para 2026-11-16. A importação … ainda não ter rodado." |
| título `{BCDI}` sem data | 400 com a mesma mensagem do resumo |

Observação de método: a execução foi às 04:30 de Brasília (07:30 UTC), quando T e a data UTC são o mesmo
dia. O critério "referenceDate com o dia de UTC" só é distinguível no B12 (não executado). `restaura` ok.

## QA12-B06 — IPCA estimado e projetado juntos, ao vivo: OK

Manipulação: `tira Ipca 2026-10-01` (`Ipca|81|2026-09-01`; M = 09/2026). Aporte `BP3`, R$ 1.000 em
01/10/2026.

| Requisição | Resposta | Esperado |
|---|---|---|
| `positions/{BP3}/projection?referenceDate=2026-12-15` | 200, bruto **1025.42**, rendimento 25.42, IOF 0.00, IR 22,5% **5.72**, líquido **1019.70**, 75/50, `isEstimated true`, `isProjected true` | caso 6 da spec |
| sem data (D = 26/11) | 200, bruto **1020.03**, IR **4.51**, líquido **1015.52**, 56/37, `true`/`true` | idem |
| `referenceDate=2026-11-03` | 200, 1013.11 (`1000 × 1,0082 × 1,06^(21/252)`), `isEstimated true`, `isProjected false` | idem |
| `referenceDate=2026-10-30` | 200 (12.48, IOF 0.37), `isEstimated true`, `isProjected false` | idem |
| `referenceDate=2026-10-31` / `2026-11-04` | 1013.11 `P0E1`; 1013.54 `P1E1` | coerente |
| `Ipca/accumulated?from=2026-09-01&to=2026-10-20` | 200, `effectiveTo 2026-09-30`, `0.820000` | idem (o mês estimado não entra) |
| `ipca.mjs --media` na listagem até 09/2026 | `meses 12 media 0.373571 -> 0.37` | μ = 0,37 |

Resumo (6 posições) com e sem data: 200, `isProjected true`. Sub-caso do 6-bis (T em dezembro): NÃO
EXECUTADO, T é 27/11/2026. `restaura` ok.

## QA12-B07 — Janela de 12 meses incompleta: OK, com RISCOs (R-B1 e R-B3)

Montagens e respostas (aporte `BP3`, 01/10/2026):

**a. `tira Ipca 2025-11-01`** (`Ipca|81`):

| Chamada | Resposta |
|---|---|
| `positions/{BP3}/projection` sem data | 400 "A projeção futura do IPCA exige os 12 meses de 2025-11 a 2026-10; há 11, falta 2025-11." |
| `…?referenceDate=2026-11-03` (e `2026-11-02`) | 200, 1006.27, `isProjected false` |
| `…?referenceDate=2026-11-04` e `2027-06-01` | 400, mesma mensagem |
| `assets/{BIPCA}/projection` sem data; `portfolio/summary` sem data e com `2026-11-04` | 400, **mesma mensagem, sem posição nem título** (R-B1) |
| `assets/{BIPCA}/projection?referenceDate=2026-11-03`; resumo com `2026-11-03` | 200 (resumo 4472.22, `isProjected false`) |
| `Ipca/accumulated?from=2026-01-15&to=2026-09-20` | 200 `3.951320` |
| `Ipca/accumulated?from=2025-10-15&to=2025-12-31` | 400 "Cotação de IPCA ausente para 2025-11." |
| `Ipca/accumulated?from=2025-12-01&to=2026-02-28` | 200 `1.365720` |
| `positions/{BP1}` (CDI) com 11-30 e `positions/{BP2}` (PreFixed) | 200 (sem efeito) |

**b. `tira Ipca 2025-11-01` e `tira Ipca 2026-03-01`** (`Ipca|80`): `positions/{BP3}/projection` sem
data devolveu 400 "… 12 meses de 2025-11 a 2026-10; há 10, falta **2025-11**." (cita o primeiro
ausente, não 2026-03: a lacuna O-1 do Stryker está coberta ao vivo). Com `referenceDate=2026-11-03`:
200, 1006.27.

**c. `tira Ipca 2026-03-01`** (`Ipca|81`): `positions/{BP3}/projection` sem data: 400 "… há 11, falta
2026-03." Com `2026-11-03`: 200. IPCA+ com aporte em 2026-02-02 (`BIPCA2`): sem data e com
`2026-11-03`: 400 **"Cotação de IPCA ausente para 2026-03."** (mensagem de lacuna da RN-10); com
`2026-02-27` e `2026-03-02`: 200. Título `BIPCA` e resumo sem data: 400 com a mensagem da janela (a
posição mais recente, `BP3`, é a primeira a falhar). As duas mensagens citam 2026-03 (R-B3).

**d. só os últimos 12 meses** (`delete … reference_date < '2025-11-01'`, `Ipca|12|2026-10-01`):
`positions/{BP3}/projection` sem data: 200, 1013.24, `isProjected true`; com `2026-12-15`: 200,
1018.65, true. `ipca.mjs --media`: `meses 12 media 0.377749 -> 0.38`. `Ipca/accumulated?from=2025-01-10&to=2025-12-20`:
400 "Cotação de IPCA ausente para 2025-01."; `from=2025-11-10&to=2025-12-20`: 200 `0.510590`. Resumo
sem data: 200.

**e. sem nenhum IPCA** (`delete … index_type='Ipca'`): `positions/{BP3}/projection` sem data e com
`2026-11-03`: 400 "Não há cotação de IPCA publicada para estimar a projeção."; com `2026-10-01` (dia do
aporte): 200 zeros. `Ipca/accumulated?from=2026-01-01&to=2026-10-31`: 400 "Não há cotação de IPCA
publicada no período de 2026-01-01 a 2026-10-31." `positions/{BP2}` e `{BP1}`: 200. `assets/{BIPCA}` e
resumo: 400 com a mesma mensagem de "estimar a projeção".

`restaura` ok depois de cada subcaso.

## QA12-B08 — Lacuna de IPCA antes do último publicado: OK

Manipulação: `tira Ipca 2026-08-01` (`Ipca|81|2026-10-01`).

| Chamada | Resposta |
|---|---|
| `Ipca/accumulated?from=2026-01-15&to=2026-09-20` | 400 "Cotação de IPCA ausente para 2026-08." |
| `Ipca/accumulated?from=2026-09-01&to=2026-10-31` | 200 `0.961150` |
| `Ipca/accumulated?from=2026-08-01&to=2026-08-31` | 400 citando 2026-08; julho sozinho: 200 `0.070000` |
| IPCA+ com aporte em 2026-07-01 (`BIPCA3`), `referenceDate=2026-09-01` e `2026-08-31` | 400 "Cotação de IPCA ausente para 2026-08." |
| `BIPCA3`, `referenceDate=2026-08-01` | 200 (só julho) |
| `positions/{BP3}/projection` sem data | 400 "… 12 meses de 2025-11 a 2026-10; há 11, falta 2026-08." |
| `positions/{BP3}/projection?referenceDate=2026-11-03` | 200 1006.27 |
| título `{BIPCA}` e resumo com `2026-11-03` | 400 "Cotação de IPCA ausente para 2026-08." (por causa de `BIPCA3`) |

Agosto não foi tratado como estimado em nenhuma chamada. `restaura` ok.

## QA12-B09 — Borda do calendário (2050): OK

Títulos `QA-F12B BORDA PRE` e `QA-F12B BORDA CDI` (vencimento 2051-06-01).

- Aporte em 2051-01-02: 400 "Data de aporte 2051-01-02 fora do período coberto pelo calendário
  (2020-01-01 a 2050-12-31)." Aportes de 2050-12-30 e de 2026-11-16 aceitos (201).

| Chamada | Sem data | 2050-12-30 | 2050-12-31 | 2051-01-02 |
|---|---|---|---|---|
| `assets/{BORDA}/projection` | 200, ref 26/11, aporte de 2050 com rendimento zero (1000.00), `P0` | 200, 1000.00 | 200, 1000.38 | 400 calendário |
| `assets/{BORDACDI}/projection` | 200, bruto 2003.56 (o de 16/11 em 1003.56, o de 2050 em 1000.00) | 200, 22495.25 (item de 16/11: 21495.25, IR 15.0%, `P1`) | 200, 22506.68 | 400 calendário |
| `portfolio/summary` (11 posições) | 200, `isProjected true` | 200 (0,15 s) | 200 (0,18 s) | 400 "Data 2051-01-02 fora do período coberto pelo calendário (2020-01-01 a 2050-12-31)." |

Nenhum 500; tempos entre 0,02 e 0,18 s. O resumo e a projeção por título sem data não foram derrubados
pelo aporte de 2050-12-30.

## QA12-B10 — Muitos aportes: OK, com RISCO (R-B4)

Título `QA-F12B MUITOS CDI` com 253 aportes de R$ 100,37 (os 300 datas do roteiro, filtradas até
2026-11-25: 253; **aceitos 253, recusados 0**, inclusive os que caíram em feriado). Sem manipulação de
cotações (`restaura` antes e depois).

| Requisição | Tempo a quente (3 medições) | Invariantes |
|---|---|---|
| `assets/{MANY}/projection` sem data | 0,09 / 0,09 / 0,10 s | `soma.mjs`: totais OK, líquido = bruto − IR − IOF OK, ordem decrescente OK; `positionCount` 253 |
| `…?referenceDate=2026-11-30` | 0,09 / 0,10 / 0,14 s | tudo OK |
| `…?referenceDate=2028-06-30` | 0,68 / 0,69 / 0,80 s | tudo OK |
| `…?referenceDate=2026-06-30` | 0,04 s | tudo OK |
| `portfolio/summary` sem data (264 posições, 162.137 bytes) | 0,05 / 0,06 / 0,06 s | — |
| `portfolio/summary?referenceDate=2026-11-30` | 0,05 s | — |
| `portfolio/summary?referenceDate=2028-06-30` (262 posições) | **1,35 / 1,37 / 1,40 s** | — |
| `portfolio/summary?referenceDate=2050-12-30` | **2,65 / 2,69 / 3,03 s** | — |
| `assets/{MANY}/projection?referenceDate=2050-12-30` | **1,11 / 1,12 / 1,13 s** | — |

`somatitulos.mjs` (soma das projeções de todos os títulos contra o resumo): sete totais iguais,
`isProjected` da raiz = OR dos itens = OR dos títulos, sem data e com `2026-11-30`. `coerencia.mjs` no
título `MANY` em 2026-11-30: os 253 itens iguais à projeção da posição e ao item do resumo, mesmos
aportes, mesma ordem.

RISCO R-B4: tempo acima de 1 s nas projeções longas (ver abaixo).

## QA12-B11 — Carga paralela nas rotas novas: OK

20 clientes simultâneos (`xargs -P 20`), log do `qaf12-api` com 0 ocorrências de `exception|fail:` antes
e depois:

| Rota | Requisições | Resultado |
|---|---|---|
| `Cdi/accumulated?from=2020-01-02&to=2026-12-31` | 200 | 200 × 200 |
| `assets/{MANY}/projection?referenceDate=2026-11-30` | 200 | 200 × 200 |
| `portfolio/summary?referenceDate=2026-12-31` | 200 | 200 × 200 |
| `assets/{MANY}/projection?referenceDate=2028-06-30` | 100 | 200 × 100 |

Memória do contêiner no fim: 179 MiB. Nenhum 500.

## QA12-B12 — Fuso de Brasília na fronteira: NÃO EXECUTADO

Motivo: o teste só distingue "hoje em Brasília" de "hoje em UTC" entre 21h e 24h de Brasília. A
execução foi às 04:28 a 04:37 de Brasília (07:28 a 07:37 UTC) de 27/11/2026, quando os dois dias
coincidem. Não se simulou relógio (regra do roteiro). Risco a registrar: nenhuma das chamadas desta
parte prova que a API usa Brasília; as colunas "se a API usar UTC" do B12 seguem sem prova ao vivo (a
spec cobre o fuso por teste automatizado, segundo a `docs/VALIDACAO-F12.md`).

## QA12-B13 — Hoje em fim de semana ou feriado: NÃO EXECUTADO

Motivo: 27/11/2026 é sexta, dia útil (não há feriado). O caso exige sábado, domingo ou feriado.

## QA12-B14 — Fator exato com R$ 100 milhões: OK

Título `QA-F12B EXATO CDI100` (CDB 100% do CDI, 2020-01-01 a 2030-01-02), dois aportes de R$ 100.000.000,00.

| Requisição | Resposta | Recálculo |
|---|---|---|
| `positions/{E1}/projection?referenceDate=2026-10-01` (aporte 01/09/2026) | `grossAmount 101081673.00` (fator 1,01081673), IR 243376.43, líquido 100838296.57 | `1e8 × 1,01081673` = 101081673.00 |
| `positions/{E2}/projection` sem data (aporte 02/01/2020) | `grossAmount 194579715.00`, `referenceDate 2026-11-26`, `isProjected false` | |
| `Cdi/accumulated?from=2020-01-02&to=2026-11-26` | `effectiveTo 2026-11-26`, `94.579715` | `cdi.mjs` (1731 publicados): fator 1.94579715; `1e8 × 1,94579715` = 194579715.00 |
| `positions/{E2}/projection?referenceDate=2026-12-31` | `grossAmount 196965375.00`, 1755 dias úteis, `isProjected true` | `cdi.mjs 100 1755`: fator 1.96965375 (24 dias projetados), 196965375.00 |
| `positions/{E2}/projection?referenceDate=2027-12-31` | `223737496.00`, 2006 dias úteis | `cdi.mjs 100 2006`: fator 2.23737496 |

Diferença zero em todos os dígitos. Exploração adicional sobre o limite do aporte (o roteiro pede para
registrar se R$ 100 milhões for recusado; não foi):

- `investedAmount` 999.999.999.999, 1e12, 99.999.999.999.999 e 9.999.999.999.999.999,99 aceitos (201); 1e30 e o `decimal.MaxValue`
  recusados com 400 ("O valor do campo 'investedAmount' é inválido." e "InvestedAmount deve ser menor que
  10000000000000000."). O limite é 1e16, exclusivo.
- Projeção do aporte de R$ 9.999.999.999.999.999,99 em 2026-11-26 e em 2050-12-30: 200, sem estouro
  (bruto 19457971499999999.98 e 28810745099999999.97). Resumo e projeção por título com esses aportes:
  200. Nenhum 500.

## RISCOS

Todos de severidade **baixa** (decisão 6 do roteiro: sem regra explícita na spec). Nenhum aparece como
BUG porque a spec não manda o contrário.

- **R-B1 (baixa). Mensagens de IPCA do resumo e da projeção por título não identificam a posição nem o
  título.** B07a e B08. `GET /api/portfolio/summary` (com o IPCA de 2025-11 removido) e
  `GET /api/assets/{BIPCA}/projection` devolvem 400 "A projeção futura do IPCA exige os 12 meses de
  2025-11 a 2026-10; há 11, falta 2025-11." e "Cotação de IPCA ausente para 2026-08.", sem
  "Posição … (título '…')", ao contrário do CDI (B01, B03), que cita a posição e o título. O roteiro
  esperava "no resumo, identificando a posição" na D-4. A spec (D-11, R-10) só manda identificar no caso
  de CDI ausente; por isso é RISCO, não BUG. Efeito para o front: o usuário vê o mês faltante, mas não
  qual título derrubou o resumo.
- **R-B2 (baixa). "Ausente" e "Não há cotação" não dizem que a série pode nunca ter sido importada, e o
  acumulado não traz o aviso da importação.** B03 (acumulado), B04 e B05. `GET /api/indexes/Cdi/accumulated?from=2020-01-02&to=2020-03-02`
  (CDI anterior a 2020-02-03 apagado) responde 400 "Cotação de CDI ausente para 2020-01-02." (sem o
  "A importação … pode estar desligada" que a projeção traz). Com CDI vazio:
  "Não há cotação de CDI publicada no período de 2026-09-01 a 2026-10-01." O texto está correto, mas a
  causa provável (importação parada ou desligada) só aparece na projeção.
- **R-B3 (baixa). Dois 400 diferentes para a mesma lacuna de IPCA.** B07c. Com `tira Ipca 2026-03-01`,
  a projeção por título e o resumo sem data respondem a mensagem da janela ("… há 11, falta 2026-03.")
  para o aporte de 01/10 e "Cotação de IPCA ausente para 2026-03." para o aporte de 02/02. Ambas citam
  o mês; só varia o texto. Não esconde o dado faltante.
- **R-B4 (baixa). Projeção longa passa de 1 s.** B10. Com 262 a 270 posições, o resumo em
  `referenceDate=2028-06-30` leva 1,35 a 1,40 s e em `2050-12-30`, 2,65 a 3,03 s; a projeção por título
  com 253 aportes em 2050-12-30, 1,11 a 1,13 s. A data padrão e `2026-11-30` ficam abaixo de 0,15 s. Não
  há erro nem valor errado; é custo por dia projetado multiplicado pelo número de aportes. Um front que
  peça "projetar até o vencimento" para uma carteira grande verá segundos de espera.

## Lista de bugs (por severidade)

Nenhum bug encontrado nos cenários da Parte B executados.

Nenhum teste automatizado sugerido para correção; para os RISCOS, se o Matheus decidir tratá-los, os
nomes no padrão da skill `testes-xunit` seriam:

- R-B1: `GetPortfolioSummaryUseCaseTests.Janela_de_ipca_incompleta_identifica_a_posicao_e_o_titulo`
- R-B2: `GetIndexAccumulationUseCaseTests.Cotacao_de_cdi_ausente_lembra_que_a_importacao_pode_estar_desligada`
- R-B4: `GetPortfolioSummaryUseCaseTests.Projecao_ate_2050_com_250_aportes_termina_em_ate_1_segundo`

## Dados criados durante o QA (todos em `qaf12-db`; somem com `docker rm -f qaf12-api qaf12-db`)

Títulos `QA-F12B` e aportes (ids):

| Nome | id | Aportes |
|---|---|---|
| QA-F12B VAZIO | `f0aa63ac-065b-4b2a-8d34-50a86f1e974c` | nenhum |
| QA-F12B CDI100 | `22c8dd1f-e36d-436f-912f-038fdb4c53c9` | BP1 `21278677-1d76-4a66-8cab-2f2c5743291c` |
| QA-F12B PRE10 | `7fe7b587-77a7-4cad-b8c4-99e415966581` | BP2 `a8c7c586-6b4a-4dec-a115-47b6e2d22cd4` |
| QA-F12B IPCA6 | `f09a118e-bd41-4aac-a574-e8335a99251a` | BP3 `57d1fd98-1c6f-4a2e-b141-7b05d9958545`; BIPCA2 `dfb63c4b-381a-4054-9736-17c679b96e4b`; BIPCA3 `4e685f73-022e-4265-a82b-af108656a6b8` |
| QA-F12B CDI ANTIGO | `45abf193-c122-474e-9d9b-feb582c7f171` | BOLD `25a70498-5f6a-4f80-9018-e78546ae4f8c`; BOLD2 `48ba427b-3989-4d79-8161-4a3d5517ce7f` |
| QA-F12B CDI ANTIGO2 | `5ebbb459-94a9-40ca-93b8-5b76a37e4951` | BANTP `743ff8b6-cb76-481a-9c88-23bf5bfec27a` |
| QA-F12B BORDA PRE | `edc657a5-d53f-48e0-a164-913802e4b935` | 1 aporte (2050-12-30) |
| QA-F12B BORDA CDI | `837c06ea-008c-4e50-9f93-6ca1996d98ad` | 2 aportes (16/11/2026 e 2050-12-30) |
| QA-F12B MUITOS CDI | `627b7e92-4eda-45ea-9693-d5619ffc5b51` | 253 aportes (ids em `…\scratchpad\qaf12\b\many-ids.txt`) |
| QA-F12B EXATO CDI100 | `05a395e9-5de0-4514-a494-96f8847db79f` | E1 `aaf59931-85c9-4234-949b-a6983f141827`; E2 `1bb52423-8db3-4afc-be2d-86435ea29669`; EMAX `af8e9905-2c25-4684-a2fa-272010327347`; extras (R$ 999.999.999.999, R$ 1.000.000.000.000 e R$ 99.999.999.999.999) `692cc4fb-5c65-4e14-9f17-8f2b216d122d`, `13d666d9-c833-40cd-9972-434b4bcff68d`, `1284387e-c655-4267-ae7c-f7aecd675989` |

Os arquivos de apoio (`ids.env`, `many-ids.txt`, saídas) estão em
`C:\Users\matheuss\AppData\Local\Temp\claude\8c9101ad3d07d8d0\68605f27-c22c-4f25-8ede-b7b3077abf94\scratchpad\qaf12\b`.
`qaf12-api`, `qaf12-db` e a rede `qaf12-net` **não foram removidos** (o coordenador faz a limpeza depois
da triagem). Estado final de `index_rate` em `qaf12-db`: restaurado (`cotações restauradas`, 1.813
linhas, `Cdi|1731|2026-11-25` e `Ipca|82|2026-10-01`).

## Veredito

**SEM BUGS BLOQUEANTES** na Parte B. Pendências de cobertura: B12 e B13 (dependem de janela de horário
ou de dia não útil) e o caso 6-bis do B06 (T em dezembro) ficam `NÃO EXECUTADO`.
