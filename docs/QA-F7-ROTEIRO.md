# Roteiro de QA — F7: Indexadores reais e dias úteis

Roteiro exploratório para o agente `qa-exploratorio`. Foca no que a `docs/VALIDACAO-F7.md` não
cobriu: entradas inválidas, datas de borda, fuso horário, concorrência e o contrato da API visto
de fora. O relatório vai em `docs/QA-F7.md`.

## Ambiente

1. Docker Desktop aberto. Na raiz do projeto:

   ```powershell
   docker compose up -d --build
   ```

2. A API não aplica migrations sozinha. Aplique contra o banco do compose:

   ```powershell
   dotnet ef database update --project src/FixedIncome.Infrastructure --startup-project src/FixedIncome.Api --connection "Host=localhost;Port=5432;Database=fixedincome;Username=fixedincome;Password=fixedincome"
   ```

3. Base: `http://localhost:8080`. Swagger em `http://localhost:8080/swagger` (o compose sobe em
   `Development`).
4. Reinicie a API (`docker compose restart api`) para o job importar com as tabelas criadas.
   Espere `GET /api/indexes/Cdi?from=2026-09-01&to=2026-10-01` devolver cotações.
5. Anote a data da última cotação de CDI importada: vários roteiros dependem dela (chamada aqui de
   **D**).

Se o Docker não estiver disponível, registre `NÃO EXECUTADO` no que depender dele e use o caminho
alternativo da `VALIDACAO-F7.md` (API compilada sobre um PostgreSQL local) só com a aprovação do
Matheus.

Todos os títulos criados levam o prefixo `QA-` no nome. Nunca rode `docker compose down -v`.

## QA-01 — Contrato e documentação

- O Swagger lista todos os endpoints, inclusive `GET /api/indexes/{indexType}`.
- Cada requisição do `src/FixedIncome.Api/FixedIncome.Api.http` funciona contra a API. Atenção: o
  arquivo usa a porta 5124 (`dotnet run`), e o compose usa 8080. Registre se isso confunde quem
  segue o README.
- O exemplo de chamada do README devolve o formato documentado.
- Enums saem como texto; toda resposta, de sucesso ou de erro, segue o formato `ApiResponse`.

## QA-02 — Entradas inválidas em `/api/indexes`

Teste e espere 400 com mensagem útil, nunca 500:

- `from` maior que `to`
- `from` ou `to` ausentes
- datas malformadas: `2026-13-01`, `31/12/2026`, `ontem`
- `indexType` `PreFixed`, `Selic`, `cdi` (minúsculo), `0`, `99`
- período enorme: `from=1990-01-01&to=2100-12-31` (registre também o tempo de resposta)

## QA-03 — Datas de borda na projeção

Crie um CDB PreFixed 10% a.a. e um CDB 100% do CDI. Para cada um:

- aporte num feriado (2026-09-07) e num sábado (2026-09-12)
- projeção com `referenceDate` num domingo
- `referenceDate` igual à data do aporte: rendimento, IOF e IR zero
- `referenceDate` depois do vencimento: limitada ao vencimento (RN-02)
- aporte em 2019 e referência em 2031: fora do calendário (2020 a 2030). Espere 400 com mensagem
  que diga o período coberto, nunca 500
- título que vence no meio do período de cotações

## QA-04 — IOF e tributação

Com aportes 1, 15, 29 e 30 dias corridos antes de **D**, em CDB, LCI, LCA e Tesouro Direto, todos
PreFixed 12% a.a., projetando em **D**:

- `IofRate` segue a tabela da RN-11 (96%, 50%, 3%, 0%)
- LCA: `IofAmount` zero sempre; LCI: `IofAmount` maior que zero até o dia 29 e `TaxAmount` zero
- Em toda resposta da sessão, `NetAmount = GrossAmount − IofAmount − TaxAmount` exatamente
- No resumo da carteira, `TotalNetAmount = TotalGrossAmount − TotalTaxAmount − TotalIofAmount`
  e igual à soma dos `NetAmount`

## QA-05 — Data padrão e fuso horário

- Resumo e projeção sem `referenceDate`: a data usada deve ser o dia útil seguinte a **D**.
- Risco a investigar: quando não há cotação, a data de hoje vem de `TimeProvider.GetUtcNow()`, em
  UTC. Entre 21h e 23h59 no horário de Brasília, o UTC já está no dia seguinte. O container roda
  em UTC. Verifique, sem ler o código antes, se existe algum caminho em que a data de "hoje" usada
  pela API difere da data de hoje em Brasília (por exemplo, com a tabela de cotações vazia, ou na
  janela que o job usa para importar até "hoje"). Se não conseguir reproduzir, registre como
  `RISCO` com a explicação.

## QA-06 — Aporte recente

- Aporte com data posterior a **D**: o resumo sem data deve mostrar essa posição com rendimento
  zero, sem derrubar a carteira.
- A projeção individual da mesma posição, sem data, devolve 400: é lacuna conhecida da F7.
  Confirme que a mensagem explica o motivo.

## QA-07 — IPCA

- Título IPCA+ 6% com aporte em meses cujo IPCA já saiu: `IsEstimated` falso.
- Projeção até um mês cujo IPCA ainda não saiu: `IsEstimated` verdadeiro.
- Período só no mês de deflação (agosto de 2026, −0,32%) com taxa baixa: se o rendimento ficar
  negativo, `IofAmount` e `TaxAmount` devem ser zero.

## QA-08 — Banco Central fora do ar

- Suba a API sem acesso ao SGS (por exemplo, desconectando o container da rede externa ou com um
  proxy morto, como na `VALIDACAO-F7.md`).
- A API continua respondendo: `GET /api/assets` 200.
- Com a tabela de cotações vazia, uma projeção em CDI devolve 400 citando a primeira data sem
  cotação, nunca 500.
- Devolva a rede e confirme que a importação se recupera na execução seguinte do job.

## QA-09 — Duas instâncias importando ao mesmo tempo

Antecipa a F8, onde o Container Apps pode subir mais de uma réplica.

- Com a tabela de cotações vazia, suba duas instâncias da API apontando para o mesmo banco (a do
  compose e uma com `dotnet run` na 5124, com a connection string do compose).
- Verifique nos logs se alguma importação falha por violação do índice único e se o banco termina
  sem duplicatas (`count(*)` contra `count(distinct (index_type, reference_date))`).

## QA-10 — Volume

- Crie 200 posições `QA-` distribuídas entre os tipos e meça o tempo do resumo da carteira e de uma
  projeção. Não há limite definido: registre os tempos e marque `RISCO` se o resumo passar de 2 s.

## QA-11 — Conferência externa num período novo

- CDB 105% do CDI, R$ 5.000, aplicação 2025-03-10, referência 2025-06-10.
- Compare o `GrossAmount` com a Calculadora do Cidadão do Banco Central para o mesmo período e
  percentual (o `VALIDACAO-F7.md` mostra como consultar). A diferença aceitável é de R$ 0,01 por
  R$ 1.000.

## Fora deste roteiro

O que a `VALIDACAO-F7.md` já provou com evidência não precisa ser repetido: contagem de dias úteis
dos casos de referência, importação idempotente numa instância só, retry com o SGS inalcançável e
o caso de setembro de 2026 contra a Calculadora do Cidadão.
