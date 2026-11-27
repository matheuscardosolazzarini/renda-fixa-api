# QA exploratório — F12 (Parte A: API do Compose, cotações reais)

Relatório parcial da Parte A do roteiro `docs/QA-F12-ROTEIRO.md` (cenários QA12-A00 a QA12-A30). A
Parte B (pilha isolada, cotações controladas) é outro relatório (`docs/QA-F12-parteB.md`); o
coordenador consolida os dois em `docs/QA-F12.md`.

## Cabeçalho

| Item | Valor |
|---|---|
| Data | 27/11/2026 (sexta), das 04:28 às 04:36 em Brasília (07:28 a 07:36 UTC) |
| Commit-base | `26eec24aefed9404cbc99c508d133a68f640badf` (branch `f12-contrato-front-v2`) **mais a árvore de trabalho** (a fase não está commitada; `git status --short` igual ao da preparação) |
| API | `fixedincome-api` do Compose, `http://localhost:8080`, `Server: Kestrel`, imagem construída pela preparação comum com o código da fase. Importação de cotações ligada (padrão) |
| Banco | `fixedincome-db` (Matheus, com dados reais e de QAs anteriores). Só títulos e aportes criados pela API; nenhum `insert`/`update`/`delete` direto; `index_rate` só lida |
| Ferramentas | Git Bash, `curl`, Node 26 (scripts `cdi.mjs`, `ipca.mjs`, `soma.mjs`, `coerencia.mjs`, `somatitulos.mjs` do roteiro, que leem o texto do número) |
| Rede | Três consultas à Calculadora do Cidadão (decisão 4), nada mais |

### Leituras de T, P, L, D, M e μ

| Símbolo | No início (04:28 BRT) | No fim (04:35 BRT, A30) |
|---|---|---|
| T | 2026-11-27 (UTC 2026-11-27 07:28:32; cabeçalho `Date` da API `Fri, 27 Nov 2026 07:28:31 GMT`) | 2026-11-27 (cabeçalho `Date` 07:35:18 GMT) |
| P | 2026-11-26 | 2026-11-26 |
| L | 2026-11-25, taxa 0.050788 (CDI de 16/11 em diante a 0.050788) | **2026-11-25, não mudou** |
| D | 2026-11-26 (`referenceDate` do `QA-F12A VAZIO` sem data) | 2026-11-26 |
| M | 2026-10, variação 0.14 | 2026-10, 0.14 |
| μ | 0.377749 -> **0,38** (janela 11/2025 a 10/2026, 12 meses) | 0,38 |

Sobre a atenção (1) do pedido: os esperados de IPCA do roteiro (A13, A21, A22, A23) já usam **μ = 0,38
(M = 10/2026)**; nenhum foi calculado com 0,37. Recalculei de forma independente dois deles com μ =
0,38 (ver A13 e A23) e bateram. Como L não virou 26/11 (atenção 2), todos os casos de CDI da parte
valeram com **L = 25/11**; nenhum esperado dependente de D precisou de ajuste, e a A21 não foi repetida.

Relógio: no horário da execução (04:35 BRT = 07:35 UTC) o dia de calendário é o mesmo em UTC e em
Brasília. **Esta parte não consegue distinguir "hoje em UTC" de "hoje em Brasília"**; a fronteira
fica com a janela das 21h às 24h (B12 da Parte B).

### Resumo

- 31 cenários (A00 a A30): **24 OK, 7 RISCO, 0 BUG, 0 NÃO EXECUTADO** (a abertura do Swagger UI e o
  "Try it out" da A28 ficaram `NÃO EXECUTADO` dentro do cenário, por falta de navegador; o resto
  da A28 está OK).
- Nenhum 500 em nenhuma entrada, nenhum valor diferente do recálculo independente, nenhuma marca
  `isProjected`/`isEstimated` fora da tabela, nenhuma divergência entre item do título, projeção da
  posição e item do resumo (73 linhas de item, A17), nenhuma divergência da soma dos títulos contra o
  resumo (5 datas, 89 títulos, A18).
- Os sete RISCO são todos da decisão 6 (sem regra na spec, severidade **baixa**) ou do tipo
  documentação.

**Veredito: SEM BUGS BLOQUEANTES.**

---

## QA12-A00 — Reconhecimento: **OK**

```
GET /health                                                   -> 200, Server: Kestrel
GET /api/indexes/Cdi?from=2026-11-01&to=2026-12-31            -> último item 2026-11-25, 0.050788
GET /api/indexes/Ipca?from=2025-09-01&to=2026-12-31           -> último item 2026-10-01, 0.140000
node ipca.mjs --media (sobre a listagem acima)                -> meses 12 media 0.377749 -> 0.38
GET /api/assets/e971a3ce-59e6-48e2-bb20-51311d2a7cea/projection  (QA-F12A VAZIO)
  -> 200, ..., "positionCount":0, "referenceDate":"2026-11-26","positions":[]
GET /api/indexes/Cdi/accumulated?from=2026-09-01&to=2026-10-01
  -> 200 {"indexType":"Cdi","from":"2026-09-01","to":"2026-10-01","effectiveTo":"2026-10-01","accumulatedPercentage":1.081673}
GET /api/portfolio/summary                                    -> 200, raiz "isProjected":true (carteira real com IPCA+)
```

D = 26/11, dia útil seguinte a L (25/11), como manda o roteiro. O resumo da carteira real **responde
200** (nada de QACF-R2 hoje), então a matriz da A24 e a A17/A18 usaram os três endpoints.

## QA12-A01 — Acumulado: formatos de data: **RISCO** (baixa; nenhum BUG)

Todas as linhas do roteiro rodaram (`$QA/a/a01-cdi.txt`). Resultado, para CDI
(`GET /api/indexes/Cdi/accumulated?<q>`):

| `q` | Resposta | O que a API entendeu |
|---|---|---|
| `from=2026-13-01&to=2026-12-01`, `from=2026-02-30&to=2026-03-10`, `from=31/12/2026&to=2027-01-10` | 400 "O valor do campo 'from' é inválido." | |
| `from=20260901&to=20261001`, `from=ontem&to=hoje` | 400 "O valor do campo 'to' é inválido. O valor do campo 'from' é inválido." | |
| `from=&to=2026-10-01`, `from=%20&to=...` | 400 "O campo 'from' é obrigatório." | |
| `from=2026-09-01&to=` | 400 "O campo 'to' é obrigatório." | |
| (sem nada) | 400 "O campo 'to' é obrigatório. O campo 'from' é obrigatório." | |
| `from_date=2026-09-01&to=2026-10-01` | 400 "O campo 'from' é obrigatório." | |
| `from=01/09/2026&to=01/10/2026` | **200**, `"from":"2026-01-09","to":"2026-01-10"`, 0.055131 | `MM/dd`: 9 e 10 de janeiro |
| `from=09/01/2026&to=10/01/2026` | 200, `"from":"2026-09-01","to":"2026-10-01"`, 1.081673 | `MM/dd`: 1º de setembro |
| `from=2026-9-1&to=2026-10-1`, `from=2026/09/01&to=2026/10/01` | 200, 1.081673 | 1º de set. a 1º de out. |
| `from=2026-09-01T00:00:00&to=2026-10-01` | 200, `"from":"2026-09-01"` (a hora some) | |
| `from=2026-09-01T23:30:00-03:00&to=2026-10-01` | **400** "O valor do campo 'from' é inválido." | com deslocamento recusa |
| `from=%202026-09-01&to=2026-10-01` | 200, 1.081673 | aparou o espaço |
| `from=2026-09-01&to=2026-10-01&from=2026-09-15` | 200, `"from":"2026-09-01"` | usa o primeiro valor, sem aviso |
| `FROM=2026-09-01&TO=2026-10-01` | 200, 1.081673 | sem diferenciar caixa |

