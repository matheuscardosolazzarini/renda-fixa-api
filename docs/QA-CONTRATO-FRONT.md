# QA exploratório — Contrato para o front (L-01 e L-03)

Tarefa: `docs/SPEC-CONTRATO-FRONT.md`. Caixa-preta, pela API em execução. Roteiro curto de 20 casos.

## Cabeçalho

| Item | Valor |
|---|---|
| Data | 2026-11-16 (relógio da API: 2026-11-17 00:46 UTC) |
| Commit-base | `621d0bfc4012b1ed4afcb9e5e1bd03e04e7a5aeb`, branch `contrato-para-o-front`, **mais a árvore de trabalho** (a tarefa não está commitada: 10 arquivos modificados, mais `docs/SPEC-CONTRATO-FRONT.md`, `docs/VALIDACAO-CONTRATO-FRONT.md` e `tests/FixedIncome.Api.Tests/SwaggerContractTests.cs` não rastreados) |
| Execução | `docker compose up -d` (imagem `renda-fixa-api-api:latest` já reconstruída; não usei `--build`), API em `http://localhost:8080`, PostgreSQL 16 do compose. Apache continua em `127.0.0.1:8080` (R9-03), por isso sempre `localhost`; as respostas vieram do Kestrel (`Server: Kestrel`) |
| Banco | Já tinha 316 posições e dados de QAs anteriores (`QA-01`, `QA-03`, `QA-06`, `QA-R`, `QA-X`...). Nada apagado. Dados do QA com prefixo `QA-CF` |
| Ferramentas | `curl` e scripts Node 22 (soma em decimal exato: o texto do número é lido com `JSON.parse` + `context.source` e convertido para `BigInt` com 6 casas, sem float) |
| Estado do banco | Falta a migration `20261112203900_ExtendAnbimaHolidaysTo2050` no banco do compose (a tabela `holiday` vai de 2020-01-01 a 2030-12-25; ver QACF-R1). Não apliquei, para não alterar o banco existente |

## Resumo

20 casos executados, 1 não executado (carteira realmente vazia). **0 bugs.** 3 riscos/observações, todos pré-existentes, nenhum desta tarefa.

## Resultados por caso

### Roteiro 1 — Resumo sem `referenceDate`

- **QACF-01: OK.** `GET /api/portfolio/summary` → 200. `data` tem as chaves antigas mais `referenceDate` e `positions`. Raiz `referenceDate` = `2026-11-16` (`yyyy-MM-dd`); `positionCount` 316 = `positions.length` 316 (depois dos meus aportes: 323 = 323).
- **QACF-02: OK.** Fechamento em decimal exato dos seis campos monetários, com 316 itens e de novo com 323: `InvestedAmount` 1.032.041,99, `GrossAmount` 1.160.218,39, `GrossYield` 128.176,40, `TaxAmount` 13.101,03, `IofAmount` 40,45, `NetAmount` 1.147.076,91: soma dos itens = total, nos seis. Repetido com datas 2026-06-15, 2026-08-02, 2026-08-03 e 2026-11-16 (informada): fecha sempre.
- **QACF-03: OK.** A ordem de `positions` é exatamente a de `GET /api/positions` (mesmos ids, mesma sequência) e `applicationDate` é não crescente.

### Roteiro 2 — Data informada no passado

- **QACF-04: OK.** `?referenceDate=2026-06-15` → 192 itens = `positionCount`; `?referenceDate=2026-08-03` → 218; `2026-08-02` → 209. Dos meus aportes: em 06-15 entram PRE1, CDI1, IPCA1, VENC1 (ficam fora PRE2, LCA1 e FUT1); em 08-03 entra PRE2 (aporte no próprio dia, limite inclusivo); em 08-02 não. Todo item tem `projection.referenceDate` igual à raiz e `applicationDate` <= data. Fechamento `OK` nas três datas. O IPCA1 passa de `isEstimated: true` (sem data) a `false` (em 06-15), coerente com a série publicada.

### Roteiro 3 — Aporte posterior à data padrão

- **QACF-05: OK.** Criei a posição FUT1 (CDB PRE 10%, R$ 500, aporte 2026-12-15; a API aceita aporte futuro). Sem data: raiz `2026-11-16`; item de FUT1 com `projection.referenceDate` = `2026-12-15`, `applicationDate` = `2026-12-15`, bruto = líquido = 500, rendimento 0, IOF 0, IR 0, `elapsedDays` 0, `businessDays` 0.
- **QACF-06: OK.** `GET /api/positions/{FUT1}/projection` (sem data) → 400, `data: null`, mensagem: "A data de referência padrão (2026-11-16, o dia útil seguinte à última cotação de CDI) é anterior à data de aporte (2026-12-15). Informe referenceDate." (útil para o front). Com `?referenceDate=2026-12-15` → 200 e o objeto é **idêntico** (`JSON.stringify` do texto) ao `projection` do item.