IPCA (linhas 1, 4, 5, 10 e 13): mesmos resultados; `from=01/09/2026&to=01/10/2026` dá 200 com
`"from":"2026-01-09"`, 0.330000 (janeiro a janeiro).

Classificação (decisão 6: casos sem regra na spec são RISCO baixa): `01/09/2026` lido como 9 de
janeiro (um usuário brasileiro que digita `dd/MM/yyyy` recebe outro período sem aviso; o front que
envia a data só em `yyyy-MM-dd` não é afetado), data com hora aceita, parâmetro repetido usando o
primeiro, e a incoerência de `T00:00:00` aceito contra `T…-03:00` recusado. Nenhum 500; toda mensagem
diz o campo.

Observação de texto: com os dois campos inválidos a mensagem cita `to` antes de `from` (ordem
invertida), sem prejuízo.

## QA12-A02 — Acumulado: `indexType`: **RISCO** (baixa; nenhum BUG)

Todos os valores do roteiro nas duas rotas (`$QA/a/a02.txt`). Resultado igual nas duas rotas para
cada valor:

- `Cdi`, `cdi`, `CDI`, `cDi`: 200, `"indexType":"Cdi"` (nome canônico, não a grafia pedida). `Ipca`,
  `ipca`, `IPCA`: 200, `"indexType":"Ipca"`.
- `PreFixed`, `prefixed`: 400 "Só há cotação para CDI e IPCA." nas duas.
- `Selic`, `accumulated`, `0`, `1`, `2`, `3`, `-1`, `+1`, `%201`, `1.0`, `0x1`, `Cdi,Ipca`,
  `PreFixed,Cdi`, `Cdi,Cdi`, `Cdi%2Caccumulated`, `Cdi%2Faccumulated`: 400 "O valor do campo
  'indexType' é inválido." nas duas (as combinações de flags não passam).
- `%20Cdi%20` e `Cdi%20`: **200** nas duas rotas (aceita espaço em volta). RISCO baixa (decisão 6),
  sem divergência entre as rotas.
- `/api/indexes/Cdi/accumulated/` (barra final) 200; `/api/indexes/Cdi/ACCUMULATED` 200; `/api/Indexes/Cdi/accumulated`
  200; `/api/indexes/accumulated?from=...` 400 (o `accumulated` entra como `indexType`).
- POST, PUT, DELETE e PATCH em `/api/indexes/Cdi/accumulated`: 405 no envelope
  (`{"statusCode":405,"success":false,"message":"Método HTTP não permitido para este recurso.","data":null}`),
  com `Allow: GET`.

## QA12-A03 — Acumulado: datas extremas: **OK**

`$QA/a/a03.txt`. Todos abaixo de 50 ms (limite do roteiro: 2 s). Sem 500.

CDI:

| Chamada (`from`, `to`) | Resposta |
|---|---|
| `0001-01-01`, `9999-12-31` e `0001-01-01`, `0001-01-02` | 400 "Data 0001-01-01 fora do período coberto pelo calendário (2020-01-01 a 2050-12-31)." |
| `0001-01-01`, `0001-01-01` | 400 "Não há cotação de CDI publicada no período de 0001-01-01 a 0001-01-01." |
| `2019-12-31`, `2020-01-10` | 400 mensagem do calendário citando 2019-12-31 |
| `2020-01-01`, `2020-01-03` | 200, `0.017089` (= taxa de 2020-01-02; 01/01 feriado) |
| `2020-01-01`, `9999-12-31` | 200, `effectiveTo 2026-11-26`, 94.579715 |
| `2026-11-02`, `9999-12-31` | 200, `effectiveTo 2026-11-26`, 0.815711 |
| `2050-12-30`, `2050-12-31`; `2051-01-01`, `2051-02-01`; `9999-12-01`, `9999-12-31`; `9999-12-31`, `9999-12-31` | 400 "Não há cotação de CDI publicada no período de …" (corte em D deixa o período vazio antes do calendário) |

IPCA (não consulta o calendário): `0001-01-01` com qualquer `to`: 400 "Cotação de IPCA ausente
para 0001-01."; `2019-12-31`: 400 "... ausente para 2019-12."; `2020-01-01`, `2020-01-03`: 200, 0.210000;
`2020-01-01`, `9999-12-31`: 200, `effectiveTo 2026-10-31`, 44.853610; `2026-11-02`, `9999-12-31` e
`2050-…`/`2051-…`/`9999-…`: 400 "Não há cotação de IPCA publicada no período de …".

## QA12-A04 — Acumulado: `from == to` e períodos de um dia: **RISCO** (baixa; nenhum BUG)

L = 25/11 (não mudou). `$QA/a/a04.txt`.

| Chamada | Obtido | Esperado |
|---|---|---|
| `Cdi ...from=2026-11-24&to=2026-11-24` | 400 "Não há cotação de CDI publicada no período de 2026-11-24 a 2026-11-24." | 400 (ok) |
| `Cdi ...from=2026-11-24&to=2026-11-25` | 200, 0.050788, `effectiveTo 2026-11-25` | ok |
| `Cdi ...from=2026-11-20&to=2026-11-23` | 400 mesma mensagem | ok |
| `Cdi ...from=2026-11-21&to=2026-11-24` | 200, 0.050788 | ok |
| `Cdi ...from=2026-11-20&to=2026-11-24` | 200, 0.050788 | ok |
| `Cdi ...from=2026-11-27&to=2026-11-27` | 400 | ok |
| `Ipca ...from=2026-10-01&to=2026-10-01` e `from=2026-10-31&to=2026-10-31` | 200, `0.140000` | ok |
| `Ipca ...from=2026-10-31&to=2026-10-01` | 400 "A data inicial não pode ser posterior à data final." | ok |
| `Ipca ...from=2026-11-01&to=2026-11-01` | 400 "Não há cotação de IPCA publicada no período de 2026-11-01 a 2026-11-01." | ok |
| extras: `Cdi from=2026-11-21&to=2026-11-21` (sábado), `from=2026-11-22&to=2026-11-23`, `from=2026-11-26&to=2026-11-26` | 400 "Não há cotação de CDI publicada no período de …" | |

RISCO (baixa, decisão 6): `from == to` num dia **com** cotação (24/11 tem 0.050788 na listagem)
responde "Não há cotação de CDI publicada no período de 2026-11-24 a 2026-11-24." O front mostraria
"sem cotação" para uma data que tem cotação; a mensagem do fim de semana é a mesma.

## QA12-A05 — Acumulado: corte e idempotência: **OK**

`$QA/a/a05.txt`. `from=2026-11-16`:

| `to` | `effectiveTo` | `accumulatedPercentage` |
|---|---|---|
| 2026-11-24 | 2026-11-24 | 0.254198 |
| 2026-11-25 (L) | 2026-11-25 | 0.305115 |
| 2026-11-26 (D) | 2026-11-26 | 0.356058 |
| 2026-11-27, 2026-11-28 (sábado), 2026-12-25 (feriado) | 2026-11-26 | 0.356058 |

`to` ecoado como pedido em todos; o corte e `to = effectiveTo` dão o mesmo percentual. IPCA com
`from=2026-01-15`: `to` 2026-10-15 dá `effectiveTo 2026-10-15`, 4.096850; 10-31 dá `effectiveTo 2026-10-31`; 11-01, 11-30 e
2027-03-01 dão `effectiveTo 2026-10-31`, todos 4.096850. `from=2026-10-31&to=2026-11-15`: 200,
`effectiveTo 2026-10-31`, 0.140000.

## QA12-A06 — Acumulado: períodos longos e tempo: **OK**

Cinco chamadas cada, a quente: CDI 2020-01-02 a 2026-12-31 (24 a 33 ms), CDI 2020-01-01 a 2050-12-31
(26 a 44 ms), IPCA 2020-01-01 a 2026-12-31 (28 a 33 ms), CDI 2024-01-02 a 2025-01-02 (19 a 24 ms). Todos 200.

## QA12-A07 — Acumulado contra a listagem de cotações: **RISCO** (baixa; nenhum BUG)

Recalculado com `cdi.mjs` / `ipca.mjs` sobre `GET /api/indexes/{indexType}` (`$QA/a/a07.txt`),
dígito a dígito, **sem nenhuma diferença**:

| Período | API | Script |
|---|---|---|
| CDI 2026-09-01 a 2026-10-01 | 1.081673 | 1.081673 |
| CDI 2025-12-30 a 2026-01-05 | 0.165484 | 0.165484 |
| CDI 2020-01-02 a 2020-03-02 | 0.671468 | 0.671468 |
| CDI 2024-01-02 a 2025-01-02 | 10.875983 | 10.875983 |
| CDI 2020-01-02 a 2026-12-31 (effectiveTo 2026-11-26) | 94.579715 | 94.579715 |
| CDI 2020-01-01 a 2020-01-03 | 0.017089 | 0.017089 |
| CDI 2026-11-16 a 2026-11-30 (effectiveTo 2026-11-26) | 0.356058 | 0.356058 |
| IPCA 2020-01-01 a 2026-12-31 (82 meses) | 44.853610 | 44.853610 |
| IPCA 2023-03-10 a 2024-02-20 | 4.496270 | 4.496270 |
| IPCA 2026-08-15 a 2026-08-15 | -0.320000 | -0.320000 |
| IPCA 2026-09-01 a 2026-10-01 | 0.961150 | 0.961150 |
| IPCA 2026-01-15 a 2026-10-15 | 4.096850 | 4.096850 |

Duas convenções na mesma consulta: `GET /api/indexes/Ipca?from=2026-01-15&to=2026-09-20` devolve
fevereiro a setembro (8 meses, sem janeiro); `GET /api/indexes/Ipca/accumulated?from=2026-01-15&to=2026-09-20`
devolve `3.951320` (janeiro a setembro, 9 meses).

RISCO (baixa): o mesmo `from` inclui janeiro no acumulado e exclui na listagem. O README diz as
duas coisas, mas a seção do acumulado só afirma que "no IPCA, o mês de `to` entra" e que o IPCA "usa
meses cheios"; **não diz de forma explícita que o mês de `from` também entra** (a diferença para a
listagem fica a cargo do leitor ligar as duas seções). Sugestão de texto, sem urgência.

## QA12-A08 — Acumulado contra a projeção de um CDB 100% do CDI: **OK**

Criados `QA-F12A CDI100` (CDB, Cdi 100, 2026-01-01 a 2030-01-02) e `QA-F12A LCI100` (LCI, Cdi 100,
2026-01-01 a 2028-01-03); aportes de R$ 1.000 em 2026-09-01 (CDI100), 2026-11-16 (CDI100) e 2026-11-16
(LCI100).

| Chamada | Bruto | Acumulado | Fecha? |
|---|---|---|---|
| posição 09/01 `referenceDate=2026-10-01` | 1010.82, `isProjected false`, IR 22.5, 30/21 | `Cdi 2026-09-01..2026-10-01` = 1.081673 | 1000 × 1,01081673 = 1010,82 ✓ |
| posição 11/16 `referenceDate=2026-11-26` (D) | 1003.56, `isProjected false` | `Cdi 2026-11-16..2026-11-26` = 0.356058 | 1003,56 ✓ |
| posição 11/16 `referenceDate=2026-11-25` | 1003.05, `false` | 0.305115 | ✓ |
| LCI100 `referenceDate=2026-11-26` | 1003.56, `taxRate 0`, `taxAmount 0.00`, líquido 1001.21 | | mesmo bruto do CDB ✓ |
| posição 11/16 `referenceDate=2026-12-31` | 1015.86, **`isProjected true`**, 45/31 | `Cdi 2026-11-16..2026-12-31` = 0.356058, `effectiveTo 2026-11-26` | acumulado não passa de D, como esperado |
| posição 11/16 `referenceDate=2026-11-30` | 1004.58, `true` | | `cdi.mjs 100 9` sobre a listagem 16/11–25/11: fator 1.00458022, bruto 1004.58 ✓ |

## QA12-A09 — Serialização e escala: **OK**

Das 56 respostas de acumulado coletadas na parte (`grep -oh '"accumulatedPercentage":...'`):
todas com **exatamente 6 casas** (`0.140000`, `-0.320000`, `0.820000`, `1.081673`, `94.579715`), número
JSON (`typeof number`, nunca texto), sem notação científica, negativo com sinal. `effectiveTo`, `from`
e `to` em `yyyy-MM-dd`, `indexType` texto. A escala `0.8200` do Domain não aparece ao vivo: sai sempre
`0.820000` (decisão 6, item "escala": sem diferença entre respostas).

## QA12-A10 — Conferência externa na Calculadora do Cidadão: **OK**

Três consultas (decisão 4), `POST https://www3.bcb.gov.br/CALCIDADAO/publico/corrigirPeloCDI.do?method=corrigirPeloCDI`
com `aba=5`, R$ 1.000,00, 100% (`$QA/a/a10-1.html`, `$QA/a/a10-23.txt`). A calculadora exclui a data final,
como o CDI da API (`to` fora). Diferença **R$ 0,00** nas três (e no índice, 8 casas):