### Roteiro 4 — Item do resumo x endpoint de projeção

- **QACF-07: OK.** PRE1, PRE2, CDI1 (LCI a 100% CDI), IPCA1 (IPCA+6%, estimado), LCA1 e FUT1: a `projection` do item é idêntica, campo a campo, inclusive o formato do número, à resposta de `GET projection?referenceDate=<projection.referenceDate do item>`.
- **QACF-08: OK.** Varredura completa: todos os itens, sem data (323) e com `referenceDate=2026-08-03` (218): **541 de 541 idênticos, 0 divergentes**. Isso inclui os 90 itens `isEstimated: true`, todos os indexadores e tipos (Cdb, Lci, Lca, TreasuryBond).

### Roteiro 5 — Posição vencida

- **QACF-09: OK.** VENC1 (CDB PRE 10%, 2025-04-01 a vencimento 2025-04-22, R$ 1.000). Projeção com `referenceDate` 2025-04-21: `elapsed` 20, líquido 1.002,56. Com 2025-04-22, 2025-04-23, 2026-11-16 e 2030-01-01: `projection.referenceDate` = a pedida, `elapsedDays` 21, `businessDays` 13, bruto 1.004,93, IOF 1,48, IR 0,78, líquido 1.002,67 (os valores do caso de referência da spec, estáveis depois do vencimento). No resumo (datas 2025-04-22, 2025-05-05 e 2026-11-16): item com `projection.referenceDate` = raiz, `elapsedDays` 21, líquido 1.002,67 e `asset.maturityDate` = `2025-04-22`, que identifica a posição como vencida.

### Roteiro 6 — Entradas ruins

Todas com envelope `{statusCode, success, message, data}`; nos erros, `data: null`.

- **QACF-10: OK.** `?referenceDate=abc` e `?referenceDate=2026-02-30` (resumo e projeção) → 400 "O valor do campo 'referenceDate' é inválido."; `referenceDate=26-11-16` (resumo) → 400.
- **QACF-11: OK.** Data anterior a todos os aportes: `?referenceDate=2000-01-01` e `0001-01-01` → 200 com `referenceDate` presente na data pedida, totais 0, `positionCount` 0, `byAssetType: []`, `positions: []`. (Também `2025-04-01` → 74 posições.)
- **QACF-12: OK (com ressalva em QACF-R1).** `referenceDate=2051-01-01`, `2050-12-31` e `9999-12-31` no resumo → 400 "Data 2040-01-01 fora do período coberto pelo calendário (2020-01-01 a 2030-12-31)." `data: null`. Na projeção de PRE1 (vencimento 2028) a mesma data 2051 → **200** (o cálculo para no vencimento, RN-02; o calendário não é consultado depois dele); o `elapsedDays` fica em 607, o do vencimento.
- **QACF-13: OK.** Posição inexistente: 404 "Posição com id 0000... não encontrada.", com e sem `referenceDate`. Id malformado (`/positions/xyz/projection`) → 400 "O valor do campo 'id' é inválido."
- **QACF-14: OK.** `?referenceDate=` (vazio) e `?referenceDate=%20` no resumo e `?referenceDate=` na projeção são tratados como omitidos: 200 com a data padrão `2026-11-16`. É o comportamento do binder, que já era assim, e é coerente nos dois endpoints.
- **QACF-15: OK.** Data anterior ao aporte na projeção (`2026-05-03` para aporte em `2026-05-04`) → 400 "A data de referência não pode ser anterior à data de aporte."; no dia do aporte → 200 com `elapsedDays` 0.
- **QACF-16: OK.** Datas acima do último CDI importado: `referenceDate=2027-06-01`, `2030-12-31` e `2031-01-01` no resumo → 400 com mensagem útil e o título/posição culpado (ex.: "Posição 900df8c7-... (título 'QA-03 CDI100 2019-2032'): Cotação de CDI ausente para 2030-12-30..."). Não é 500. Comportamento anterior à tarefa (ver QACF-R2).

### Roteiro 7 — Compatibilidade