| Período na calculadora | Índice da calculadora | Valor | Chamada da API | Acumulado da API | Índice da API |
|---|---|---|---|---|---|
| 02/01/2020 a 25/11/2026 (L) | 1,94480942 | R$ 1.944,81 | `Cdi/accumulated?from=2020-01-02&to=2026-11-25` | 94.480942 | 1,94480942 |
| 30/12/2025 a 05/01/2026 | 1,00165484 | R$ 1.001,65 | `Cdi/accumulated?from=2025-12-30&to=2026-01-05` | 0.165484 | 1,00165484 |
| 02/01/2024 a 02/01/2025 | 1,10875983 | R$ 1.108,76 | `Cdi/accumulated?from=2024-01-02&to=2025-01-02` | 10.875983 | 1,10875983 |

## QA12-A11 — Projeção por título: ids e rotas malformados: **OK**

`GET /api/assets/{x}/projection` (`$QA/a/a11.txt`), título `QA-F12A CDI100` (`f9384fac-…`):

- `abc`; `…0` (GUID com um dígito a mais); `'%20or%201=1--`; 2.000 caracteres `x`: 400
  "O valor do campo 'id' é inválido." (o de 2.000 caracteres não deu 414 nem HTML).
- `00000000-0000-0000-0000-000000000000`: 404 "Título com id 00000000-0000-0000-0000-000000000000 não encontrado." `data` nulo.
- GUID entre chaves `{...}`, parênteses `(...)`, MAIÚSCULAS, sem hífen, com `%20` antes ou depois:
  200, igual ao GUID normal.
- `/api/assets//projection`: 404 `{"statusCode":404,...,"message":"Recurso não encontrado."}` (envelope);
  `/api/assets/{id}/projection/`, `/Projection` e `/api/Assets/{id}/projection`: 200.
- POST, PUT, DELETE e PATCH em `/api/assets/{id}/projection`: 405 no envelope, `Allow: GET`.
- Id inexistente com `referenceDate=2051-01-01`: **404** (o 404 vem antes do calendário, como a D-11
  manda); com `referenceDate=abc`: 400 "O valor do campo 'referenceDate' é inválido.".

Observação: `GET /api/assets/%00/projection` devolve 400 com corpo vazio (o Kestrel recusa o byte nulo
antes do envelope; irrelevante para o front).

## QA12-A12 — Projeção por título: `referenceDate` malformada ou vazia: **RISCO** (baixa; nenhum BUG)

Cada linha rodou nas três rotas (título, resumo, posição `c19939bb-…`), `$QA/a/a12.txt`. **As três
tratam a mesma entrada do mesmo jeito**:

- `abc`, `2026-02-30`, `2026-13-01`, `31/12/2026`, `26-11-16`, `20261126`: 400 "O valor do campo 'referenceDate' é inválido." nas três.
- vazio (`referenceDate=`) e só espaço (`%20`): tratada como omitida (200 com D) nas três (na posição, 200 com D; a posição do roteiro tem aporte em 16/11, então D serve).
- `2026-11-26T00:00:00` aceita (hora some); `2026-9-1` aceita; `2026-11-26&referenceDate=2026-06-15`
  (repetida) usa o primeiro valor.
- `01/12/2026` lida como **12 de janeiro de 2026** (título e resumo 200 com `referenceDate` de janeiro;
  a posição responde 400 "A data de referência não pode ser anterior à data de aporte.").

RISCO (baixa, decisão 6; o roteiro propunha média): `dd/MM/yyyy` virando `MM/dd`, mesmo
comportamento do A01. É a cultura invariante do binder; não é específico da F12.

## QA12-A13 — Carteira de teste e valores esperados: **OK**

Criados `QA-F12A PRE10`, `QA-F12A CDI110`, `QA-F12A LCA PRE9`, `QA-F12A IPCA6` (ids em "Dados criados").
Obtido contra o esperado da tabela do roteiro (`$QA/a/a13.txt`), todos iguais:

| Chamada | Bruto | IOF | IR | Líquido | Corr./úteis | `isProjected` | `isEstimated` |
|---|---|---|---|---|---|---|---|
| título PRE10 `referenceDate=2025-04-22` (caso 29) | 2009.86 (total) | 2.96 | 1.56 | 2005.34 | 21 / 13 por item | false | false |
| CDI100 16/11, `2026-11-30` | 1004.58 | 2.43 | 0.48 | 1001.67 | 14 / 9 | true | false |
| CDI110 16/11, `2026-11-30` | 1005.04 | 2.67 | 0.53 | 1001.84 | 14 / 9 | true | false |
| LCI100 16/11, `2026-11-30` | 1004.58 | 2.43 | 0.00 | 1002.15 | 14 / 9 | true | false |
| PRE10 16/11, `2026-11-30` | 1003.41 | 1.81 | 0.36 | 1001.24 | 14 / 9 | **false** | false |
| LCA PRE9 16/11, `2026-11-30` | 1003.08 | 0.00 | 0.00 | 1003.08 | 14 / 9 | false | false |
| IPCA6 01/10, `2026-11-03` | 1006.27 | 0 | 1.41 | 1004.86 | 33 / 21 | **false** | false |
| IPCA6 01/10, `2026-11-04` | 1006.71 | 0 | 1.51 | 1005.20 | 34 / 22 | **true** | false |

Recálculos independentes (calculadora de mão, μ = 0,38): IPCA+ em 04/11 `1000 × 1,0014 × 1,0038^(1/19) × 1,06^(22/252)` = 1006,71; em 03/11 `1000 × 1,0014 × 1,06^(21/252)` = 1006,27;
PRE 10% a 608 dias / 417 úteis: `1000 × 1,1^(417/252)` = 1170,83, IR 17,5% = 29,90 (bate com a resposta
de PRE10 em 2026-11-30 para o aporte de 2025-04-01).

## QA12-A14 — Data antes dos aportes, entre aportes e no dia do aporte: **OK**

`$QA/a/a14.txt`, título PRE10, `soma.mjs` com `OK` em todos os campos em todas as linhas:

| `referenceDate` | `positionCount` | Observação |
|---|---|---|
| 0001-01-01, 2000-01-01, 2024-12-31, 2025-03-31 | 0 | 200, totais 0, `positions []`, `referenceDate` igual à pedida (inclusive antes da emissão) |
| 2025-04-01 | 2 | rendimento 0.00 (o aporte do próprio dia entra) |
| 2025-04-02 | 2 | bruto 1000.38 cada |
| 2026-11-15 | 2 | bruto 1166.85 cada |
| 2026-11-16 | 3 | o de 16/11 com rendimento 0.00 |
| 2026-12-14 | 3 | |
| 2026-12-15 | 4 | o de R$ 500 com rendimento 0.00 |

## QA12-A15 — Aporte posterior à data padrão: **OK**

`GET /api/assets/06bf3e33-…/projection` (sem data): raiz `referenceDate 2026-11-26`, `positionCount 4`;
o aporte de R$ 500 em 2026-12-15 entra com `referenceDate 2026-12-15`, `grossYield 0.00`, IOF e IR 0,
`isProjected false`; os outros com `referenceDate 2026-11-26`. Posição R$ 500 sem data: 400 "A data de
referência padrão (2026-11-26, o dia útil seguinte à última cotação de CDI) é anterior à data de aporte
(2026-12-15). Informe referenceDate." (documentado). Com `referenceDate=2026-12-15`: 200, igual ao item.
`coerencia.mjs`: 4 de 4 itens `posição: OK | resumo: OK`.

## QA12-A16 — Título vencido e vencimento na fronteira: **OK**

`QA-F12A VENCIDO PRE` (PreFixed 10%, emissão 2024-01-01, vencimento 2025-01-02, aporte 1000 em 2024-01-02).
Quatro chamadas (sem data, `2025-01-02`, `2030-06-03`, `2050-12-31`): mesmos valores nas quatro
(bruto 1100.42, IR 17.5% = 17.57, líquido 1082.85, `elapsedDays 366`, `businessDays 253`,
`isProjected false`), `referenceDate` da raiz e do item igual à pedida (ou D sem data).
`coerencia.mjs` OK com e sem data de 2030.

## QA12-A17 — Coerência campo a campo entre os três endpoints: **OK**

Sete títulos (PRE10, CDI100, CDI110, LCI100, LCA PRE9, IPCA6, VENCIDO) × sete datas (sem data, D, P, T,
2026-11-30, 2026-12-31, 2028-01-03; D e P valem o mesmo dia nesta sessão): **73 linhas de item
`posição: OK | resumo: OK`, zero** `DIVERGE`, `ausente` ou `status 4xx/5xx`, `soma.mjs` sem divergência (`$QA/a/a17.txt`).
O resumo da carteira real respondeu 200 em todas. "mesma ordem do resumo difere" só no PRE10, entre os
dois aportes do mesmo dia (2025-04-01), caso aceito pela D-12 (ver A19).

## QA12-A18 — Soma das projeções de todos os títulos contra o resumo: **OK**

`somatitulos.mjs` com `referenceDate` ausente, D (2026-11-26), T (2026-11-27), 2026-11-30 e 2026-12-31:
**89 títulos, 0 com falha**, os sete totais (`totalInvestedAmount`, `totalGrossAmount`,
`totalGrossYield`, `totalTaxAmount`, `totalIofAmount`, `totalNetAmount`, `positionCount`) iguais ao
resumo em todas as datas, `isProjected` da raiz = OR dos itens = OR dos títulos (`true` em todas: os
IPCA+ do mês de hoje projetam em qualquer data) (`$QA/a/a18.txt`).

## QA12-A19 — Ordem dos aportes: **RISCO** (baixa; nenhum BUG)

`QA-F12A ORDEM PRE12` com sete aportes em ordem embaralhada e três em 2025-01-06. `soma.mjs`: "ordem
(mais recente primeiro) OK" sem data, em 2026-06-01 e em 2025-01-06. Seis chamadas seguidas da projeção
por título deram a **mesma ordem** (os três empates sempre `2eeba155, c0ea74a5, 5112af32`). O resumo,
três chamadas seguidas, também é estável, mas põe os empates em outra ordem
(`5112af32, c0ea74a5, 2eeba155`).