- **QACF-17: OK.** Raiz do resumo: exatamente as 8 chaves antigas (`totalInvestedAmount`, `totalGrossAmount`, `totalGrossYield`, `totalTaxAmount`, `totalIofAmount`, `totalNetAmount`, `positionCount`, `byAssetType`) mais `referenceDate` e `positions`; `byAssetType[]` com as 5 chaves de sempre. Projeção (no endpoint e em todos os 323 itens): as 13 chaves antigas mais `referenceDate` e `applicationDate`, nenhuma outra. Item de `positions`: só `projection` e `asset`. Tipos: valores monetários, alíquotas e dias numéricos (dias inteiros), `isEstimated` booleano, ids e datas como texto. camelCase em todo o JSON. Enums como texto (`"Cdb"`, `"PreFixed"`, `"TreasuryBond"`; 12 combinações tipo/indexador vistas). `asset` de todos os 323 itens é igual (campo e texto) ao de `GET /api/assets` (e `GET /api/assets/{id}` para os 5 títulos QA-CF); `projection.assetId` = `asset.id`; `applicationDate`, `investedAmount` e `assetId` de cada item batem com `GET /api/positions`.

### Roteiro 8 — Swagger, README e `.http`

- **QACF-18: OK.** `GET /swagger/v1/swagger.json` (200, 32.715 bytes): `PositionProjectionResponse` tem `referenceDate` e `applicationDate` (`string`, `format: date`); `PortfolioSummaryResponse` tem `referenceDate` (`date`) e `positions` (array de `$ref PortfolioPositionResponse`, `nullable`); `PortfolioPositionResponse` tem `projection` (`$ref PositionProjectionResponse`) e `asset` (`$ref AssetResponse`). O parâmetro `referenceDate` das duas rotas é `date`, opcional; respostas 200, 400, 404 declaradas. `additionalProperties: false` em todos. Pré-existentes e já anotados na spec: nenhum campo marcado `required`; o caminho do Swagger é `/api/Portfolio/summary` e `/api/Positions/{id}/projection`, com inicial maiúscula (o roteamento aceita as duas grafias: `GET /api/Portfolio/summary` → 200).
- **QACF-19: OK.** Exemplos do `README.md`: o do README (CDB PRE 10%, R$ 1.000, 2025-04-01 a 2025-04-22) bate valor a valor com a resposta real de VENC1 em `referenceDate=2025-04-22` (bruto 1.004,93, rendimento 4,93, IR 22,5% = 0,78, IOF 30% = 1,48, líquido 1.002,67, 21 corridos, 13 úteis) e com o formato dos campos. O exemplo do resumo (LCI aportada depois da data padrão, `referenceDate` do item = data do aporte, raiz = padrão) reproduz o que FUT1 faz ao vivo; a frase "da mais recente para a mais antiga" confere. Diferença só cosmética: a API devolve decimais com escala (`1000.00`, `0`) onde o README escreve `1000`, `500`. As chamadas do `FixedIncome.Api.http` (projeção com `referenceDate`, resumo com e sem `referenceDate`) rodaram contra a API e respondem como os comentários dizem. Obs.: a chamada do resumo com `referenceDate=2026-06-01` do `.http` fica vazia em um banco que não tenha aportes antes dessa data; não é erro.

### Roteiro 9 — Tamanho e tempo (só observação)

- **QACF-20: OK (observação).** Resumo sem data com a carteira atual: 316 posições, **187.694 bytes**, 0,457 s na primeira chamada (a quente: 34 a 40 ms); 323 posições: **191.845 bytes** (~594 bytes por item), 34 a 40 ms em 5 chamadas, 28 a 32 ms com data. Sem compressão HTTP (`Transfer-Encoding: chunked`, sem `Content-Encoding`). Para uma carteira "pessoal e pequena", como a spec supõe, é irrelevante; com a escala atual (centenas de posições) já é perto de 190 KB e cresce linearmente.

### Não executado

- **QACF-NE1: NÃO EXECUTADO.** Carteira realmente vazia (D-4: `positions: []` com `referenceDate` da data padrão, ou 400 com banco sem calendário). Exigiria apagar as 316 posições, o que a regra proíbe. O caso aproximado (data anterior a todos os aportes, QACF-11) passou; a carteira vazia sem data está coberta pelos testes automatizados 7, 8, 18 e 23 da spec.

## Bugs

Nenhum.

## Riscos e observações

Todos pré-existentes à tarefa; nenhum bloqueia.