RISCO (baixa): a ordem dos aportes do mesmo dia no título difere da do resumo. A spec permite (D-12, "sem
garantia entre aportes do mesmo dia"), e a diferença não afeta valores; um front que cruze as duas
listas por posição de índice, e não por `positionId`, veria os três trocados.

## QA12-A20 — Título removido, taxa alterada e aporte novo: **OK**

- `QA-F12A REMOVIDO` sem aportes: projeção 200 com zeros; `DELETE` removeu; depois, 404 com e sem
  `referenceDate=2051-01-01` ("Título com id … não encontrado.").
- `DELETE /api/assets/$CDI110` (com aporte): 400 "Título possui aportes e não pode ser removido." (RN-06).
- `PUT /api/assets/$CDI110` com `rate 120`: 200. Depois do PUT, os três endpoints com 120% do CDI:
  bruto 1005.50 (`(1+0,00050788×1,2)^9`), IOF 2.92, IR 0.58, líquido 1002.00 em `2026-11-30`;
  `coerencia.mjs` OK.
- Aporte novo de 1.000 em 2026-11-23 no CDI110: aparece de imediato no título e no resumo
  (`coerencia.mjs`: 2 itens OK, "mesmos aportes no resumo OK", "mesma ordem OK").

Nota de ordem: rodei a A20 antes da A21 e da A25; CDI110 virou 120% dali em diante. Só os brutos da
linha CDI110 na A21 refletem 120% (o que importa ali são as marcas, que não dependem da taxa).

## QA12-A21 — Matriz de `isProjected` e `isEstimated` com o hoje real: **OK**

L = 25/11, hora 04:33 BRT (L não mudou). Saída completa em `$QA/a/a21.txt`. Resumo por título
(todas as datas; `isEstimated` **0 em todas as linhas**):

| `referenceDate` | CDI100 / CDI110 / LCI100 | PRE10 / LCA PRE9 | IPCA6 |
|---|---|---|---|
| sem data (D = 26/11), 2026-11-25, 2026-11-26 (P) | `false` | `false` | `true` |
| 2026-11-27 (T) | `true` | `false` | `true` |
| 2026-11-30 | `true` | `false` | `true` |
| 2026-12-31 | `true` | `false` | `true` |
| 2027-11-26 | `true` | `false` | `true` |
| 2029-11-27 | `true` (LCI100: vencimento 2028-01-03, ainda `true` porque o intervalo até o vencimento tem dia projetado) | `false` | `true` |

Igual à coluna "CDI (L = 25/11)" da tabela do roteiro em todas as células. Sem 400 em T nem em
2026-11-30. Aportes posteriores à data pedida ficam fora dos itens (o de 2026-12-15 só aparece de
2026-12-31 em diante).

## QA12-A22 — Vencimento na fronteira da projeção: **OK**

Quatro títulos CDI (100%, aporte 16/11) e quatro IPCA+ 6% (aporte 01/10), todos com
`referenceDate=2027-01-15` (`$QA/a/a22.txt`):

| Título | Vencimento | `isProjected` | Bruto | Corr./úteis |
|---|---|---|---|---|
| CDI | 2026-11-26 | false | 1003.56 | 10 / 7 |
| CDI | 2026-11-27 | **true** | 1004.07 | 11 / 8 |
| CDI | 2026-11-28 (sábado) | **true** | 1004.58 (igual ao de 11-30; IOF 2.75, IR 0.41 por 12 dias) | 12 / 9 |
| CDI | 2026-11-30 | **true** | 1004.58 | 14 / 9 |
| IPCA+ | 2026-10-31 | false | 1006.27, IR 1.41 | 30 / 21 |
| IPCA+ | 2026-11-02 (feriado) | **false** | 1006.27 | 32 / 21 |
| IPCA+ | 2026-11-03 | **false** | 1006.27 | 33 / 21 |
| IPCA+ | 2026-11-04 | **true** | 1006.71, IR 1.51 | 34 / 22 |

Todos com `referenceDate` da raiz e do item = 2027-01-15. Igual à tabela do roteiro.

## QA12-A23 — IPCA+ no mês de hoje, mudança de comportamento 1: **OK**

Ao `QA-F12A IPCA6` juntei aportes de 1.000 em 2026-11-03 e 2026-11-26 (`$QA/a/a23.txt`):

| `referenceDate` | Itens (aporte → referência, `isProjected`, bruto, corr./úteis) |
|---|---|
| sem data | 11/26→11/26 P0 1000 0/0 ; 11/03→11/26 P1 1006.92 23/16 ; 10/01→11/26 P1 1013.24 56/37 |
| 2026-11-03 | 11/03→11/03 **P0** 1000 0/0 ; 10/01→11/03 **P0** 1006.27 33/21 |
| 2026-11-04 | 11/03→11/04 P1 1000.43 1/1 ; 10/01→11/04 P1 1006.71 34/22 |
| 2026-11-26 (D) | igual à linha "sem data" |
| 2026-12-01 | 11/26 P1 1001.29 5/3 ; 11/03 P1 1008.22 28/19 ; 10/01 P1 1014.55 61/40 |
| 2027-01-04 | 11/26 P1 1010.22 39/25 ; 11/03 P1 1017.21 62/41 ; 10/01 P1 **1023.59** 95/62 |

Recálculo do bruto do aporte de 01/10 em 2027-01-04: `1000 × 1,0014 × 1,0038 × 1,0038 × 1,06^(62/252)` =
**1023,5947 → 1023,59** (μ = 0,38; com μ sem arredondar, 0,377749, daria 1023,55: a diferença de
quatro centavos mostra que a API usa os 2 casas, como a D-5 manda). Recálculo em 11/26:
`1000 × 1,0014 × 1,0038^(16/19) × 1,06^(37/252)` = 1013,24 ✓. Aporte de 26/11 sem data entra com
`referenceDate` = D (D = 26/11) e rendimento zero, sem exigir cotação.

## QA12-A24 — Resumo com carteira mista: **OK**

Resumo (carteira real mais a de QA) em padrão, D, T, 2026-11-30 e 2000-01-01 (`$QA/a/a24-*.json`):

- Chaves da raiz, nessa ordem: `totalInvestedAmount, totalGrossAmount, totalGrossYield, totalTaxAmount,
  totalIofAmount, totalNetAmount, positionCount, byAssetType, referenceDate, positions, isProjected`; sem
  `isEstimated` na raiz; itens com só `projection` e `asset`.
- Raiz `isProjected true` em padrão, D, T e 2026-11-30 (itens projetados: 93, 93, 196, 196 de 352/346);
  **`false` com carteira vazia** (2000-01-01: `positionCount 0`, `byAssetType []`, `positions []`, `isProjected false`).
- `isEstimated` em itens: 0.
- Raiz da projeção por título: exatamente `assetId, totalInvestedAmount, totalGrossAmount, totalGrossYield,
  totalTaxAmount, totalIofAmount, totalNetAmount, positionCount, referenceDate, positions`; sem
  `isProjected` e sem `byAssetType`.

Observação: sem `referenceDate` o resumo traz 352 itens e com `referenceDate=2026-11-26` traz 346; os seis
a mais são aportes posteriores a D, que entram com a própria data (comportamento documentado no README,
igual ao da projeção por título, A15).

## QA12-A25 — Projeções longas e faixas de IR com dias projetados: **OK**

Posição `QA-F12A CDI100` de 16/11 e `PRE10` de 16/11 (`$QA/a/a25.txt`):

| `referenceDate` | Corr. | Alíq. IR CDI e PRE | Úteis CDI e PRE | Bruto CDI | `isProjected` CDI / PRE |
|---|---|---|---|---|---|
| 2027-05-14 | 179 | 22.5 | 122 / 122 | 1063.90 | true / false |
| 2027-05-15 (sábado) | 180 | 22.5 | 123 / 123 | 1064.44 | true / false |
| 2027-05-16 (domingo) | 181 | **20.0** | 123 / 123 | 1064.44 | true / false |
| 2027-05-17 | 182 | 20.0 | 123 / 123 | 1064.44 | true / false |
| 2027-11-11 | 360 | 20.0 | 247 / 247 | 1133.62 | true / false |
| 2027-11-12 | 361 | **17.5** | 248 / 248 | 1134.19 | true / false |
| 2028-11-05 (domingo) | 720 | 17.5 | 494 / 494 | 1285.09 | true / false |
| 2028-11-06 | 721 | **15.0** | 494 / 494 | 1285.09 | true / false |
| 2036-11-17 e 2050-12-30 | 1143 (corte no vencimento 2030-01-02) | 15.0 | 780 / 780 | 1485.94 | true / false |

Conferido com `cdi.mjs 100 <DU>` sobre a listagem de 16/11 a 25/11: DU 122 → 1063.90; DU 123 →
1064.44; DU 780 → 1485.94, todos iguais. Mesmo `businessDays` entre CDI e PRE nas mesmas datas.
Tempo de cada chamada, 17 a 27 ms, inclusive em 2050.

## QA12-A26 — Regressão: chaves e tipos das quatro respostas: **OK**

(`$QA/a/a26.txt`.) Projeção da posição: `positionId, assetId, investedAmount, grossAmount, grossYield,
taxRate, taxAmount, netAmount, elapsedDays, businessDays, iofRate, iofAmount, isEstimated, referenceDate,
applicationDate, isProjected` nessa ordem; `isProjected` booleano, números como `number`, datas texto.
Resumo e projeção por título: ver A24. Acumulado: `indexType, from, to, effectiveTo,
accumulatedPercentage`. Listagem: `{"indexType":"Cdi","referenceDate":"2026-09-14","value":0.051660}`, igual ao
README. A projeção dentro do item do resumo e a do título têm as mesmas 16 chaves da projeção da posição.

## QA12-A27 — Regressão: mensagens e status antigos: **OK**

| Chamada | Obtido |
|---|---|
| `GET /api/positions/{P2}/projection?referenceDate=2026-11-15` | 400 "A data de referência não pode ser anterior à data de aporte." |
| `GET /api/positions/00000000-0000-0000-0000-000000000000/projection` | 404 "Posição com id … não encontrada." |
| `GET /api/positions/xyz/projection` | 400 "O valor do campo 'id' é inválido." |
| `GET /api/indexes/Cdi?from=2026-10-01&to=2026-09-01` | 400 "A data inicial não pode ser posterior à data final." |
| `GET /api/indexes/PreFixed?...` | 400 "Só há cotação para CDI e IPCA." |
| `GET /api/indexes/1?...` | 400 "O valor do campo 'indexType' é inválido." |
| `GET /api/positions/{P2}/projection?referenceDate=2051-01-01` | 200 (vencimento 2030: RN-02), bruto 1485.94, 15% |
| `GET /api/foo` | 404 `{"statusCode":404,"success":false,"message":"Recurso não encontrado.","data":null}` |
| `GET /api/portfolio/summary?referenceDate=2000-01-01` | 200, zeros, `isProjected false` |

## QA12-A28 — Swagger: **OK** (Swagger UI: NÃO EXECUTADO)

`GET /swagger/v1/swagger.json` (`$QA/a/swagger.json`) e `GET /swagger/index.html` 200.

- `GET /api/Assets/{id}/projection`: `id` path obrigatório `string/uuid`, `referenceDate` query opcional
  `string/date`; respostas **200, 400, 404**.
- `GET /api/Indexes/{indexType}/accumulated`: `indexType` path obrigatório (`$ref` para o enum
  `IndexType`: `PreFixed, Cdi, Ipca`), `from` e `to` query **obrigatórios** `string/date`; respostas **200, 400** (sem 404).
- Schemas: `IndexAccumulationResponse` (`indexType`, `from`/`to`/`effectiveTo` `string/date`,
  `accumulatedPercentage` `number/double`), `AssetProjectionResponse` (`assetId` `uuid`, seis totais
  `number/double`, `positionCount` `integer/int32`, `referenceDate` `string/date`, `positions` `array`),
  `PositionProjectionResponse` (`isProjected` e `isEstimated` `boolean`, 16 campos) e
  `PortfolioSummaryResponse` (`isProjected` `boolean`, `byAssetType`, `positions`). Coerentes com as
  respostas reais.
- **NÃO EXECUTADO:** abrir o Swagger UI e rodar o "Try it out" das duas rotas (sem navegador na sessão).

Observação (já era assim na rota de listagem, não é da F12): o enum `IndexType` do Swagger lista
`PreFixed`, valor que as duas rotas recusam com 400; quem usar o "Try it out" ao escolher `PreFixed`
verá o 400 "Só há cotação para CDI e IPCA.".

## QA12-A29 — `.http` e exemplos do README: **RISCO** (baixa; nenhum BUG)

Rodei as requisições do `FixedIncome.Api.http` pela porta 8080, com os nomes `QA-F12A HTTP …` e o
emissor `QA-F12A HTTP Banco` (`$QA/a/a29.txt`): criar título (200), detalhar, atualizar (200), registrar
aporte, projeção do aporte `referenceDate=2026-06-01` (1129.45, 17.5%, `isProjected false`), remover
(400 "Título possui aportes e não pode ser removido."), resumo `referenceDate=2026-06-01` (200, aporte presente),
CDI de 14 a 18/09 (5 dias), IPCA 01/2026 a 09/2026 (nove meses datados no dia 1), acumulado de CDI (1.081673),
acumulado de IPCA `from=2026-01-01&to=2026-09-01` (3.951320, inclui setembro como o comentário diz),
PreFixed (400), CDB 100% CDI (projeção 2026-09-23: 1042.68), CDB IPCA+ (projeção 2026-07-01: 1028.56,
`isEstimated false` porque o IPCA até outubro está publicado), projeção por título com e sem data
(sem data `referenceDate 2026-11-26`), "Projeção de aporte com data futura" `referenceDate=2026-12-31`
(200, **`isProjected true`**, 1078.76) e `/health` (200 `Healthy`). Todas respondem o que o comentário diz.
Exemplos do README: acumulado de setembro (1.081673) e o do título do caso 29 (A13) reproduzem; o
exemplo da projeção da posição (caso 29: 1004.93, IOF 30%, IR 22.5%, 21/13) também.

RISCO (baixa): o `.http` cria títulos e aportes sem prefixo de teste (`CDB Banco XYZ`, `CDB Banco XYZ
100% CDI`, `CDB Banco XYZ IPCA+6%`), e o README não avisa que, apontado para o Compose (8080), o
arquivo grava na carteira do usuário. Registro só por este motivo, como o roteiro pede.

Observação fora da F12 (pré-existente): o `PUT /api/assets/{id}` devolve `"rate":13` e o `GET`
`"rate":12.000000` para o mesmo título (escala diferente entre respostas do mesmo recurso). Não afeta
valor.

## QA12-A30 — Deriva de L durante a sessão: **OK**

Releitura às 04:35:18 BRT: **L continuou 2026-11-25** (CDI: último item 2026-11-25, 0.050788; IPCA último 2026-10-01,
0.140000); D 2026-11-26; M 2026-10; μ 0,38. Foto de `index_rate` do Compose (leitura por `psql`):

```
antes : Cdi|1731|2020-01-02|2026-11-25|17355e0298441e2b3a73641df5a7546c
        Ipca|82|2020-01-01|2026-10-01|66fcca300c66e3602ff7be8e5e788ab9
depois: Cdi|1731|2020-01-02|2026-11-25|17355e0298441e2b3a73641df5a7546c
        Ipca|82|2020-01-01|2026-10-01|66fcca300c66e3602ff7be8e5e788ab9
```

**md5 idêntico** nas duas séries (nenhuma linha alterada, apagada ou acrescentada). Como L não mudou,
todos os casos da parte valeram com L = 25/11 e não foi preciso repetir a A21.

---

## Lista de bugs

**Nenhum BUG** nesta parte.

## Lista de riscos (todos de severidade baixa)

| # | Cenário | Risco | Teste automatizado que fixaria o comportamento escolhido |
|---|---|---|---|
| 1 | A01, A12 | `dd/MM/yyyy` (`01/09/2026`) é lido como `MM/dd` (9 de janeiro), em `from`/`to` e em `referenceDate`; `2026-9-1`, `2026/09/01` e data com hora também são aceitas; com deslocamento (`T…-03:00`) é recusada | `Accumulated_DataNoFormatoDiaMesAno_RetornaBadRequest` e `GetProjection_ReferenceDateComHora_...` (se o Matheus decidir recusar) |
| 2 | A01, A12 | Parâmetro repetido (`from=…&from=…`, `referenceDate=…&referenceDate=…`) usa o primeiro valor sem aviso (igual à QACF-R3 do resumo) | idem |
| 3 | A02 | `indexType` com espaço em volta (`%20Cdi%20`) é aceito nas duas rotas | `Accumulated_IndexTypeComEspacos_...` |
| 4 | A04 | `from == to` num dia com cotação responde "Não há cotação de CDI publicada no período …" | `Accumulated_FromIgualToEmDiaUtilComCotacao_MensagemExplicaIntervaloVazio` |
| 5 | A07 | O README do acumulado não diz que o mês de `from` entra no IPCA (a listagem o exclui quando `from` é depois do dia 1) | só documentação |
| 6 | A19 | Ordem dos aportes do mesmo dia difere entre a projeção por título e o resumo (cada uma é estável) | `GetProjection_AportesNoMesmoDia_...` (se a ordem de desempate passar a ser contratual, por exemplo por `id`) |
| 7 | A29 | O `.http` cria títulos sem prefixo de teste e o README não avisa que, apontado para o Compose, grava na carteira real | só documentação |

## Dados criados durante o QA (banco do Compose, `fixedincome-db`)

Títulos e aportes `QA-F12A`, criados pela API. Não podem ser removidos (RN-06), exceto o título
`REMOVIDO` (já removido) e o `VAZIO` (sem aportes), que o Matheus pode apagar com `DELETE
/api/assets/{id}`. Nenhuma cotação foi tocada. A cópia completa dos ids está em
`$QA/a/ids.txt` (scratchpad da sessão); abaixo, os ids.

| Nome | Id | Aportes |
|---|---|---|
| QA-F12A VAZIO (criado na preparação; sem aportes, removível) | `e971a3ce-59e6-48e2-bb20-51311d2a7cea` | |
| QA-F12A CDI100 | `f9384fac-37c7-4fbd-b585-e0bd508066f3` | `fc7fbb61-c6ea-4164-a339-7df5ebc1ff2f` (1000, 2026-09-01); `c19939bb-2b0e-4e53-84bf-2395a62aad76` (1000, 2026-11-16) |
| QA-F12A LCI100 | `1c164bdf-33b5-43bd-9fc7-8fd357457bd7` | `2fde7546-47c7-4c1c-b0c0-d036920e0192` (1000, 2026-11-16) |
| QA-F12A PRE10 | `06bf3e33-9ccd-4dc7-89cb-681438296a67` | `23318c8d-6c28-425f-8f12-9b43b4af6c8e`, `151ad429-a320-4178-92a4-1ee5cf541d8c` (1000, 2025-04-01); `6325ea34-2f8f-4c96-a099-2182204e82b6` (1000, 2026-11-16); `5cd83c7f-2568-4389-9a49-fc2ee1751b2c` (500, 2026-12-15) |
| QA-F12A CDI110 (renomeado "QA-F12A CDI110 alterado", taxa 120) | `589b7400-0703-4632-ab23-4835279390f9` | `5c51f3ae-3a0c-45c1-9dc2-226a6e757508` (1000, 2026-11-16); `d281717c-e602-4bc8-a5c0-236080a8a156` (1000, 2026-11-23) |
| QA-F12A LCA PRE9 | `1626a9de-60f8-4aa6-bce2-5b496191a168` | `d88a2da4-c0ed-4d0e-b201-60cd0074101f` (1000, 2026-11-16) |
| QA-F12A IPCA6 | `184ba389-68df-4fc7-ada1-0d7d2af5a5cb` | `7985226a-417f-492c-8493-da8100cfa2da` (1000, 2026-10-01); `8508517a-d5b8-4e5d-a6af-82fa8e7c1f45` (1000, 2026-11-03); `b5dab63a-9c9d-4918-883f-4094ab983152` (1000, 2026-11-26) |
| QA-F12A VENCIDO PRE | `2960bf63-1110-4c60-acd7-7100ab3ab413` | `fc9c656b-e916-4beb-8977-fddef29d2c72` (1000, 2024-01-02) |
| QA-F12A ORDEM PRE12 | `c5adb536-5e3f-412e-86ef-23eb63a32570` | 7 aportes de 100: `f1ed3630-19fc-4787-b553-bfaeb8c59adb` (2026-03-02), `c0ea74a5-7a72-4f56-a292-dc64c7739828`, `2eeba155-5193-47e8-b7b6-4913a0f80793`, `5112af32-8f6e-4a6f-9b60-e74b65ab4df1` (2025-01-06), `87cb73dc-181f-4626-86d1-5393aab2198c` (2026-06-01), `17c1c4a3-3b6d-4bf3-a751-c3cca7273bf1` (2026-11-16), `afef51fc-685a-4d91-be29-3253a51da611` (2024-07-01) |
| QA-F12A REMOVIDO (já removido pelo DELETE) | `5ae62381-01ea-4113-afbe-aa2e43a3f9f8` | |
| QA-F12A VENC CDI 2026-11-26 | `3ff40fe0-374e-4c76-b4ae-71b38cbe0d99` | `ad6cfab4-5c0c-4241-9d3b-0e2406a59fe7` |
| QA-F12A VENC CDI 2026-11-27 | `374405ec-bdc5-4691-a662-3442a433cc51` | `623cd614-4e7b-493f-973a-2ad2c6c72a82` |
| QA-F12A VENC CDI 2026-11-28 | `64ae8ebf-67f2-4390-ac58-83bd79d8ce8f` | `af0018b2-19f4-4717-8dd3-ee3db726b122` |
| QA-F12A VENC CDI 2026-11-30 | `3367648f-c9c8-4388-a92f-60091577dac5` | `c848f626-aea8-4591-8230-a11f6b99d0da` |
| QA-F12A VENC IPCA 2026-10-31 | `a43c2008-2fc3-4df0-bd6a-49ee18029219` | `58945b52-45ed-4c9e-9d24-8028ab7dcb19` |
| QA-F12A VENC IPCA 2026-11-02 | `dee33109-e09f-4d4b-9cb4-5356a3f4f54e` | `9c1cee25-d280-463c-90f7-b73a7b90227a` |
| QA-F12A VENC IPCA 2026-11-03 | `a2c03f25-cd53-4d0f-a447-01b17b093486` | `b591308c-5fbf-41a7-be4d-5a5e95755705` |
| QA-F12A VENC IPCA 2026-11-04 | `35584ca6-3aec-4de2-bd7c-c8c4dfeb968a` | `d5a4838a-2c39-4d3b-bcb8-0959e22fe731` |
| QA-F12A HTTP CDB Banco XYZ Prefixado | `9cd29e57-644c-44d4-97c9-716837dbb6d8` | `4ada6526-4b54-4c5e-b98f-b3756bed12d3` (1000, 2025-06-01) |
| QA-F12A HTTP CDB 100% CDI | `58b2ccae-7a96-4ebd-b97d-5bd6901a9150` | `b9539b8c-2553-4ef8-9907-3be04541ebbd` (1000, 2026-06-01) |
| QA-F12A HTTP CDB IPCA+6% | `0d3379fc-4009-4bbd-a4f5-2fccef71bf09` | `a3a03eaa-7fd0-440f-ad57-1182ac85c74e` (1000, 2026-04-01) |

Total: 20 títulos vivos (19 com aportes, 1 vazio, mais 1 removido) e 32 aportes. Estes dados entram na
carteira real do Matheus (resumo e projeção por título): para a carteira voltar ao que era, só o banco
pode apagar os aportes (não há rota para isso).

## Estado final do ambiente

- Não parei nem reiniciei contêineres: `fixedincome-api` e `fixedincome-db` no ar (a parada no fim é do
  coordenador); não toquei em `qaf12-*`; nenhum `docker compose down`, nenhum volume apagado.
- `index_rate` do Compose: md5 igual ao de antes (A30).
- Repositório: `git status --short` igual ao da preparação, exceto este `docs/QA-F12-parteA.md`.

## Veredito

**SEM BUGS BLOQUEANTES.** Nenhum BUG, sete RISCO de severidade baixa. Os esperados da Parte A bateram
com o recálculo independente e com a Calculadora do Cidadão, e a fronteira de "hoje em Brasília" contra
UTC ficou, como o roteiro previa, para a janela de 21h a 24h (B12), que esta execução, às 04:30 BRT,
não podia exercitar.