- **QACF-R1 (risco, média; ambiente e pré-existente).** O banco do compose está sem a migration `20261112203900_ExtendAnbimaHolidaysTo2050` (`__EFMigrationsHistory` só tem as duas primeiras; `holiday` vai de 2020-01-01 a 2030-12-25, 139 linhas). O README diz que a migration não é aplicada na subida, então quem sobe a API com um banco antigo fica com o calendário só até 2030 sem aviso. Efeito no resumo: uma data a partir de 2031 (e a data padrão, se um dia chegar lá) responde 400 mesmo que o calendário "esteja em 2050" na documentação. Não apliquei a migration (alteraria o banco existente). Não é desta tarefa. Sugestão, se quiser cobrir: um teste de ambiente/health que compare a última migration aplicada com a esperada.
- **QACF-R2 (risco, baixa; pré-existente, relevante ao front).** No resumo, uma única posição que não consiga ser calculada derruba a resposta inteira com 400. Exemplos reais: `referenceDate=2051-01-01` responde "Data 2040-01-01 fora do período coberto..." (a data citada é o vencimento de um título da carteira, `QA-R wide PRE`, não a que o usuário digitou); data após a última cotação de CDI responde "Cotação de CDI ausente para 2030-12-30...". As mensagens são úteis, mas citam datas diferentes da pedida e o front não tem como mostrar o resto da carteira. A divisão de responsabilidade já existia (a spec D-4 e RN-07 não mudam isso); `positions` apenas torna mais visível o que o front perde. Fica como observação para o front tratar o 400 do resumo com data futura.
- **QACF-R3 (observação, baixa; pré-existente).** `referenceDate=2026-11-16T00:00:00` (data com hora) é aceito no resumo (200) e `referenceDate` repetida (`?referenceDate=2026-11-16&referenceDate=2026-06-15`) usa a primeira; é o comportamento do model binder do ASP.NET e não muda com esta tarefa.

Sobre a divergência intencional da D-8: confirmada ao vivo (QACF-05, QACF-06): a projeção sem data de uma posição com aporte posterior responde 400, o resumo sem data a mostra com a data do aporte; a mensagem de erro diz "Informe referenceDate", o que ajuda o front.

## Dados criados durante o QA

Todos com prefixo `QA-CF`, no PostgreSQL do compose (`fixedincome`), criados pela API. Para limpeza posterior (primeiro as posições, depois os títulos; o título não pode ser removido com aportes):

Títulos (emissor `QA-CF Banco`):

| Chave | Id | Nome |
|---|---|---|
| PRE | `cdf2aef0-301d-488b-a29e-6d2212cddbfc` | QA-CF CDB PRE 10% |
| CDI | `06c9f3b1-6dba-4d48-82fe-59ef0f864224` | QA-CF LCI CDI 100% |
| IPCA | `12db1c45-6b61-4983-a1f1-9efc14f922fc` | QA-CF CDB IPCA+6% |
| LCA | `6eae6deb-a893-4e45-8b7c-6158676f3405` | QA-CF LCA PRE 9% |
| VENC | `0a203212-8819-4d8b-8d68-b17cff9717a5` | QA-CF CDB VENCIDO PRE 10% |

Posições:

| Chave | Id | Título | Valor | Aporte |
|---|---|---|---|---|
| PRE1 | `9d48628d-8c5e-47dd-9682-9c38efab9c26` | PRE | 1000 | 2026-05-04 |
| PRE2 | `ed3b3098-65d6-4d20-83af-a9f00df9c117` | PRE | 2500,50 | 2026-08-03 |
| CDI1 | `99a5f6ae-897c-444f-a3a2-05c954d20c76` | CDI | 1500 | 2026-06-01 |
| IPCA1 | `4eeb2b12-f9e8-439e-8568-588002d354a9` | IPCA | 1000 | 2026-04-01 |
| LCA1 | `12eb4665-361f-4c40-988f-d8f1d3ea6cc5` | LCA | 777,77 | 2026-09-01 |
| VENC1 | `88460e30-fa75-48f8-8f8e-825f7fbe2214` | VENC | 1000 | 2025-04-01 |
| FUT1 | `c26de723-8901-4a3d-8681-3d2e0b224f16` | PRE | 500 | 2026-12-15 (futuro) |

Os 4 itens com aporte em 2030 (`QA-R wide PRE`, `QA-03 ...`, `QA-X posbase`) são de QAs anteriores, não deste. Nenhuma outra alteração: nenhum arquivo de código, teste, spec ou ROADMAP foi tocado; nada em `../renda-fixa-web`. Contêineres parados com `docker compose stop` (sem `down`, volumes intactos).

## Veredito

**SEM BUGS BLOQUEANTES.** O contrato novo se comporta como a spec descreve, sem regressão nos campos antigos. Os três riscos acima são anteriores à tarefa.
