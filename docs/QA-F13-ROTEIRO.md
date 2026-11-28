# Roteiro de QA — F13: Contrato v3 para o front

Roteiro exploratório para o agente `qa-exploratorio`. O relatório vai em `docs/QA-F13.md` (se as
partes rodarem em paralelo, `docs/QA-F13-parteA.md` e `docs/QA-F13-parteB.md`, consolidados pelo
coordenador). **Status: aprovado pelo Matheus em 27/11/2026**, com as decisões registradas no fim
("Decisões antes de executar"). Mudança em relação ao rascunho (decisão 4): **a Parte A roda numa
pilha isolada própria** (`qaf13a-fase` em `http://localhost:28115`, banco `qaf13a-db` com cópia
completa do Compose e importação ligada, como no Compose). Nada é gravado no banco do Compose.

A F13 entrega ao front quatro dados prontos, só por adição de campos: `netYield` na projeção e
`totalNetYield` no consolidado e na projeção por título (RN-15); `nextTaxRate` e `nextTaxRateDate`
(RN-16) e `iofFreeDate` (RN-17) na projeção da posição, repetidos nos itens do consolidado e da
projeção por título; e `byAsset` no consolidado (RN-18). Nenhum campo existente pode mudar de nome,
de tipo ou de valor. O "usuário" é o front `renda-fixa-web`: mostra o ganho líquido sem subtrair
nada, escreve "o imposto cai para 20% em 29/09/2025" e "o IOF zera em 01/05/2025" a partir das datas,
e monta a lista de títulos a partir de `byAsset`, sem somar.

A pergunta que guia cada cenário: **em que caso o front mostraria um ganho errado, uma data de
faixa ou de IOF errada (ou uma data onde deveria haver nenhuma), um total de título que não fecha
com a lista de aportes, ou um campo antigo diferente do que mostrava antes da fase?**

## O que a validação já provou (não se repete aqui)

A `docs/VALIDACAO-F13.md` (27/11/2026) provou, e este roteiro não refaz como caso próprio:

- build sem warning, 975 testes, cobertura de Domain (97,5%) e Application (95,0%), Stryker com
  97,15% e nenhum sobrevivente no código novo;
- todos os casos de referência da spec por teste automatizado (1a, 1b, 2a, 2b, 4, 5, 6a a 6d, 10, 11,
  16) e a invariante da D-4;
- ao vivo, **num banco vazio** (`f13verif`, porta 8081, importação desligada, só PreFixed e uma LCI e
  uma LCA): caso 1a (`netYield 2.67`, literal `20.0`, `2025-09-29`, `2025-05-01`); LCA com os três
  campos `null` e presentes; caso 16 no consolidado e na projeção por título com data informada
  (ordem B, A); 9 de 9 títulos com `byAsset` igual à projeção por título em 22/04/2025; as fronteiras
  6a-1 a 6d-2, as sete linhas do caso 4 e as do caso 10;
- o `swagger.json` ao vivo: campos novos no fim, tipos, `AssetSummaryResponse` e o conjunto de
  propriedades `nullable`.

## O que a validação não cobriu (foco deste roteiro)

- **nada de CDI nem de IPCA ao vivo** (o smoke só tinha PreFixed): CDI com dias projetados, IPCA+ com o
  mês de hoje projetado, IPCA+ estimado, Tesouro Direto, LCI na projeção da posição;
- **o consolidado e a projeção por título sem data** (data padrão), inclusive o aporte posterior a ela,
  que é projetado na própria data (`d` = 0): `nextTaxRateDate` e `iofFreeDate` presentes, ou nulos
  perto do vencimento. A própria validação registrou que não reproduziu o consolidado sem data;
- **uma carteira grande e heterogênea**: o banco do Compose tem mais de 355 aportes de QAs anteriores,
  de todos os tipos, vencidos, com CDI e IPCA reais. As regras novas nunca foram conferidas item a
  item sobre ela;
- **a comparação com a `main` ao vivo**, campo a campo e no texto do JSON, para os mesmos dados: a
  validação provou o "só aditivo" pelo `git diff` dos testes e pelo Swagger, não pela resposta. Em
  particular, a D-2 reescreveu a tabela de IR com os literais `20.0m`, `17.5m` e `15.0m`, e a escala
  do `taxRate` existente pode ter mudado no texto (`20` → `20.0`) sem mudar de valor;
- `netYield` negativo ao vivo; datas de faixa e de IOF depois da cobertura do calendário; títulos
  curtíssimos com CDI projetado; aporte na véspera e no próprio dia do vencimento;
- `byAsset` com muitos títulos, títulos sem aportes, todos os aportes de um título depois da data,
  empates de aportes do mesmo dia, depois de `PUT` e `DELETE`; `byAsset` cruzado com `byAssetType`;
- serialização no texto: `null` presente nos três endpoints, escalas, zero negativo, datas ISO, o
  snapshot do front em `../renda-fixa-web/openapi/renda-fixa-api.json`;
- desempenho do consolidado com `byAsset` numa carteira grande, contra a `main` e contra o risco 11
  da F12.

## Regras herdadas do agente, adaptadas à fase

- **Caixa-preta primeiro.** Use a API como o README, o Swagger e o `FixedIncome.Api.http` ensinam.
  O código de `src/` só é aberto para explicar um achado já reproduzido.
- **Tudo reproduzível.** Cada achado traz método, URL, corpo, a resposta obtida (status e trecho do
  JSON), a esperada e a fonte da expectativa (RN-xx, D-x da spec, README, bom senso de API).
- **Nada é corrigido.** O único arquivo que o QA cria no repositório é `docs/QA-F13.md` (ou as
  partes). Os scripts e as saídas ficam no scratchpad.
- **Não destrua dados.** Nada de `docker compose down -v`, nada de apagar volume. Títulos com prefixo
  `QA-F13A` (Parte A) e `QA-F13B` (Parte B) no nome e no emissor. A Parte A **não altera cotações**
  nem escreve no banco do Compose fora da API; quem mexe em `index_rate` é só a Parte B, num banco
  próprio.
- **500 é sempre achado.** Entrada inválida deve virar 400 no envelope `ApiResponse`, com mensagem
  útil e `data` nulo.
- **Não invente resultado.** O que depender de autorização não dada fica `NÃO EXECUTADO`, com o motivo.
- **Números e datas conferidos no texto do JSON.** Os scripts leem a fonte de cada número
  (`JSON.parse` com `context.source`) e comparam dinheiro em inteiros, nunca em `float`. O esperado de
  `nextTaxRateDate` e `iofFreeDate` é recalculado por regra (`aporte + n` dias corridos, corte no
  vencimento), não copiado da resposta.

## Ambiente

### O que depende de hoje e do que não depende

As datas novas (RN-16 e RN-17) dependem só do aporte, do prazo `d` até a data de cálculo e do
vencimento: **não dependem de hoje nem de cotação**. O ganho líquido (RN-15) e os totais (RN-18)
dependem dos valores que já existiam, que este roteiro **não recalcula**: compara com a `main` sobre os
mesmos dados (Parte B) e confere as somas e as subtrações no texto. Por isso o roteiro é muito menos
sensível ao dia da execução que o da F12. Ainda assim, o executor registra **antes de começar** e **no
fim de cada parte**:

| Símbolo | O que é | Como obter | Valor esperado em 27/11/2026, sexta |
|---|---|---|---|
| **T** | hoje em Brasília | `date -u -d '-3 hours' '+%F %T'`; conferir com o cabeçalho `Date` (UTC) da API | 2026-11-27 |
| **P** | dia útil anterior a hoje (RN-12) | calendário | 2026-11-26 |
| **L** | última cotação de CDI do banco | último item de `GET /api/indexes/Cdi?from=<T−20 dias>&to=<T>` | 2026-11-25 ou 2026-11-26 |
| **D** | data padrão: dia útil seguinte a L | `referenceDate` de `GET /api/assets/{QA-F13A VAZIO}/projection` | 2026-11-26 ou 2026-11-27 |
| **M** | último mês de IPCA publicado | último item de `GET /api/indexes/Ipca?from=<T−400 dias>&to=<T>` | 2026-10 |

Onde o roteiro precisa de D, usa a variável `$D` lida no passo 5. As âncoras de data escritas nos
cenários assumem **D = 2026-11-26**; com D = 2026-11-27, só mudam as linhas marcadas "em D" (o `d` sobe
um dia) e o aporte `D − 181` da A01, que é calculado a partir de `$D`. Execução em dezembro de 2026, ou
depois, muda a A03 (o IPCA de novembro deixa de ser o mês de hoje) e pode deixar de ser "posterior à
data padrão" o aporte de 2026-12-15 (A07): nesse caso, troque 2026-12-15 por `D + 19 dias` e recalcule
as âncoras com a regra.

A importação da `qaf13a-fase` fica ligada (padrão do Compose, a cada 6 horas). Se L mudar durante a Parte A, D muda, e a
comparação "sem data" entre dois endpoints pode divergir só por isso (ressalva da D-5 da spec): o
`coerencia13.mjs` da A11 imprime a `referenceDate` de cada resposta para separar esse caso de um bug.

### Partes e isolamento

| | Parte A | Parte B |
|---|---|---|
| API | `qaf13a-fase`, `http://localhost:28115`, imagem da fase, importação ligada (como no Compose) | `qaf13-fase` (`http://localhost:28113`, código da fase) e `qaf13-main` (`http://localhost:28114`, `origin/main`), as duas com importação desligada |
| Banco | `qaf13a-db`, banco `qaf13`, sem porta publicada, com cópia completa do `fixedincome-db` (esquema, mercado e carteira) | `qaf13-db`, banco `qaf13`, sem porta publicada, lido e escrito pelas duas APIs |
| Dados | cópia da carteira do Compose + títulos novos `QA-F13A` | primeiro vazia (B00), depois cópia da carteira do Compose (decisão 1) + títulos `QA-F13B` |
| Cotações | copiadas do Compose; só a importação escreve nelas | copiadas do Compose e manipuladas à vontade, com restauração entre cenários |
| Para quê | regras novas com CDI e IPCA reais, carteira grande, coerência entre endpoints, serialização, Swagger e documentação | só aditivo contra a `main`, volume, desempenho, IPCA estimado, importação parada |

O Apache do Windows ocupa `127.0.0.1:8080` (R9-03 da F9): use sempre `localhost` e confira
`Server: Kestrel` na primeira resposta.

### O que não pode ser tocado

- **O banco do Compose** (`fixedincome-db`, volume `renda-fixa-api_fixedincome-db-data`): **só
  leitura** (decisão 4). Nenhuma requisição de escrita a `http://localhost:8080`; leitura por `psql` e
  `pg_dump` só nas fotos e na cópia da preparação (decisão 1). Nenhum `insert`, `update` ou `delete`.
- **`index_rate` do Compose**: nunca alterada.
- **O repositório**: só leitura, exceto `docs/QA-F13.md`. A imagem da `main` é montada a partir de
  `git archive` no scratchpad: nada de `git worktree`, `checkout`, `stash` ou `reset`.
- **`../renda-fixa-web`**: só leitura do snapshot `openapi/renda-fixa-api.json`.
- **A imagem `renda-fixa-api-api:latest`**: a Parte B só a marca com outra etiqueta e roda contêineres
  novos a partir dela.
- **O Docker Desktop**: não parar, não reiniciar, não limpar cache.

### Preparação comum (Git Bash, sequencial)

No PowerShell 5.1, `curl` é alias de `Invoke-WebRequest`: use o Git Bash para tudo.

0. **Variáveis e foto do estado** (só leitura):

   ```bash
   export MSYS_NO_PATHCONV=1
   REPO=/c/Users/matheuss/Desktop/renda-fixa/renda-fixa-api
   WEB=/c/Users/matheuss/Desktop/renda-fixa/renda-fixa-web
   QA="<scratchpad da sessão>/qaf13"; mkdir -p "$QA"/{a,b,js,main}
   QAW="$(cygpath -m "$QA")"          # caminho C:/... para o node
   cd "$REPO" && git rev-parse HEAD origin/main && git status --short > "$QA/git-status-antes.txt"
   docker ps -a --format '{{.Names}} {{.Image}} {{.Status}} {{.Ports}}' > "$QA/antes-ps.txt"
   docker volume ls -q | sort > "$QA/antes-volumes.txt"
   node --version          # 21 ou mais: os scripts leem o texto do número (JSON.parse com context.source)
   node -e "JSON.parse('20.0',(k,v,c)=>console.log(c.source))"   # imprime 20.0
   ```

   Esperado: `HEAD` igual a `origin/main` (`9174345`) e a fase só na árvore de trabalho (29 entradas em
   `git status`, mais o `docs/VALIDACAO-F13.md`). Se a fase já tiver sido commitada, a "main" da Parte B
   passa a ser o commit anterior ao da fase: registre qual foi usado.

1. **API do Compose com o código da fase** (o mesmo que a validação fez; recria os contêineres e
   preserva o volume):

   ```bash
   cd "$REPO" && docker compose up -d --build
   until curl -sf http://localhost:8080/health >/dev/null; do sleep 2; done
   curl -sI http://localhost:8080/health | grep -iE '^(HTTP|server|date)'
   curl -s http://localhost:8080/swagger/v1/swagger.json | grep -o '"netYield"' | head -1   # a imagem tem a F13
   docker exec fixedincome-db psql -U fixedincome -d fixedincome -tAc \
     'select "MigrationId" from "__EFMigrationsHistory" order by 1'      # as mesmas 3 da F12: a F13 não tem migration
   docker exec fixedincome-db psql -U fixedincome -d fixedincome -tAc "select min(date), max(date) from holiday"
   ```

2. **Fotos do Compose** (para provar no fim que as cotações só cresceram e saber o que a Parte B
   copiou):

   ```bash
   sqlc() { docker exec fixedincome-db psql -U fixedincome -d fixedincome -tAc "$1"; }
   FOTO="select index_type, count(*), min(reference_date), max(reference_date),
                md5(string_agg(reference_date||':'||value, ',' order by reference_date))
           from index_rate group by 1 order by 1"
   sqlc "$FOTO" > "$QA/compose-index-rate-antes.txt"
   curl -s localhost:8080/api/assets    | node -e "console.log('títulos', JSON.parse(require('fs').readFileSync(0,'utf8')).data.length)"
   curl -s localhost:8080/api/positions | node -e "console.log('aportes', JSON.parse(require('fs').readFileSync(0,'utf8')).data.length)"
   ```

2b. **Pilha da Parte A** (decisão 4): rede, etiqueta da imagem da fase e as três cópias do Compose,
   que a Parte B também usa (na preparação dela, pule o `docker tag`, o `docker network create` e os
   três `pg_dump`, já feitos aqui):

   ```bash
   PG=QaF13Senha123
   docker tag renda-fixa-api-api:latest qaf13-fase:latest            # congela a imagem da fase
   docker network create qaf13-net
   docker exec fixedincome-db pg_dump -U fixedincome -d fixedincome --schema-only --no-owner --no-privileges > "$QA/b/schema.sql"
   docker exec fixedincome-db pg_dump -U fixedincome -d fixedincome --data-only --no-owner \
     -t holiday -t index_rate -t '"__EFMigrationsHistory"' > "$QA/b/mercado.sql"
   docker exec fixedincome-db pg_dump -U fixedincome -d fixedincome --data-only --no-owner \
     -T holiday -T index_rate -T '"__EFMigrationsHistory"' > "$QA/b/carteira.sql"
   docker run -d --name qaf13a-db --network qaf13-net -e POSTGRES_USER=qaf13 -e POSTGRES_PASSWORD=$PG \
     -e POSTGRES_DB=qaf13 postgres:16-alpine
   until docker exec qaf13a-db pg_isready -U qaf13 >/dev/null 2>&1; do sleep 1; done
   docker exec -i qaf13a-db psql -U qaf13 -d qaf13 -q < "$QA/b/schema.sql"
   docker exec -i qaf13a-db psql -U qaf13 -d qaf13 -q -v ON_ERROR_STOP=1 < "$QA/b/mercado.sql"
   docker exec -i qaf13a-db psql -U qaf13 -d qaf13 -q -v ON_ERROR_STOP=1 < "$QA/b/carteira.sql"
   sqla() { docker exec qaf13a-db psql -U qaf13 -d qaf13 -tAc "$1"; }
   sqla "$FOTO" > "$QA/antes-index-rate.txt"
   diff "$QA/antes-index-rate.txt" "$QA/compose-index-rate-antes.txt" && echo "cotações copiadas iguais"
   docker run -d --name qaf13a-fase --network qaf13-net -p 28115:8080 -e ASPNETCORE_ENVIRONMENT=Development \
     -e ConnectionStrings__DefaultConnection="Host=qaf13a-db;Port=5432;Database=qaf13;Username=qaf13;Password=$PG" \
     qaf13-fase:latest
   until curl -sf localhost:28115/health >/dev/null; do sleep 1; done
   curl -s localhost:28115/api/positions | node -e "console.log('aportes na cópia', JSON.parse(require('fs').readFileSync(0,'utf8')).data.length)"
   ```

   Esperado: o mesmo número de títulos e aportes da foto do Compose. A importação da `qaf13a-fase` pode
   acrescentar cotações logo ao subir: a A18 compara com `antes-index-rate.txt` e aceita só linhas
   novas depois do L inicial.

3. **Scripts de conferência** em `$QA/js`. Todos leem o texto dos números.

   `inv13.mjs` — as regras RN-15 a RN-18 conferidas por regra, item a item e título a título, sobre a
   resposta do consolidado. Exporta `confere(projeção, título)`, usada também pelo `linha.mjs`:

   ```bash
   cat > "$QA/js/inv13.mjs" <<'EOF'
   // uso: curl -s "$B/api/portfolio/summary[?referenceDate=...]" | node inv13.mjs
   import { readFileSync } from 'node:fs';
   const num = (k, v, c) => typeof v === 'number' ? c.source : v;
   const NUM = /^-?\d+(\.\d+)?$/, ISO = /^\d{4}-\d{2}-\d{2}$/;
   const big = s => { const t = String(s), neg = t.startsWith('-'); const [i, f = ''] = t.replace('-', '').split('.'); const v = BigInt(i + (f + '000000').slice(0, 6)); return neg ? -v : v; };
   const add = (d, n) => { const x = new Date(d + 'T00:00:00Z'); x.setUTCDate(x.getUTCDate() + n); return x.toISOString().slice(0, 10); };
   const dias = (a, b) => Math.round((Date.parse(b + 'T00:00:00Z') - Date.parse(a + 'T00:00:00Z')) / 864e5);
   // RN-03, com o texto da escala da tabela (D-2)
   const FAIXAS = [[0, '22.5'], [181, '20.0'], [361, '17.5'], [721, '15.0']];
   export function confere(p, a) {
     const e = [];
     for (const k of ['netYield', 'nextTaxRate', 'nextTaxRateDate', 'iofFreeDate']) if (!(k in p)) e.push(`${k} ausente`);
     for (const k of ['investedAmount', 'grossYield', 'netAmount', 'netYield', 'taxRate', 'elapsedDays']) if (!NUM.test(String(p[k]))) e.push(`${k} fora do formato numérico: ${p[k]}`);
     if (e.length) return e;
     const d = Number(p.elapsedDays), isentoIr = a.assetType === 'Lci' || a.assetType === 'Lca';
     // RN-15
     if (big(p.netYield) !== big(p.netAmount) - big(p.investedAmount)) e.push(`netYield ${p.netYield} != netAmount ${p.netAmount} - investedAmount ${p.investedAmount}`);
     if (big(p.grossYield) <= 0n && big(p.netYield) !== big(p.grossYield)) e.push(`rendimento ${p.grossYield} <= 0 e netYield ${p.netYield} diferente dele (sem IR nem IOF)`);
     if (big(p.grossYield) > 0n && big(p.netYield) < 0n) e.push(`netYield ${p.netYield} negativo com rendimento positivo`);
     if (/^-0(\.0+)?$/.test(p.netYield)) e.push(`netYield com zero negativo: ${p.netYield}`);
     // prazo até a data de cálculo (RN-02)
     const fim = p.referenceDate < a.maturityDate ? p.referenceDate : a.maturityDate;
     if (d !== dias(p.applicationDate, fim)) e.push(`elapsedDays ${d}, esperado ${dias(p.applicationDate, fim)}`);
     const atual = isentoIr ? '0' : FAIXAS.filter(([de]) => de <= d).at(-1)[1];
     if (big(p.taxRate) !== big(atual)) e.push(`taxRate ${p.taxRate} com d=${d}, esperado ${atual}`);
     // RN-16
     let ir = null;
     if (!isentoIr) { const f = FAIXAS.find(([de]) => de > d); if (f && add(p.applicationDate, f[0]) <= a.maturityDate) ir = [f[1], add(p.applicationDate, f[0])]; }
     if ((p.nextTaxRate === null) !== (p.nextTaxRateDate === null)) e.push(`nextTaxRate ${p.nextTaxRate} e nextTaxRateDate ${p.nextTaxRateDate} fora de par`);
     else if (ir === null && p.nextTaxRate !== null) e.push(`próxima faixa ${p.nextTaxRate}/${p.nextTaxRateDate}, esperado null/null`);
     else if (ir !== null && (p.nextTaxRate === null || big(p.nextTaxRate) !== big(ir[0]) || p.nextTaxRateDate !== ir[1])) e.push(`próxima faixa ${p.nextTaxRate}/${p.nextTaxRateDate}, esperado ${ir.join('/')}`);
     else if (ir !== null && p.nextTaxRate !== ir[0]) e.push(`ESCALA nextTaxRate ${p.nextTaxRate}, esperado o texto ${ir[0]}`);
     // RN-17
     const livre = add(p.applicationDate, 30);
     const iof = a.assetType !== 'Lca' && d < 30 && livre <= a.maturityDate ? livre : null;
     if (p.iofFreeDate !== iof) e.push(`iofFreeDate ${p.iofFreeDate}, esperado ${iof}`);
     for (const k of ['nextTaxRateDate', 'iofFreeDate']) if (p[k] !== null && !ISO.test(p[k])) e.push(`${k} fora de yyyy-MM-dd: ${p[k]}`);
     return e;
   }
   if (process.argv[1]?.endsWith('inv13.mjs')) {          // só quando chamado direto, não no import do linha.mjs
     const r = JSON.parse(readFileSync(0, 'utf8'), num);
     if (!r.data) { console.log('status', r.statusCode, r.message, '| data', r.data); process.exit(0); }
     const s = r.data, ps = s.positions, motivo = {}; let falhas = 0;
     for (const it of ps) {
       const p = it.projection, e = confere(p, it.asset);
       if (e.length && ++falhas <= 30) console.log('  item', p.positionId, it.asset.name, it.asset.assetType, p.applicationDate, '->', p.referenceDate, ':', e.join('; '));
       const m = p.nextTaxRate !== null ? 'com próxima faixa' : ['Lci', 'Lca'].includes(it.asset.assetType) ? 'nula: isento' : Number(p.elapsedDays) >= 721 ? 'nula: 15%' : 'nula: vencimento';
       motivo[m] = (motivo[m] || 0) + 1;
     }
     console.log(`referenceDate ${s.referenceDate}: itens ${ps.length}, divergentes ${falhas}; ${JSON.stringify(motivo)}; com iofFreeDate ${ps.filter(it => it.projection.iofFreeDate !== null).length}`);
     const somaNY = ps.reduce((x, it) => x + big(it.projection.netYield), 0n);
     const okRaiz = big(s.totalNetYield) === big(s.totalNetAmount) - big(s.totalInvestedAmount) && big(s.totalNetYield) === somaNY;
     console.log('raiz: totalNetYield', s.totalNetYield, okRaiz ? 'OK' : `DIVERGE (líquido - investido ${big(s.totalNetAmount) - big(s.totalInvestedAmount)}, soma dos itens ${somaNY}, em milionésimos)`);
     const ordem = [...new Set(ps.map(it => it.projection.assetId))], ids = s.byAsset.map(b => b.assetId);
     console.log('byAsset na ordem da primeira aparição em positions', JSON.stringify(ids) === JSON.stringify(ordem) ? 'OK' : `DIVERGE byAsset=${ids.join(' ')} positions=${ordem.join(' ')}`);
     const CH = 'assetId,positionCount,totalInvestedAmount,totalGrossAmount,totalGrossYield,totalTaxAmount,totalIofAmount,totalNetAmount,totalNetYield';
     const CAMPOS = [['totalInvestedAmount', 'investedAmount'], ['totalGrossAmount', 'grossAmount'], ['totalGrossYield', 'grossYield'], ['totalTaxAmount', 'taxAmount'], ['totalIofAmount', 'iofAmount'], ['totalNetAmount', 'netAmount'], ['totalNetYield', 'netYield']];
     let errT = 0;
     for (const b of s.byAsset) {
       const its = ps.filter(it => it.projection.assetId === b.assetId).map(it => it.projection), e = [];
       if (Object.keys(b).join(',') !== CH) e.push('chaves ' + Object.keys(b).join(','));
       if (Number(b.positionCount) !== its.length) e.push(`positionCount ${b.positionCount} != ${its.length} itens`);
       for (const [t, i] of CAMPOS) { const soma = its.reduce((x, p) => x + big(p[i]), 0n); if (soma !== big(b[t])) e.push(`${t} ${b[t]} != soma dos itens`); }
       if (big(b.totalNetAmount) !== big(b.totalGrossAmount) - big(b.totalTaxAmount) - big(b.totalIofAmount)) e.push('líquido != bruto - IR - IOF');
       if (e.length && ++errT <= 20) console.log('  título', b.assetId, e.join('; '));
     }
     console.log(`byAsset: ${s.byAsset.length} títulos, divergentes ${errT}`);
     for (const t of [...CAMPOS.map(c => c[0]), 'positionCount']) { const soma = s.byAsset.reduce((x, b) => x + big(b[t]), 0n); if (soma !== big(s[t])) console.log('  DIVERGE soma do byAsset em', t, String(soma), '(milionésimos) x raiz', s[t]); }
     const tipo = new Map(ps.map(it => [it.asset.id, it.asset.assetType]));
     for (const g of s.byAssetType) for (const k of ['positionCount', 'totalInvestedAmount', 'totalNetAmount']) {
       if (!(k in g)) continue;
       const soma = s.byAsset.filter(b => tipo.get(b.assetId) === g.assetType).reduce((x, b) => x + big(b[k]), 0n);
       if (soma !== big(g[k])) console.log('  DIVERGE byAssetType', g.assetType, k, g[k], 'x soma do byAsset', String(soma));
     }
     console.log('fim da conferência');
   }
   EOF
   ```

   `linha.mjs` — a mesma posição numa sequência de datas, com as regras e a monotonia (com a data
   avançando, `nextTaxRate` não sobe, `nextTaxRateDate` não recua e `iofFreeDate`, depois de nula, não
   volta):

   ```bash
   cat > "$QA/js/linha.mjs" <<'EOF'
   // uso: node linha.mjs <base> <positionId> <data> [<data> ...]
   import { confere } from './inv13.mjs';
   const [base, id, ...datas] = process.argv.slice(2);
   const num = (k, v, c) => typeof v === 'number' ? c.source : v;
   const get = async u => JSON.parse(await (await fetch(base + u)).text(), num);
   let asset = null, ant = null;
   for (const r of datas) {
     const resp = await get(`/api/positions/${id}/projection?referenceDate=${r}`);
     if (!resp.data) { console.log(r, resp.statusCode, resp.message); continue; }
     const d = resp.data; asset ??= (await get(`/api/assets/${d.assetId}`)).data;
     const e = confere(d, asset);
     if (ant) {
       if (ant.nextTaxRate !== null && d.nextTaxRate !== null && Number(d.nextTaxRate) > Number(ant.nextTaxRate)) e.push('nextTaxRate subiu');
       if (ant.nextTaxRateDate && d.nextTaxRateDate && d.nextTaxRateDate < ant.nextTaxRateDate) e.push('nextTaxRateDate recuou');
       if (ant.iofFreeDate === null && d.iofFreeDate !== null) e.push('iofFreeDate voltou depois de nula');
     }
     ant = d;
     console.log(r, `d=${d.elapsedDays} proj=${d.isProjected} est=${d.isEstimated} taxRate=${d.taxRate} next=${d.nextTaxRate}/${d.nextTaxRateDate} iofRate=${d.iofRate} iofFree=${d.iofFreeDate} netYield=${d.netYield}`, e.length ? 'DIVERGE ' + e.join('; ') : 'OK');
   }
   EOF
   ```

   `coerencia13.mjs` — os três endpoints na mesma data: `byAsset` de cada título contra
   `GET /api/assets/{id}/projection`; cada item da projeção por título contra o item do resumo, campo a
   campo no texto; itens do resumo (amostra de 40, ou todos com `--todos`) contra
   `GET /api/positions/{id}/projection` na `referenceDate` do item; e todo título da carteira com
   aporte incluído presente em `byAsset`:

   ```bash
   cat > "$QA/js/coerencia13.mjs" <<'EOF'
   // uso: node coerencia13.mjs <base> [referenceDate] [--todos]
   const args = process.argv.slice(2), todos = args.includes('--todos'), [base, ref] = args.filter(a => !a.startsWith('--')), q = ref ? `?referenceDate=${ref}` : '';
   const num = (k, v, c) => typeof v === 'number' ? c.source : v;
   const big = s => { const t = String(s), neg = t.startsWith('-'); const [i, f = ''] = t.replace('-', '').split('.'); const v = BigInt(i + (f + '000000').slice(0, 6)); return neg ? -v : v; };
   const get = async u => { const r = await fetch(base + u); return { status: r.status, body: JSON.parse(await r.text(), num) }; };
   const diff = (a, b) => [...new Set([...Object.keys(a), ...Object.keys(b)])].filter(k => a[k] !== b[k]).map(k => `${k}: ${a[k]} x ${b[k]}`);
   const T = ['positionCount', 'totalInvestedAmount', 'totalGrossAmount', 'totalGrossYield', 'totalTaxAmount', 'totalIofAmount', 'totalNetAmount', 'totalNetYield'];
   const s = await get(`/api/portfolio/summary${q}`);
   if (s.status !== 200) { console.log('resumo', s.status, s.body.message); process.exit(0); }
   const S = s.body.data; let dt = 0, di = 0, dp = 0, esc = 0, outraData = 0, faltam = 0;
   for (const b of S.byAsset) {
     const t = await get(`/api/assets/${b.assetId}/projection${q}`);
     if (t.status !== 200) { dt++; console.log('título', b.assetId, t.status, t.body.message); continue; }
     const d = t.body.data;
     if (d.referenceDate !== S.referenceDate) { outraData++; console.log('título', b.assetId, `referenceDate ${d.referenceDate} x resumo ${S.referenceDate} (D-5)`); }
     const e = T.filter(k => big(d[k]) !== big(b[k])).map(k => `${k}: título ${d[k]} x byAsset ${b[k]}`);
     if (e.length) { dt++; console.log('título', b.assetId, e.join('; ')); }
     esc += T.filter(k => big(d[k]) === big(b[k]) && d[k] !== b[k]).length;
     const doResumo = new Map(S.positions.filter(it => it.projection.assetId === b.assetId).map(it => [it.projection.positionId, it.projection]));
     if (d.positions.length !== doResumo.size) { di++; console.log('título', b.assetId, `itens ${d.positions.length} x resumo ${doResumo.size}`); }
     for (const it of d.positions) { const x = doResumo.get(it.positionId), e2 = x ? diff(it, x) : ['ausente no resumo']; if (e2.length) { di++; console.log('item', it.positionId, e2.join('; ')); } }
   }
   const noByAsset = new Set(S.byAsset.map(b => b.assetId));
   for (const a of (await get('/api/assets')).body.data) {
     if (noByAsset.has(a.id)) continue;
     const t = await get(`/api/assets/${a.id}/projection${q}`);
     if (t.status === 200 && Number(t.body.data.positionCount) > 0) { faltam++; console.log('título com aporte incluído fora do byAsset', a.id, a.name, t.body.data.positionCount); }
   }
   const passo = todos ? 1 : Math.max(1, Math.ceil(S.positions.length / 40)), amostra = S.positions.filter((_, k) => k % passo === 0);
   for (const it of amostra) {
     const p = await get(`/api/positions/${it.projection.positionId}/projection?referenceDate=${it.projection.referenceDate}`);
     const e = p.status === 200 ? diff(it.projection, p.body.data) : [`status ${p.status} ${p.body.message}`];
     if (e.length) { dp++; console.log('posição', it.projection.positionId, e.join('; ')); }
   }
   console.log(`resumo ${S.referenceDate}: byAsset ${S.byAsset.length} títulos (${dt} divergentes, ${outraData} com outra referenceDate, ${esc} totais de mesmo valor e texto diferente, ${faltam} faltando); itens divergentes ${di}; posições conferidas ${amostra.length} (${dp} divergentes)`);
   EOF
   ```

   `compara.mjs`, `urls.mjs` e `swdiff.mjs` (só Parte B) e `carga.mjs` (B03) estão na preparação da
   Parte B.

4. **Funções da sessão** (as da F12, com o prefixo novo):

   ```bash
   B=http://localhost:28115                # Parte A (pilha qaf13a; nunca a 8080)
   get()  { curl -s -w '\n%{http_code} %{time_total}s\n' "$B$1"; }
   post() { curl -s -X POST -H 'Content-Type: application/json' -d "$2" "$B$1"; echo; }
   put()  { curl -s -X PUT  -H 'Content-Type: application/json' -d "$2" "$B$1"; echo; }
   id()   { node -e "const j=JSON.parse(require('fs').readFileSync(0,'utf8')); process.stdout.write(j.data ? j.data.id : 'ERRO '+j.statusCode+' '+j.message)"; }
   chaves() { node -e "const d=JSON.parse(require('fs').readFileSync(0,'utf8')).data; console.log(Object.keys(d).join(','))"; }
   titulo() { post /api/assets "{\"name\":\"$1\",\"issuer\":\"${1%% *} Banco\",\"assetType\":\"$2\",\"indexType\":\"$3\",\"rate\":$4,\"issueDate\":\"$5\",\"maturityDate\":\"$6\"}" | id; }
   aporte() { post /api/positions "{\"assetId\":\"$1\",\"investedAmount\":$2,\"applicationDate\":\"$3\"}" | id; }
   novos()  { grep -oE '"(netYield|nextTaxRate|nextTaxRateDate|iofFreeDate|totalNetYield)":("[^"]*"|[^,}]*)' ; }
   ```

   Guarde cada id devolvido em `$QA/a/ids.txt` (ou `b/ids.txt`) com o nome do título: vai para a
   seção "Dados criados" do relatório. Um `ERRO` no lugar do id é registrado no cenário.

5. **Leitura das datas.** Crie primeiro o título vazio, que dá D sem depender da carteira:

   ```bash
   VAZIO=$(titulo "QA-F13A VAZIO" Cdb PreFixed 10 2026-01-01 2028-01-03); echo "VAZIO $VAZIO" >> "$QA/a/ids.txt"
   date -u '+%F %T UTC'; date -u -d '-3 hours' '+%F %T BRT'
   get "/api/indexes/Cdi?from=2026-11-01&to=2026-12-31" | tail -c 300      # L é o último item
   get "/api/indexes/Ipca?from=2025-09-01&to=2026-12-31" | tail -c 300     # M é o último item
   D=$(curl -s "$B/api/assets/$VAZIO/projection" | node -e "process.stdout.write(JSON.parse(require('fs').readFileSync(0,'utf8')).data.referenceDate)"); echo "D=$D"
   T=$(date -u -d '-3 hours' '+%F'); echo "T=$T"
   P=2026-11-26; M=2026-10      # P: maior dia útil antes de T (feriados de holiday); M: o último mês da listagem
   ```

   Esperado: D é o dia útil seguinte a L. Diferente disso é **BUG (alta)** de regressão da F12: pare e
   registre.

### Preparação da Parte B (pilha isolada, fase e `main` lado a lado)

Só depois do passo 1 (a imagem `renda-fixa-api-api:latest` precisa ter o código da fase). A imagem
da `main` é montada de `git archive`, sem tocar o repositório.

```bash
PG=QaF13Senha123
# a etiqueta qaf13-fase:latest, a rede qaf13-net e os três dumps (schema, mercado, carteira) já vêm do passo 2b
git -C "$REPO" rev-parse origin/main > "$QA/b/main-commit.txt"
git -C "$REPO" archive origin/main | tar -x -C "$QA/main"
docker build -q -t qaf13-main:latest "$QAW/main"                # caminho C:/...: o docker.exe não entende /c/...
docker run -d --name qaf13-db --network qaf13-net -e POSTGRES_USER=qaf13 -e POSTGRES_PASSWORD=$PG \
  -e POSTGRES_DB=qaf13 postgres:16-alpine
until docker exec qaf13-db pg_isready -U qaf13 >/dev/null 2>&1; do sleep 1; done
docker exec -i qaf13-db psql -U qaf13 -d qaf13 -q < "$QA/b/schema.sql"
docker exec -i qaf13-db psql -U qaf13 -d qaf13 -q -v ON_ERROR_STOP=1 < "$QA/b/mercado.sql"
sqlb() { docker exec qaf13-db psql -U qaf13 -d qaf13 -tAc "$1"; }
sqlb "select index_type, count(*), max(reference_date) from index_rate group by 1 order by 1" | tee "$QA/b/base-index-rate.txt"
sqlb "create table qa_index_rate_bkp as table index_rate"
for n in fase main; do p=$([ $n = fase ] && echo 28113 || echo 28114)
  docker run -d --name qaf13-$n --network qaf13-net -p $p:8080 -e ASPNETCORE_ENVIRONMENT=Development \
    -e ConnectionStrings__DefaultConnection="Host=qaf13-db;Port=5432;Database=qaf13;Username=qaf13;Password=$PG" \
    -e IndexRatesImport__Enabled=false -e HTTPS_PROXY=http://127.0.0.1:9 -e HTTP_PROXY=http://127.0.0.1:9 \
    qaf13-$n:latest; done
until curl -sf localhost:28113/health >/dev/null && curl -sf localhost:28114/health >/dev/null; do sleep 1; done
BF=http://localhost:28113; BM=http://localhost:28114
curl -s $BF/swagger/v1/swagger.json | grep -c '"netYield"'      # 1 ou mais: fase
curl -s $BM/swagger/v1/swagger.json | grep -c '"netYield"'      # 0: main
docker logs qaf13-fase 2>&1 | grep -iE 'hosting environment|import' | head -3
```

A carteira (`carteira.sql`) só é carregada no B00, depois do teste com a carteira vazia.

Funções de manipulação de cotações, só em `qaf13-db` (as da F12; `index_type` é texto):

```bash
restaura()  { sqlb "begin; delete from index_rate; insert into index_rate select * from qa_index_rate_bkp; commit;"
              sqlb "select index_type, count(*), max(reference_date) from index_rate group by 1 order by 1" | diff - "$QA/b/base-index-rate.txt" && echo "cotações restauradas"; }
cdi_ate()   { sqlb "delete from index_rate where index_type='Cdi' and reference_date > '$1'"; }
tira()      { sqlb "delete from index_rate where index_type='$1' and reference_date = '$2'"; }
sintetico() { sqlb "insert into index_rate (id, index_type, reference_date, value) values (gen_random_uuid(), 'Cdi', '$1', $2) on conflict do nothing"; }
```

Depois de cada manipulação, confira pelas duas APIs (`GET /api/indexes/...`) que a mudança aparece.
Se não aparecer, `docker restart qaf13-fase qaf13-main`, repita e registre como **RISCO** (cache que a
spec não prevê). **Cada cenário que mexe em cotação começa e termina com `restaura`.**

Scripts da Parte B:

`compara.mjs` — a mesma rota na `main` e na fase: mesmo status; cada campo da `main` presente na fase
com o **mesmo texto**; as chaves antigas na mesma ordem e as novas só depois delas; listas casadas por
`positionId`, `id` ou `assetType` (a ordem entre aportes do mesmo dia não é garantida, RN-14); e o
conjunto de chaves novas por rota e caminho:

```bash
cat > "$QA/js/compara.mjs" <<'EOF'
// uso: node compara.mjs <baseMain> <baseFase> < urls.txt   (uma rota por linha; # comenta)
import { readFileSync } from 'node:fs';
const [M, F] = process.argv.slice(2);
const num = (k, v, c) => typeof v === 'number' ? c.source : v;
const get = async u => { const r = await fetch(u); const t = await r.text(); let b; try { b = JSON.parse(t, num); } catch { b = t; } return { status: r.status, b }; };
const chave = o => o && typeof o === 'object' ? (o.positionId ?? o.projection?.positionId ?? o.id ?? o.assetType ?? null) : null;
const novas = new Map(); let rotas = 0, div = 0;
function cmp(m, f, cam, e, rota) {
  if (Array.isArray(m)) {
    if (!Array.isArray(f) || f.length !== m.length) return e.push(`${cam}: lista ${Array.isArray(f) ? f.length : typeof f} na fase x ${m.length} na main`);
    const porChave = m.length && m.every(x => chave(x) !== null) ? new Map(f.map(x => [chave(x), x])) : null;
    m.forEach((x, k) => cmp(x, porChave ? porChave.get(chave(x)) : f[k], `${cam}[]`, e, rota));
    return;
  }
  if (m && typeof m === 'object') {
    if (!f || typeof f !== 'object') return e.push(`${cam}: objeto na main x ${f} na fase`);
    const km = Object.keys(m), kf = Object.keys(f);
    for (const k of km) if (!(k in f)) e.push(`${cam}.${k}: ausente na fase`);
    if (JSON.stringify(kf.filter(k => k in m)) !== JSON.stringify(km)) e.push(`${cam}: ordem das chaves antigas mudou`);
    const extra = kf.filter(k => !(k in m)), ultima = Math.max(-1, ...km.map(k => kf.indexOf(k)));
    if (extra.length) novas.set(`${rota} ${cam}`, extra.join(','));
    if (extra.some(k => kf.indexOf(k) < ultima)) e.push(`${cam}: chave nova antes de uma antiga`);
    for (const k of km) if (k in f) cmp(m[k], f[k], `${cam}.${k}`, e, rota);
    return;
  }
  if (m !== f) e.push(`${cam}: main ${m} x fase ${f}`);
}
const urls = readFileSync(0, 'utf8').split(/\r?\n/).map(s => s.trim()).filter(s => s && !s.startsWith('#'));
for (const u of urls) {
  rotas++; const [m, f] = await Promise.all([get(M + u), get(F + u)]), e = [];
  if (m.status !== f.status) e.push(`status main ${m.status} x fase ${f.status}`);
  cmp(m.b, f.b, '$', e, u.split('?')[0].replace(/[0-9a-f]{8}-[0-9a-f-]{27}/g, '{id}'));
  if (e.length) { div++; console.log('DIVERGE', u); e.slice(0, 15).forEach(x => console.log('   ', x)); }
}
console.log(`rotas ${rotas}, divergentes ${div}`);
console.log('chaves novas por rota e caminho:'); for (const [c, k] of novas) console.log('  ', c, '->', k);
EOF
```

`urls.mjs` — as rotas de leitura sobre a carteira inteira: resumo sem data e nas datas dadas, cada
título (detalhe e projeção, sem data e nas datas) e cada aporte (projeção sem data e nas duas
primeiras datas):

```bash
cat > "$QA/js/urls.mjs" <<'EOF'
// uso: node urls.mjs <base> <data> [<data> ...] > urls.txt
const [base, ...datas] = process.argv.slice(2);
const j = async u => (await (await fetch(base + u)).json()).data;
const titulos = await j('/api/assets'), aportes = await j('/api/positions');
const qs = ['', ...datas.map(d => `?referenceDate=${d}`)], out = ['/api/assets', '/api/positions'];
for (const q of qs) out.push(`/api/portfolio/summary${q}`);
for (const t of titulos) { out.push(`/api/assets/${t.id}`); for (const q of qs) out.push(`/api/assets/${t.id}/projection${q}`); }
for (const p of aportes) for (const q of qs.slice(0, 3)) out.push(`/api/positions/${p.id ?? p.positionId}/projection${q}`);
console.log(out.join('\n'));
EOF
cat > "$QA/b/fixos.txt" <<'EOF'
/api/portfolio/summary?referenceDate=2000-01-01
/api/portfolio/summary?referenceDate=abc
/api/portfolio/summary?referenceDate=2051-01-02
/api/assets/00000000-0000-0000-0000-000000000000/projection
/api/assets/abc/projection
/api/positions/00000000-0000-0000-0000-000000000000/projection?referenceDate=2026-11-30
/api/positions/xyz/projection
/api/indexes/Cdi?from=2026-11-01&to=2026-11-30
/api/indexes/Ipca?from=2025-09-01&to=2026-12-31
/api/indexes/Cdi/accumulated?from=2026-09-01&to=2026-10-01
/api/indexes/Ipca/accumulated?from=2026-01-01&to=2026-10-01
/api/indexes/PreFixed?from=2026-09-14&to=2026-09-15
/api/foo
EOF
```

`swdiff.mjs` — dois `swagger.json`: rotas e schemas antigos intactos (cada propriedade com o mesmo
`type`, `format`, `nullable`, `$ref` e `items`), propriedades antigas no início e na mesma ordem, e a
lista do que é novo:

```bash
cat > "$QA/js/swdiff.mjs" <<'EOF'
// uso: node swdiff.mjs <antigo.json> <novo.json>
import { readFileSync } from 'node:fs';
const [a, b] = process.argv.slice(2).map(f => JSON.parse(readFileSync(f, 'utf8')));
const e = [], desc = v => `${v.type || v.$ref}${v.format ? '/' + v.format : ''}${v.nullable ? '?' : ''}${v.items ? '<' + (v.items.$ref || v.items.type) + '>' : ''}`;
for (const [p, ops] of Object.entries(a.paths)) { if (!b.paths[p]) e.push(`rota ${p} sumiu`); else if (JSON.stringify(ops) !== JSON.stringify(b.paths[p])) e.push(`rota ${p} mudou`); }
for (const [n, s] of Object.entries(a.components.schemas)) {
  const t = b.components.schemas[n]; if (!t) { e.push(`schema ${n} sumiu`); continue; }
  const pa = s.properties || {}, pb = t.properties || {};
  for (const [k, v] of Object.entries(pa)) if (JSON.stringify(v) !== JSON.stringify(pb[k])) e.push(`${n}.${k}: ${JSON.stringify(v)} x ${JSON.stringify(pb[k])}`);
  if (JSON.stringify(Object.keys(pb).slice(0, Object.keys(pa).length)) !== JSON.stringify(Object.keys(pa))) e.push(`${n}: propriedades antigas fora do início ou da ordem`);
  if (JSON.stringify({ ...s, properties: undefined }) !== JSON.stringify({ ...t, properties: undefined })) e.push(`${n}: atributos do schema mudaram`);
  const novos = Object.keys(pb).filter(k => !(k in pa)); if (novos.length) console.log('novos em', n, '->', novos.map(k => `${k}:${desc(pb[k])}`).join(', '));
}
for (const n of Object.keys(b.components.schemas).filter(n => !a.components.schemas[n])) console.log('schema novo', n, '->', Object.entries(b.components.schemas[n].properties || {}).map(([k, v]) => `${k}:${desc(v)}`).join(', '));
console.log('rotas novas:', Object.keys(b.paths).filter(p => !a.paths[p]).join(', ') || 'nenhuma');
console.log(e.length ? e.join('\n') : 'nenhuma rota, schema ou propriedade antiga alterada');
EOF
```

Tempo de resposta (mediana de 5, depois de uma chamada de aquecimento descartada):

```bash
tempo() { for k in 0 1 2 3 4 5; do curl -s -o /dev/null -w '%{time_total} %{size_download}\n' "$1$2"; done \
          | tail -5 | sort -n | awk '{t[NR]=$1; s=$2} END {printf "mediana %ss, %s bytes\n", t[3], s}'; }
```

Se o Docker não estiver disponível, todo o roteiro fica `NÃO EXECUTADO`, exceto a leitura do README,
do `.http` e do snapshot do front (A15 e A16 só por leitura).

## Cenários

Legenda: **BUG** quando a resposta contraria a especificação, a spec da fase, o README ou uma regra do
agente; **RISCO** quando é defensável pela spec, mas surpreende o front ou o usuário. Cada cenário traz
prioridade (P1 a P3), parte e tempo estimado.

Severidade dos bugs no relatório:

- **alta**: 500; `netYield` ou `totalNetYield` diferente de líquido − investido (mesmo em R$ 0,01);
  `nextTaxRateDate` ou `iofFreeDate` diferente da regra (um dia a mais ou a menos, mês ou ano no lugar
  de dias corridos), ou presente onde a regra manda nulo, ou nulo onde manda data; `nextTaxRate`
  incoerente com a faixa; total de `byAsset` diferente da soma dos itens do título ou da projeção por
  título na mesma data informada; título com aporte incluído fora de `byAsset`, ou título sem aporte
  incluído dentro; **qualquer campo antigo com valor diferente do da `main`** para os mesmos dados.
- **média**: `nextTaxRate` e `nextTaxRateDate` fora de par (um nulo e o outro não); campo novo ausente
  em vez de `null`; ordem de `byAsset` diferente da primeira aparição em `positions` na mesma resposta;
  chave antiga fora da ordem ou chave nova antes de uma antiga; `swagger.json` diferente da resposta
  real; resposta de erro com `data` não nulo; desempenho do consolidado mais que duas vezes pior que o
  da `main` nos mesmos dados.
- **baixa**: escala ou formatação de número sem efeito de valor (`20` contra `20.0`, `0` contra
  `0.00`); divergência de documentação; `.http` ou exemplo do README que não funciona como descrito.

### Parte A — Pilha `qaf13a` (cópia do Compose), cotações reais (`QA-F13A`)

Onde um cenário desta parte disser "Compose", "porta 8080" ou "carteira real", leia `qaf13a-fase`
(`$B`, porta 28115) e a cópia da carteira em `qaf13a-db`. `sqla` aponta para `qaf13a-db`.

#### QA13-A00 — Reconhecimento [P1, A, 10 min]

Passos 0 a 5 da preparação comum. Registre também a linha de base da carteira real, antes de criar
qualquer título da F13:

```bash
curl -s -o /dev/null -w '%{http_code} %{time_total}s %{size_download}B\n' "$B/api/portfolio/summary"
curl -s "$B/api/portfolio/summary" | node "$QAW/js/inv13.mjs" | tee "$QA/a/a00.txt"
```

Esperado: resumo 200 (a F12 viu 200; com 400, anote a mensagem e o título culpado: a varredura da A10
passa a usar a projeção por título), as regras sem divergência em nenhum item da carteira real e o
número de itens e de títulos de `byAsset` registrado. Cada divergência aqui já é achado: é a primeira
vez que as regras rodam sobre CDI, IPCA, LCI, LCA, Tesouro e vencidos reais ao mesmo tempo.

#### QA13-A01 — Carteira de teste da Parte A [P1, A, 20 min]

```bash
A181=$(date -d "$D -181 days" +%F)             # aporte que cruza a faixa de 180 para 181 dias exatamente em D
PRE=$(titulo "QA-F13A PRE10" Cdb PreFixed 10 2025-01-01 2027-01-01); aporte $PRE 1000 2025-04-01; aporte $PRE 1000 2025-04-01
LCI=$(titulo "QA-F13A LCI PRE10" Lci PreFixed 10 2025-01-01 2027-01-01); aporte $LCI 1500 2025-04-02
CDI=$(titulo "QA-F13A CDI100" Cdb Cdi 100 2026-01-01 2030-01-02)
P_CDI1=$(aporte $CDI 1000 2026-11-16); P_CDI2=$(aporte $CDI 1000 2026-06-01); P_CDI3=$(aporte $CDI 1000 $A181)
CDI30=$(titulo "QA-F13A CDI100 VENCE+30" Cdb Cdi 100 2026-11-16 2026-12-16); P_CDI30=$(aporte $CDI30 1000 2026-11-16)
CDI181=$(titulo "QA-F13A CDI100 VENCE+181" Cdb Cdi 100 2026-06-01 2026-11-29); P_CDI181=$(aporte $CDI181 1000 2026-06-01)
CURTO=$(titulo "QA-F13A PRE CURTO15" Cdb PreFixed 10 2026-11-16 2026-12-01); P_CURTO=$(aporte $CURTO 1000 2026-11-16)
VESP=$(titulo "QA-F13A PRE VESPERA" Cdb PreFixed 10 2026-01-01 2026-12-01); P_VESP1=$(aporte $VESP 1000 2026-11-30); P_VESP2=$(aporte $VESP 1000 2026-12-01)
IPCA=$(titulo "QA-F13A IPCA6" Cdb Ipca 6 2026-01-01 2028-01-03); P_IPCA1=$(aporte $IPCA 1000 2026-10-01); P_IPCA2=$(aporte $IPCA 1000 2026-11-03)
TDPRE=$(titulo "QA-F13A TESOURO PRE11" TreasuryBond PreFixed 11 2025-01-01 2029-01-01); P_TDPRE=$(aporte $TDPRE 1000 2026-11-16)
TDIPCA=$(titulo "QA-F13A TESOURO IPCA6" TreasuryBond Ipca 6 2026-01-01 2035-05-15); P_TDIPCA=$(aporte $TDIPCA 1000 2026-11-03)
LCICDI=$(titulo "QA-F13A LCI CDI95" Lci Cdi 95 2026-01-01 2028-01-03); P_LCICDI=$(aporte $LCICDI 1000 2026-11-16)
LCA=$(titulo "QA-F13A LCA PRE9" Lca PreFixed 9 2026-01-01 2028-01-03); P_LCA1=$(aporte $LCA 1000 2026-11-16); P_LCA2=$(aporte $LCA 500 2026-12-15)
FUT=$(titulo "QA-F13A FUTURO PRE10" Cdb PreFixed 10 2026-01-01 2028-01-03); P_FUT=$(aporte $FUT 1000 2026-12-15)
FUTV=$(titulo "QA-F13A FUTURO PERTO VENC" Cdb PreFixed 10 2026-01-01 2027-01-10); P_FUTV=$(aporte $FUTV 1000 2026-12-15)
VENC=$(titulo "QA-F13A VENCIDO PRE" Cdb PreFixed 10 2024-01-01 2025-01-02); P_VENC1=$(aporte $VENC 1000 2024-01-02); P_VENC2=$(aporte $VENC 1000 2024-12-20)
DEFL=$(titulo "QA-F13A IPCA2 DEFLACAO" Cdb Ipca 2 2022-01-01 2030-01-02)      # o aporte sai na A08, depois de escolher o mês
CAL=$(titulo "QA-F13A PRE 2050" Cdb PreFixed 10 2049-01-04 2055-01-04); P_CAL=$(aporte $CAL 1000 2050-12-20)
SEM=$(titulo "QA-F13A SEM APORTE" Cdb PreFixed 10 2026-01-01 2028-01-03)
DEP=$(titulo "QA-F13A TODOS DEPOIS" Cdb PreFixed 10 2026-01-01 2028-01-03); aporte $DEP 1000 2026-11-20; aporte $DEP 1000 2026-11-24
MD1=$(titulo "QA-F13A MESMO DIA 1" Cdb PreFixed 10 2026-01-01 2028-01-03); aporte $MD1 1000 2026-11-24
MD2=$(titulo "QA-F13A MESMO DIA 2" Lci PreFixed 10 2026-01-01 2028-01-03); aporte $MD2 1000 2026-11-24
MD3=$(titulo "QA-F13A MESMO DIA 3" Lca PreFixed 10 2026-01-01 2028-01-03); aporte $MD3 1000 2026-11-24
DEL=$(titulo "QA-F13A REMOVIVEL" Cdb PreFixed 10 2026-01-01 2028-01-03)
```

Guarde todos os ids. Esperado: todos 201/200. O aporte `P_VESP2` no próprio dia do vencimento deve ser
aceito (RN-05 só rejeita data **posterior** ao vencimento); se for recusado, registre como observação
fora da fase e siga sem ele. O título `CAL` vence depois da cobertura do calendário (2050): se a criação
for recusada, a A09 fica `NÃO EXECUTADO` com a mensagem.

Âncoras de data (com D = 2026-11-26; `d` é o prazo da RN-16; "—" é nulo). O `inv13.mjs` e o
`linha.mjs` recalculam tudo por regra; a tabela é para o executor reconhecer o resultado:

| Aporte | Data pedida | `d` | taxRate | nextTaxRate / nextTaxRateDate | iofFreeDate (iofRate) |
|---|---|---|---|---|---|
| `P_CDI1` 2026-11-16 | D | 10 | 22.5 | 20.0 / 2027-05-16 (domingo) | 2026-12-16 |
| `P_CDI1` | 2026-12-16 | 30 | 22.5 | 20.0 / 2027-05-16 | — (0) |
| `P_CDI1` | 2027-05-16 | 181 | 20.0 | 17.5 / 2027-11-12 | — |
| `P_CDI2` 2026-06-01 | D | 178 | 22.5 | 20.0 / 2026-11-29 (domingo) | — |
| `P_CDI2` | 2026-11-29 | 181 | 20.0 | 17.5 / 2027-05-28 | — |
| `P_CDI3` D − 181 (2026-05-29) | D − 1 (2026-11-25) | 180 | 22.5 | 20.0 / **D** | — |
| `P_CDI3` | D | 181 | **20.0** | 17.5 / 2027-05-25 | — |
| `P_CDI30` 2026-11-16, vence 2026-12-16 | D | 10 | 22.5 | — / — (2027-05-16 > vencimento) | **2026-12-16** (= vencimento) |
| `P_CDI30` | 2026-12-15 | 29 | 22.5 | — / — | 2026-12-16 (3) |
| `P_CDI30` | 2026-12-16 e 2027-01-15 | 30 | 22.5 | — / — | — (0) |
| `P_CDI181` 2026-06-01, vence 2026-11-29 | D, T e 2026-11-28 | 178 a 180 | 22.5 | 20.0 / **2026-11-29** (= vencimento) | — |
| `P_CDI181` | 2026-11-29 e 2027-01-15 | 181 | 20.0 | — / — (2027-05-28 > vencimento) | — |
| `P_CURTO` 2026-11-16, vence 2026-12-01 | qualquer | ≤ 15 | 22.5 | — / — | — (2026-12-16 > vencimento) |
| `P_VESP1` 2026-11-30, vence 2026-12-01 | 2026-11-30 / 2026-12-01 / 2027-01-15 | 0 / 1 / 1 | 22.5 | — / — | — (0 / 96 / 96) |
| `P_VESP2` 2026-12-01 (= vencimento) | 2026-12-01 e depois | 0 | 22.5 | — / — | — (0) |
| `P_IPCA1` 2026-10-01 | D | 56 | 22.5 | 20.0 / 2027-03-31 | — |
| `P_IPCA2` 2026-11-03 | D | 23 | 22.5 | 20.0 / 2027-05-03 | 2026-12-03 |
| `P_TDPRE` 2026-11-16 | D | 10 | 22.5 | 20.0 / 2027-05-16 | 2026-12-16 |
| `P_TDIPCA` 2026-11-03 | D | 23 | 22.5 | 20.0 / 2027-05-03 | 2026-12-03 |
| `P_LCICDI` 2026-11-16 | D | 10 | 0 | — / — | 2026-12-16 |
| `P_LCA1` 2026-11-16 | D | 10 | 0 | — / — | — (0) |
| `P_FUT` 2026-12-15 | sem data (item em 2026-12-15) | 0 | 22.5 | 20.0 / 2027-06-14 | 2027-01-14 (0) |
| `P_FUTV` 2026-12-15, vence 2027-01-10 | sem data | 0 | 22.5 | — / — | — (2027-01-14 > vencimento) |
| `P_LCA2` 2026-12-15 | sem data | 0 | 0 | — / — | — |
| `P_VENC1` 2024-01-02, vence 2025-01-02 | 2025-01-02 e depois | 366 | 17.5 | — / — (2025-12-23 > vencimento) | — |
| `P_VENC2` 2024-12-20 | 2024-12-31 | 11 | 22.5 | — / — | — (63) (2025-01-19 > vencimento) |
| `P_VENC2` | 2025-01-02 e depois | 13 | 22.5 | — / — | — (56) |
| `P_CAL` 2050-12-20 | 2050-12-30 | 10 | 22.5 | 20.0 / **2051-06-19** | **2051-01-19** |
| `PRE` 2025-04-01 (cada) | 2025-04-22 | 21 | 22.5 | 20.0 / 2025-09-29 | 2025-05-01 (30) |
| `LCI` 2025-04-02 | 2025-04-22 | 20 | 0 | — / — | 2025-05-02 (33) |

Por fim, a âncora do caso 16 numa carteira suja (a validação só o fez num banco vazio):

```bash
curl -s "$B/api/portfolio/summary?referenceDate=2025-04-22" | node -e "
const d=JSON.parse(require('fs').readFileSync(0,'utf8'),(k,v,c)=>typeof v==='number'?c.source:v).data;
for (const id of process.argv.slice(1)) console.log(JSON.stringify(d.byAsset.find(b=>b.assetId===id)))" $PRE $LCI
```

Esperado: as linhas A e B da tabela do caso 16 da spec (A: 2 / 2000.00 / 2009.86 / 9.86 / 1.56 / 2.96 /
2005.34 / 5.34; B: 1 / 1500.00 / 1506.82 / 6.82 / 0.00 / 2.25 / 1504.57 / 4.57), com os outros títulos
da carteira real no mesmo resumo.

- BUG (alta): os totais de `PRE` ou de `LCI` diferentes do caso 16 só porque há outros títulos.

#### QA13-A02 — Linha do tempo de um CDI com dias projetados [P1, A, 20 min]

```bash
node "$QAW/js/linha.mjs" "$B" $P_CDI1 2026-11-16 2026-11-17 $D $T 2026-12-14 2026-12-15 2026-12-16 2026-12-17 \
  2027-05-14 2027-05-15 2027-05-16 2027-05-17 2027-11-11 2027-11-12 2028-11-05 2028-11-06 2028-11-07 2029-12-31 | tee "$QA/a/a02.txt"
node "$QAW/js/linha.mjs" "$B" $P_CDI2 2026-11-25 $D $T 2026-11-28 2026-11-29 2026-11-30 2027-05-27 2027-05-28 2028-05-21 2028-05-22
node "$QAW/js/linha.mjs" "$B" $P_CDI3 $(date -d "$D -1 day" +%F) $D $T
```

Esperado (RN-16, RN-17; RN-16: "não usa cotação, não é projetada e não muda `isProjected`"): `OK` em
todas as linhas, com as âncoras da A01; `isProjected` verdadeiro nas datas depois de T e falso em D (a
data padrão nunca projeta CDI); as datas novas iguais nas linhas projetadas e nas não projetadas da
mesma faixa. O `P_CDI3` é o caso da faixa virando exatamente na data padrão: em D − 1, `nextTaxRateDate`
é D; em D, `taxRate` já é `20.0` e `nextTaxRate` `17.5`.

- BUG (alta): qualquer `DIVERGE` do `inv13` (data errada, nulo errado, faixa incoerente); `nextTaxRate`
  subindo ou `nextTaxRateDate` recuando com a data avançando.
- BUG (média): `isProjected` mudando por causa dos campos novos (diferente do mesmo pedido na `main`,
  conferido na B01).

#### QA13-A03 — IPCA+ projetado e Tesouro Direto [P1, A, 15 min]

```bash
for p in $P_IPCA1 $P_IPCA2 $P_TDIPCA $P_TDPRE; do echo "== $p"
  node "$QAW/js/linha.mjs" "$B" $p $D $T 2026-11-30 2026-12-02 2026-12-03 2026-12-15 2027-03-30 2027-03-31 2027-05-02 2027-05-03; done | tee "$QA/a/a03.txt"
get "/api/assets/$TDIPCA/projection" | novos
```

Esperado: `OK` em todas; nos IPCA+, `isProjected` verdadeiro em D e nas datas seguintes (o mês de hoje
tem dia útil no intervalo, RN-12) e `isEstimated` falso com M = 2026-10; as datas novas são as das
âncoras **com e sem as marcas** (RN-16: a data não depende de cotação); o Tesouro Direto recebe
`nextTaxRate` e `iofFreeDate` como um CDB (RN-17: "LCI, CDB e Tesouro Direto pagam IOF e recebem a
data"), com `iofFreeDate` 2026-12-03 até 2026-12-02 e nula de 2026-12-03 em diante.

- BUG (alta): Tesouro sem `nextTaxRate` ou sem `iofFreeDate` com `d` < 30; data diferente entre a
  linha projetada e a mesma faixa sem projeção.

#### QA13-A04 — LCI e LCA nos três endpoints, `null` presente [P1, A, 10 min]

```bash
for r in "" "?referenceDate=$D" "?referenceDate=2026-11-30"; do echo "== ${r:-sem data}"
  for p in $P_LCICDI $P_LCA1; do printf 'posição %s: ' ${p:0:8}; curl -s "$B/api/positions/$p/projection${r:-?referenceDate=$D}" | novos | tr '\n' ' '; echo; done
  for t in $LCICDI $LCA $MD2 $MD3; do printf 'título %s: ' ${t:0:8}; curl -s "$B/api/assets/$t/projection$r" | novos | tr '\n' ' '; echo; done
  printf 'resumo: '; curl -s "$B/api/portfolio/summary$r" | node -e "
const d=JSON.parse(require('fs').readFileSync(0,'utf8')).data, ids=process.argv.slice(1);
for (const it of d.positions.filter(x=>ids.includes(x.asset.id))) console.log(' ', it.asset.name, JSON.stringify(Object.fromEntries(['netYield','nextTaxRate','nextTaxRateDate','iofFreeDate'].map(k=>[k, k in it.projection ? it.projection[k] : 'AUSENTE']))))" $LCICDI $LCA $MD2 $MD3; done
```

Esperado (RN-16, RN-17; spec D-6, "nulo sai como `null`, com o campo presente"): LCI com
`"nextTaxRate":null,"nextTaxRateDate":null` e `iofFreeDate` igual a aporte + 30 enquanto `d` < 30; LCA
com os três `null`, inclusive o aporte de 2026-12-15 (`P_LCA2`) projetado na própria data sem
`referenceDate`. O mesmo texto nos três endpoints, nunca `AUSENTE`.

- BUG (média): campo ausente em qualquer um dos três endpoints.
- BUG (alta): LCA com `iofFreeDate`; LCI sem ela com `d` < 30; LCI ou LCA com `nextTaxRate`.

#### QA13-A05 — Vencimento curto, véspera e vencimento na fronteira com CDI projetado [P1, A, 15 min]

```bash
node "$QAW/js/linha.mjs" "$B" $P_CDI30 2026-11-16 $D $T 2026-12-01 2026-12-15 2026-12-16 2026-12-17 2027-01-15
node "$QAW/js/linha.mjs" "$B" $P_CDI181 $D $T 2026-11-28 2026-11-29 2026-11-30 2027-01-15
node "$QAW/js/linha.mjs" "$B" $P_CURTO 2026-11-16 $D 2026-11-30 2026-12-01 2026-12-02 2050-12-30
node "$QAW/js/linha.mjs" "$B" $P_VESP1 2026-11-30 2026-12-01 2026-12-02 2027-01-15
node "$QAW/js/linha.mjs" "$B" $P_VESP2 2026-12-01 2027-01-15
for t in $CDI30 $CDI181 $CURTO $VESP; do curl -s "$B/api/assets/$t/projection?referenceDate=2027-01-15" | novos | tr '\n' ' '; echo; done
```

Esperado (RN-02 corta antes da RN-16 e da RN-17; "igual ao vencimento é devolvida"): as âncoras da A01.
`P_CDI30`: `iofFreeDate` igual ao vencimento até 2026-12-15, com `isProjected` verdadeiro nas datas
depois de T; `P_CDI181`: `nextTaxRateDate` igual ao vencimento (num domingo) até 2026-11-28 e nula do
vencimento em diante; `P_CURTO`, `P_VESP1` e `P_VESP2`: nenhuma data em nenhuma linha (o título vence
antes de qualquer marco), com `iofRate` 96 em `P_VESP1` a partir de 2026-12-01 (paga IOF e nunca chega à
isenção, como o 6d-2 da spec). Projeção por título em 2027-01-15: `totalNetYield` presente e os itens
com os três campos `null`.

- BUG (alta): data posterior ao vencimento devolvida; data igual ao vencimento omitida; data presente
  numa posição cuja referência já passou do vencimento.

#### QA13-A06 — Posições vencidas [P1, A, 10 min]

```bash
node "$QAW/js/linha.mjs" "$B" $P_VENC1 2024-01-02 2024-06-30 2024-07-01 2024-12-31 2025-01-02 2025-06-19 $T 2050-12-30
node "$QAW/js/linha.mjs" "$B" $P_VENC2 2024-12-20 2024-12-31 2025-01-01 2025-01-02 2025-01-19 $T
for r in "" "?referenceDate=$T" "?referenceDate=2050-12-30"; do curl -s "$B/api/assets/$VENC/projection$r" | novos | tr '\n' ' '; echo; done
```

Esperado: antes do vencimento, as datas da regra (`P_VENC1` em 2024-01-02: `20.0`/2024-07-01 e
2024-02-01; em 2024-07-01, `17.5`/2024-12-28); do vencimento em diante, sempre `d` = 366 (ou 13), os três
campos nulos e `netYield` igual nas chamadas (os valores do vencimento, RN-02). `P_VENC2` em 2024-12-31:
`iofRate` 63 e `iofFreeDate` nula (2025-01-19 cairia depois do vencimento), o caso que o front leria
como "paga IOF e não tem data de fim".

- BUG (alta): data de faixa ou de IOF numa posição vencida; `netYield` variando depois do vencimento.
- RISCO (baixa): o README não diz que, num título vencido, os três campos vêm nulos (é consequência de
  "a data cairia depois do vencimento", mas o front pode ler `null` como "isento" ou "já em 15%"). Ver
  decisão 5.

#### QA13-A07 — Aporte posterior à data padrão nos três endpoints [P1, A, 15 min]

```bash
curl -s "$B/api/portfolio/summary" > "$QA/a/a07-resumo.json"
node -e "
const d=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8'),(k,v,c)=>typeof v==='number'?c.source:v).data;
console.log('raiz', d.referenceDate);
for (const it of d.positions.filter(x=>x.projection.applicationDate>d.referenceDate)) { const p=it.projection;
  console.log(it.asset.name, p.applicationDate, '->', p.referenceDate, 'd', p.elapsedDays, 'netYield', p.netYield, 'next', p.nextTaxRate, p.nextTaxRateDate, 'iofFree', p.iofFreeDate); }" "$QAW/a/a07-resumo.json"
node "$QAW/js/inv13.mjs" < "$QA/a/a07-resumo.json"
for t in $FUT $FUTV $LCA $VESP; do curl -s "$B/api/assets/$t/projection" | novos | tr '\n' ' '; echo; done
node "$QAW/js/coerencia13.mjs" "$B" --todos | tail -3            # sem data: os três endpoints, item a item
get "/api/positions/$P_FUT/projection"                          # documentado: 400, pede referenceDate
get "/api/positions/$P_FUT/projection?referenceDate=2026-12-15" | novos
for r in "" "?referenceDate=$T"; do curl -s "$B/api/portfolio/summary$r" | node -e "
const d=JSON.parse(require('fs').readFileSync(0,'utf8')).data; const ids=process.argv.slice(1);
console.log('${r:-sem data}', ids.map(i=>i.slice(0,8)+':'+(d.byAsset.find(b=>b.assetId===i)?.positionCount ?? 'fora')).join(' '))" $FUT $FUTV $LCA $VESP; done
```

Esperado (ESPECIFICACAO, "Endpoints"; RN-14; RN-18, "as regras de data são as do consolidado"): sem
data, `P_FUT`, `P_FUTV`, `P_LCA2` e os dois de `VESP` (se D < 2026-11-30) com `referenceDate` igual à
data do aporte, `d` = 0, `netYield` zero, e as datas das âncoras (`P_FUT`: 2027-06-14 e 2027-01-14;
`P_FUTV` e `P_LCA2`: nulas). Os mesmos itens na projeção por título sem data. A projeção da posição sem
data responde 400 (comportamento documentado); com a data do item, igual ao item. Em `byAsset`, sem
data: `FUT` e `FUTV` com 1, `LCA` com 2, `VESP` com 2; com `referenceDate=T`: `FUT`, `FUTV` e `VESP`
**fora** (todos os aportes depois da data) e `LCA` com 1.

- BUG (alta): aporte futuro sem as datas da regra; título com todos os aportes depois da data presente
  em `byAsset` (ou com `positionCount` 0); item projetado "para trás" (`referenceDate` = D).

#### QA13-A08 — `netYield` negativo e zero ao vivo [P1, A, 15 min]

Ache um mês de deflação no IPCA importado. O IBGE publicou −0,68 em 07/2022, −0,36 em 08/2022 e −0,29
em 09/2022; confira na listagem e use o mais negativo disponível (variável `MD`, dia 1 do mês):

```bash
curl -s "$B/api/indexes/Ipca?from=2020-01-01&to=2026-12-31" | node -e "
const v=JSON.parse(require('fs').readFileSync(0,'utf8'),(k,x,c)=>typeof x==='number'?c.source:x).data;
console.log(v.filter(x=>x.value.startsWith('-')).map(x=>x.referenceDate.slice(0,7)+' '+x.value).join(' | '))"
MD=2022-07-01                                   # ou o mês escolhido acima
P_DEFL=$(aporte $DEFL 1000 $MD)
FIM=$(date -d "$MD +28 days" +%F)               # d = 28: IOF de 6% pela tabela, mas sem rendimento
node "$QAW/js/linha.mjs" "$B" $P_DEFL $MD $(date -d "$MD +1 day" +%F) $FIM
curl -s "$B/api/positions/$P_DEFL/projection?referenceDate=$FIM"; echo
curl -s "$B/api/portfolio/summary?referenceDate=$FIM" | node "$QAW/js/inv13.mjs"
curl -s "$B/api/portfolio/summary?referenceDate=$FIM" | grep -oE '"totalNetYield":[^,}]*'
```

Esperado (RN-15, "pode ser negativo, num IPCA+ com deflação"; RN-11, "rendimento menor ou igual a zero
não gera IOF nem IR"; caso 2a da spec): em `$FIM`, `grossYield` e `netYield` negativos e iguais,
`iofRate` 6 e `iofAmount` 0, `taxRate` 22.5 e `taxAmount` 0, `nextTaxRate` `20.0` com aporte + 181 e
`iofFreeDate` aporte + 30 **mesmo sem rendimento**; no aporte, `netYield` zero. No resumo da mesma data,
o item do `DEFL` em `byAsset` com `totalNetYield` negativo, e a raiz fechando (`inv13` `OK`). Registre
o texto exato do zero (`0.00` ou `0`) e do negativo.

- BUG (alta): `netYield` diferente de `grossYield` com rendimento negativo; IOF ou IR cobrados sobre
  rendimento negativo.
- BUG (baixa): zero negativo (`-0.00`) em qualquer campo.
- NÃO EXECUTADO: sem mês de deflação na base importada ou sem calendário cobrindo o mês (registre a
  cobertura de `holiday` do passo 1).

#### QA13-A09 — Datas depois da cobertura do calendário [P2, A, 5 min]

```bash
node "$QAW/js/linha.mjs" "$B" $P_CAL 2050-12-20 2050-12-30 2050-12-31
get "/api/positions/$P_CAL/projection?referenceDate=2051-01-02"
get "/api/assets/$CAL/projection?referenceDate=2050-12-30" | novos
```

Esperado (RN-16, "não usa cotação"; RN-08 continua para os dias úteis): em 2050-12-30, 200 com
`nextTaxRateDate` 2051-06-19 e `iofFreeDate` 2051-01-19, datas que o calendário não cobre, mas que não
dependem dele; em 2051-01-02, o 400 do calendário que já existia (o mesmo texto da `main`, B01).

- BUG (alta): 400 ou 500 em 2050-12-30 por causa das datas novas.

#### QA13-A10 — Varredura das regras na carteira inteira [P1, A, 20 min]

```bash
for r in "" $D $T 2026-11-30 2026-12-16 2026-12-31 2027-05-16 2028-01-03 2030-01-02 2050-12-30; do
  echo "== ${r:-sem data}"; curl -s "$B/api/portfolio/summary${r:+?referenceDate=$r}" | node "$QAW/js/inv13.mjs"; done | tee "$QA/a/a10.txt"
grep -cE 'DIVERGE|item |título ' "$QA/a/a10.txt"
```

Esperado (RN-15 a RN-18): zero divergências em todas as datas, com todos os títulos e aportes do
Compose (os da F13 e os de QAs anteriores). A linha de motivos mostra quantos nulos são "isento",
"15%" e "vencimento": confira que os três aparecem em alguma data (2030 e 2050 têm muitos vencidos e
muitos em 15%).

- BUG (alta): qualquer item ou título divergente; soma do `byAsset` diferente da raiz; `byAssetType`
  diferente da soma do `byAsset` dos títulos do tipo.
- BUG (média): ordem de `byAsset` diferente da primeira aparição.

#### QA13-A11 — Coerência entre os três endpoints, com e sem data [P1, A, 25 min]

```bash
for r in "" $D $T 2026-11-30 2026-12-31 2028-01-03; do echo "== ${r:-sem data}"
  node "$QAW/js/coerencia13.mjs" "$B" $r $([ -z "$r" ] || [ "$r" = "$T" ] && echo --todos); done 2>&1 | tee "$QA/a/a11.txt"
```

Esperado (RN-18, "os totais de cada título iguais à soma dos itens e aos da projeção por título";
D-5; RN-14): em todas as datas, zero títulos e itens divergentes, nenhum título com aporte incluído
fora de `byAsset`, e cada item igual campo a campo (os quatro campos novos inclusive) nos três
endpoints. Sem data, se alguma linha mostrar "com outra referenceDate", a importação rodou entre as
duas chamadas (ressalva da D-5): repita só aquela data; se persistir sem mudança de L, é bug.

- BUG (alta): total de título diferente entre `byAsset` e a projeção por título com a mesma
  `referenceDate`; item diferente entre endpoints; título com aporte incluído fora de `byAsset`.
- RISCO (baixa): "totais de mesmo valor e texto diferente" (por exemplo `2000` contra `2000.00`) entre
  `byAsset` e a projeção por título.

#### QA13-A12 — `byAsset`: sem aportes, todos depois da data, empates do mesmo dia [P1, A, 15 min]

```bash
for r in "" 2026-11-19 2026-11-20 2026-11-23 2026-11-24 $T; do
  curl -s "$B/api/portfolio/summary${r:+?referenceDate=$r}" | node -e "
const d=JSON.parse(require('fs').readFileSync(0,'utf8')).data, [sem,dep,...md]=process.argv.slice(1);
const pos=x=>d.byAsset.findIndex(b=>b.assetId===x), cnt=x=>d.byAsset.find(b=>b.assetId===x)?.positionCount ?? 'fora';
console.log('${r:-sem data}', 'SEM', cnt(sem), '| DEP', cnt(dep), '| ordem DEP,MD1,MD2,MD3 no byAsset', [dep,...md].map(pos).join(','),
  '| na positions', [dep,...md].map(x=>d.positions.findIndex(p=>p.asset.id===x)).join(','))" $SEM $DEP $MD1 $MD2 $MD3; done
for k in 1 2 3 4 5; do curl -s "$B/api/portfolio/summary?referenceDate=2026-11-24" | node -e "
const d=JSON.parse(require('fs').readFileSync(0,'utf8')).data; console.log(d.byAsset.slice(0,8).map(b=>b.assetId.slice(0,8)).join(' '))"; done
get "/api/assets/$SEM/projection" | head -c 400; echo
```

Esperado (RN-18, "um item por título com pelo menos uma posição incluída, na ordem da primeira
aparição"): `SEM` sempre fora; `DEP` fora em 2026-11-19, com 1 em 2026-11-20 e 2026-11-23, com 2 de
2026-11-24 em diante e sem data; a ordem de `DEP`, `MD1`, `MD2` e `MD3` em `byAsset` é a ordem relativa
da primeira aparição deles em `positions` na **mesma** resposta (os quatro têm aporte em 2026-11-24, e
a ordem entre eles não é garantida, RN-14). A projeção por título de `SEM`: 200, zeros,
`totalNetYield` zero, `positions []`.

- BUG (alta): `SEM` em `byAsset`; `DEP` presente antes do primeiro aporte.
- BUG (média): ordem de `byAsset` diferente da primeira aparição na mesma resposta.
- RISCO (baixa): a ordem dos títulos empatados variando entre as cinco chamadas iguais (a lista do
  front "pisca"). A spec permite; só registrar.

#### QA13-A13 — `PUT` e `DELETE` de título [P2, A, 15 min]

Rode depois da A10 e da A11 (muda a taxa de `DEP`).

```bash
before=$(curl -s "$B/api/portfolio/summary?referenceDate=$T")
put "/api/assets/$DEP" '{"name":"QA-F13A TODOS DEPOIS","issuer":"QA-F13A Banco","rate":12,"issueDate":"2026-01-01","maturityDate":"2028-01-03"}'
put "/api/assets/$MD1" '{"name":"QA-F13A MESMO DIA 1 renomeado","issuer":"QA-F13A Banco","rate":10,"issueDate":"2026-01-01","maturityDate":"2028-01-03"}'
put "/api/assets/$DEP" '{"name":"QA-F13A TODOS DEPOIS","issuer":"QA-F13A Banco","rate":12,"issueDate":"2026-01-01","maturityDate":"2026-12-20"}'   # tem aporte: 400 (RN-06)
put "/api/assets/$SEM" '{"name":"QA-F13A SEM APORTE","issuer":"QA-F13A Banco","rate":10,"issueDate":"2026-01-01","maturityDate":"2026-12-20"}'   # sem aporte: aceito
P_SEM=$(aporte $SEM 1000 2026-11-24)
node "$QAW/js/coerencia13.mjs" "$B" $T
curl -s "$B/api/portfolio/summary?referenceDate=$T" | node "$QAW/js/inv13.mjs"
curl -s "$B/api/portfolio/summary?referenceDate=$T" | node -e "
const d=JSON.parse(require('fs').readFileSync(0,'utf8')).data, [dep,md1,sem]=process.argv.slice(1);
console.log('DEP', JSON.stringify(d.byAsset.find(b=>b.assetId===dep)));
console.log('MD1 nome em positions', d.positions.find(p=>p.asset.id===md1).asset.name, '| chaves do byAsset', Object.keys(d.byAsset[0]).join(','));
console.log('SEM', JSON.stringify(d.positions.filter(p=>p.asset.id===sem).map(p=>[p.projection.nextTaxRate,p.projection.nextTaxRateDate,p.projection.iofFreeDate])))" $DEP $MD1 $SEM
curl -s -X DELETE "$B/api/assets/$DEL" -w ' %{http_code}\n'; get "/api/assets/$DEL/projection" | tail -c 200
curl -s -X DELETE "$B/api/assets/$DEP" -w ' %{http_code}\n'                       # tem aporte: recusado (RN-06)
echo "$before" | node "$QAW/js/inv13.mjs" | tail -3
```

Esperado (RN-06; RN-18): depois do `PUT` de taxa, os totais de `DEP` em `byAsset` mudam junto com os
itens e com a projeção por título (`coerencia13` sem divergência), e as datas novas dos aportes de
`DEP` **não mudam** (só dependem do aporte); o `PUT` de nome aparece em `positions[].asset.name`, e
`byAsset` continua sem nome (só `assetId`, como o contrato diz); o `PUT` de vencimento de `SEM` vale para
o aporte novo: em T, `nextTaxRate` e `nextTaxRateDate` nulos (2027-05-24 cairia depois de 2026-12-20) e
`iofFreeDate` nula (2026-12-24 também). `DEL` removido e 404 na projeção; `DEP` não removível e ainda em
`byAsset`.

- BUG (alta): `byAsset` com a taxa antiga e os itens com a nova (ou o contrário); data nova presa ao
  vencimento antigo.

#### QA13-A14 — Serialização no texto [P1, A, 15 min]

```bash
for u in "/api/portfolio/summary" "/api/portfolio/summary?referenceDate=$T" "/api/portfolio/summary?referenceDate=2050-12-30" \
         "/api/assets/$CDI/projection" "/api/positions/$P_CDI1/projection?referenceDate=$T"; do
  curl -s "$B$u" > "$QA/a/a14.json"; echo "== $u"
  printf 'projeções %s | netYield %s | nextTaxRate %s | nextTaxRateDate %s | iofFreeDate %s\n' \
    $(grep -o '"positionId":' "$QA/a/a14.json" | wc -l) $(grep -o '"netYield":' "$QA/a/a14.json" | wc -l) \
    $(grep -o '"nextTaxRate":' "$QA/a/a14.json" | wc -l) $(grep -o '"nextTaxRateDate":' "$QA/a/a14.json" | wc -l) $(grep -o '"iofFreeDate":' "$QA/a/a14.json" | wc -l)
  grep -oE '"(nextTaxRate|taxRate)":[^,}]*' "$QA/a/a14.json" | sort | uniq -c | tr '\n' ' '; echo
  grep -oE '"(nextTaxRateDate|iofFreeDate)":[^,}]*' "$QA/a/a14.json" | grep -vE ':(null|"[0-9]{4}-[0-9]{2}-[0-9]{2}")$' | head -3
  grep -oE '"(netYield|totalNetYield)":-?0(\.0+)?[,}]' "$QA/a/a14.json" | sort | uniq -c | tr '\n' ' '; echo   # formas do zero
  grep -oE ':-0(\.0+)?[,}]|:-?[0-9.]+[eE][+-]?[0-9]+' "$QA/a/a14.json" | head -3                              # zero negativo, expoente
done
curl -s "$B/api/portfolio/summary?referenceDate=2000-01-01" | grep -oE '"(totalNetYield|byAsset|positions)":[^,}]*'
curl -sI "$B/api/portfolio/summary" | grep -i '^content-type'
```

Esperado (spec D-2, D-6; README, "Os campos nulos saem como `null`, com o campo presente"): o número de
`netYield`, `nextTaxRate`, `nextTaxRateDate` e `iofFreeDate` igual ao de `positionId` em cada resposta;
`nextTaxRate` só com os textos `null`, `20.0`, `17.5` e `15.0`; datas só `null` ou `"yyyy-MM-dd"`
(nenhuma linha na terceira saída); nenhum zero negativo nem notação exponencial (nenhuma linha na quinta
saída); carteira vazia em 2000-01-01 com `"byAsset":[]` e `totalNetYield` zero. Registre os textos do
`taxRate` (`20` ou `20.0`, `15` ou `15.0`): a comparação com a `main` é na B01.

- BUG (média): campo novo ausente em algum item.
- BUG (baixa): `nextTaxRate` sem a escala da tabela (`20` no lugar de `20.0`); zero negativo.

#### QA13-A15 — Swagger e o snapshot do front [P2, A, 15 min]

```bash
curl -s "$B/swagger/v1/swagger.json" > "$QA/a/swagger.json"
node "$QAW/js/swdiff.mjs" "$(cygpath -m "$WEB")/openapi/renda-fixa-api.json" "$QAW/a/swagger.json" | tee "$QA/a/a15.txt"
node -e "
const s=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8')).components.schemas;
console.log('AssetSummaryResponse', Object.keys(s.AssetSummaryResponse.properties).join(','));
console.log('byAsset', JSON.stringify(s.PortfolioSummaryResponse.properties.byAsset));
for (const n of ['PositionProjectionResponse','PortfolioSummaryResponse','AssetSummaryResponse','AssetProjectionResponse'])
  console.log(n, 'nullable:', Object.entries(s[n].properties).filter(([,v])=>v.nullable).map(([k])=>k).join(',') || '—')" "$QAW/a/swagger.json"
curl -s "$B/api/portfolio/summary?referenceDate=$T" | node -e "
const d=JSON.parse(require('fs').readFileSync(0,'utf8')).data, s=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8')).components.schemas;
const eq=(n,o)=>console.log(n, JSON.stringify(Object.keys(o))===JSON.stringify(Object.keys(s[n].properties)) ? 'chaves = schema' : 'DIVERGE '+Object.keys(o).join(','));
eq('PortfolioSummaryResponse', d); eq('AssetSummaryResponse', d.byAsset[0]); eq('PositionProjectionResponse', d.positions[0].projection)" "$QAW/a/swagger.json"
curl -s -o /dev/null -w 'swagger ui %{http_code}\n' "$B/swagger/index.html"
```

Esperado: em relação ao snapshot do front (gerado em 18/11/2026, antes da F12), nenhuma propriedade
antiga com outro tipo, formato ou `nullable` (as mudanças de rota da F12, como respostas declaradas a
mais, aparecem como "rota mudou" e são só registradas); a lista do que o snapshot não tem (os campos e
schemas da F12 e da F13) vai para o relatório, para o front saber o que regenerar. `AssetSummaryResponse`
sem `isProjected` nem `isEstimated` (RN-18); `byAsset` como `array` de `AssetSummaryResponse`; as chaves
reais na ordem do schema.

- BUG (média): propriedade do snapshot com tipo ou formato diferente (mudança não aditiva desde a v1);
  chaves reais diferentes do schema.
- Observação (fora da fase, sem severidade): o snapshot do front está desatualizado desde antes da F12.
  O roteiro não toca o `renda-fixa-web`.

#### QA13-A16 — `.http` e o README [P2, A, 15 min]

Reproduza pela porta 8080, com nomes `QA-F13A HTTP …` no lugar de `EXEMPLO-…`, as três requisições do
`FixedIncome.Api.http` que ganharam comentário da F13 ("Projeção do aporte", "Resumo consolidado da
carteira", "Projeção por título com referenceDate"), e confira cada afirmação da seção "Ganho líquido,
próxima faixa de IR e fim do IOF" do README contra o que as cenas anteriores mostraram:

| Afirmação do README | Onde foi vista |
|---|---|
| exemplo `netYield 2.67`, `nextTaxRate 20.0`, `2025-09-29`, `2025-05-01` | A01 (`PRE` em 2025-04-22) |
| exemplo de `byAsset` (2000 / 2009.86 / … / 5.34) | A01 (caso 16) |
| `netYield` pode ser negativo e é zero no dia do aporte | A08 |
| `nextTaxRate` nulo para LCI e LCA, em 15% e depois do vencimento; igual ao vencimento é devolvida | A04, A05, A10 |
| `iofFreeDate` nula com `d` ≥ 30, para LCA e depois do vencimento | A03, A04, A05 |
| datas em dias corridos, sem ajuste para dia útil | A02 (domingos), A01 (feriado) |
| `byAsset` sem marca de estimado nem de projetado; carteira vazia devolve lista vazia | A14, A15 |

Esperado: cada requisição do `.http` responde 200 com os campos que o comentário anuncia; cada
afirmação confere. O exemplo do README mostra `"investedAmount": 1000` e `"totalInvestedAmount": 2000`:
registre se a resposta real traz `2000.00` (formato já existente antes da F13, só observação).

- BUG (baixa): afirmação do README ou comentário do `.http` que não confere.

#### QA13-A17 — Respostas de erro com os campos novos [P2, A, 5 min]

```bash
get "/api/portfolio/summary?referenceDate=abc"
get "/api/portfolio/summary?referenceDate=2051-01-02"
get "/api/positions/$P_FUT/projection?referenceDate=$T"            # "A data de referência não pode ser anterior à data de aporte."
get "/api/assets/00000000-0000-0000-0000-000000000000/projection"
get "/api/assets/$DEL/projection"
```

Esperado: os status e as mensagens de sempre, `data` nulo, nenhum `byAsset`, `totalNetYield` ou
`netYield` parcial fora de `data`. O texto exato é comparado com a `main` na B01.

- BUG (média): `data` não nulo ou campo novo na resposta de erro.

#### QA13-A18 — Deriva de L durante a sessão [P1, A, 5 min, no fim]

Repita o passo 5 e a foto do passo 2. Se L mudou, diga em quais casos valia cada L (pela hora) e repita
a A11 só sem data. A foto de `index_rate` deve mostrar só linhas novas depois do L inicial.

- BUG (alta): cotação anterior ao L inicial alterada ou apagada (nesta parte, só a importação da
  `qaf13a-fase` escreve em `index_rate`). Confira também que o Compose não recebeu nenhuma escrita:
  `curl -s localhost:8080/api/assets` e `/api/positions` com as mesmas contagens do passo 2.

### Parte B — Pilha isolada, fase e `main` sobre o mesmo banco (`QA-F13B`)

`BF=http://localhost:28113` (fase), `BM=http://localhost:28114` (`main`). Importação desligada nas
duas: L e D ficam fixos na cópia, e as duas APIs leem o mesmo relógio. Quando `B` aparecer nas funções
da preparação comum (`titulo`, `aporte`), use `B=$BF`: tudo o que a fase grava, a `main` lê.

#### QA13-B00 — Carteira vazia e carga da carteira [P1, B, 10 min]

```bash
for b in $BM $BF; do echo "== $b"
  curl -s "$b/api/portfolio/summary"; echo
  curl -s "$b/api/portfolio/summary?referenceDate=2026-11-30"; echo; done
printf '/api/portfolio/summary\n/api/portfolio/summary?referenceDate=2026-11-30\n' | node "$QAW/js/compara.mjs" $BM $BF
docker exec -i qaf13-db psql -U qaf13 -d qaf13 -q -v ON_ERROR_STOP=1 < "$QA/b/carteira.sql"
for b in $BM $BF; do curl -s "$b/api/positions" | node -e "console.log('$b aportes', JSON.parse(require('fs').readFileSync(0,'utf8')).data.length)"; done
```

Esperado (RN-18, "Carteira vazia: lista vazia"; D-1): sem posições, a fase com `"totalNetYield":0` (ou
`0.00`, registre), `"byAsset":[]`, `"positions":[]` e `referenceDate` presente; o `compara` sem
divergência, com as chaves novas `totalNetYield,byAsset` na raiz. Depois da carga, as duas APIs com o
mesmo número de aportes que a foto do passo 2.

- BUG (alta): 500 ou 400 com a carteira vazia.

#### QA13-B01 — Só aditivo: `main` contra fase, campo a campo [P1, B, 30 min]

```bash
B=$BF; D_B=$(curl -s "$BF/api/portfolio/summary" | node -e "process.stdout.write(JSON.parse(require('fs').readFileSync(0,'utf8')).data.referenceDate)")
node "$QAW/js/urls.mjs" $BF $D_B $T 2026-12-31 2028-01-03 2050-12-30 > "$QA/b/urls.txt"; cat "$QA/b/fixos.txt" >> "$QA/b/urls.txt"; wc -l "$QA/b/urls.txt"
node "$QAW/js/compara.mjs" $BM $BF < "$QA/b/urls.txt" | tee "$QA/b/b01.txt"
curl -s "$BF/api/portfolio/summary?referenceDate=2028-01-03" | node "$QAW/js/inv13.mjs" | tail -4
```

Esperado (ESPECIFICACAO, "Também só aditivo, sem mudar nenhum valor existente"; spec, "Mudanças de
comportamento: nenhuma"): **zero rotas divergentes**, inclusive nas respostas de erro (mesmo status e
mesmo texto de `message`). A lista de chaves novas é exatamente:

- `/api/positions/{id}/projection $.data` → `netYield,nextTaxRate,nextTaxRateDate,iofFreeDate`;
- `/api/portfolio/summary $.data` → `totalNetYield,byAsset` e `$.data.positions[].projection` → os
  quatro da projeção;
- `/api/assets/{id}/projection $.data` → `totalNetYield` e `$.data.positions[]` → os quatro;
- nada em `/api/assets`, `/api/assets/{id}`, `/api/positions`, `/api/indexes/...`.

Atenção ao `taxRate`: a D-2 reescreveu a tabela de IR com os literais `22.5m`, `20.0m`, `17.5m` e
`15.0m` e diz que `RateFor` tem "comportamento idêntico ao de hoje". Se a `main` escreve `"taxRate":20`
e a fase `"taxRate":20.0` (mesmo valor, outro texto), o `compara` mostra
`taxRate: main 20 x fase 20.0`: registre quantas linhas e classifique pela decisão 5.

- BUG (alta): qualquer campo antigo com outro valor; chave antiga ausente; status diferente.
- BUG (média): mensagem de erro com outro texto; chave nova antes de uma antiga; chave nova fora da
  lista acima.
- BUG (baixa) ou RISCO (decisão 5): campo antigo com o mesmo valor e outro texto (escala).

#### QA13-B02 — Swagger: `main` contra fase [P1, B, 5 min]

```bash
curl -s $BM/swagger/v1/swagger.json > "$QA/b/sw-main.json"; curl -s $BF/swagger/v1/swagger.json > "$QA/b/sw-fase.json"
node "$QAW/js/swdiff.mjs" "$QAW/b/sw-main.json" "$QAW/b/sw-fase.json"
```

Esperado (critério 5 do ROADMAP): "nenhuma rota, schema ou propriedade antiga alterada"; nenhuma rota
nova; novos só `netYield:number/double`, `nextTaxRate:number/double?`, `nextTaxRateDate:string/date?`,
`iofFreeDate:string/date?` em `PositionProjectionResponse`; `totalNetYield:number/double` e
`byAsset:array?<#/components/schemas/AssetSummaryResponse>` em `PortfolioSummaryResponse`;
`totalNetYield:number/double` em `AssetProjectionResponse`; e o schema novo `AssetSummaryResponse` com
`assetId:string/uuid`, `positionCount:integer/int32` e sete `number/double`.

- BUG (média): propriedade antiga alterada; rota alterada; novo fora da lista.

#### QA13-B03 — Muitos títulos e um título com muitos aportes [P2, B, 25 min]

```bash
cat > "$QA/js/carga.mjs" <<'EOF'
// uso: node carga.mjs <base>   (150 títulos QA-F13B com 1 a 3 aportes e um título com ~250 aportes)
const base = process.argv[2];
const post = async (u, b) => { const r = await fetch(base + u, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(b) }); const j = await r.json(); return j.data?.id ?? `ERRO ${r.status} ${j.message}`; };
const util = d => { const w = d.getUTCDay(); if (w === 6) d.setUTCDate(d.getUTCDate() + 2); if (w === 0) d.setUTCDate(d.getUTCDate() + 1); return d.toISOString().slice(0, 10); };
const dia = k => util(new Date(Date.UTC(2024, 0, 2) + (k * 37 % 1040) * 864e5));
const tipos = ['Cdb', 'Lci', 'Lca', 'TreasuryBond'], idx = ['PreFixed', 'Cdi'];
let erros = 0;
for (let k = 1; k <= 150; k++) {
  const ix = idx[k % 2], id = await post('/api/assets', { name: `QA-F13B T${String(k).padStart(3, '0')}`, issuer: 'QA-F13B Banco', assetType: tipos[k % 4], indexType: ix,
    rate: ix === 'Cdi' ? 90 + k % 30 : 8 + k % 7, issueDate: '2024-01-01', maturityDate: k % 10 === 0 ? '2026-12-10' : '2029-01-02' });
  console.log('titulo', k, id); if (id.startsWith('ERRO')) { erros++; continue; }
  for (let j = 0; j < 1 + k % 3; j++) { const p = await post('/api/positions', { assetId: id, investedAmount: 100.37 + k, applicationDate: k % 7 === 0 ? '2026-11-24' : dia(k * 3 + j) }); console.log('  aporte', p); if (p.startsWith('ERRO')) erros++; }
}
const many = await post('/api/assets', { name: 'QA-F13B MUITOS CDI', issuer: 'QA-F13B Banco', assetType: 'Cdb', indexType: 'Cdi', rate: 100, issueDate: '2024-01-01', maturityDate: '2030-01-02' });
console.log('muitos', many);
for (let d = new Date(Date.UTC(2024, 0, 2)); d.toISOString().slice(0, 10) <= '2026-11-20'; d.setUTCDate(d.getUTCDate() + 3)) { const w = d.getUTCDay(); if (w === 0 || w === 6) continue; const p = await post('/api/positions', { assetId: many, investedAmount: 100.37, applicationDate: d.toISOString().slice(0, 10) }); if (p.startsWith('ERRO')) erros++; }
console.log('erros', erros);
EOF
node "$QAW/js/carga.mjs" $BF > "$QA/b/carga.txt"; tail -2 "$QA/b/carga.txt"
MANY=$(grep '^muitos' "$QA/b/carga.txt" | cut -d' ' -f2)
for r in "" 2026-11-30 2030-01-02; do echo "== ${r:-sem data}"
  curl -s "$BF/api/portfolio/summary${r:+?referenceDate=$r}" | node "$QAW/js/inv13.mjs" | tail -6
  node "$QAW/js/coerencia13.mjs" $BF $r | tail -1; done
for k in 1 2 3 4 5; do curl -s "$BF/api/portfolio/summary" | node -e "
const d=JSON.parse(require('fs').readFileSync(0,'utf8')).data; console.log(require('crypto').createHash('md5').update(d.byAsset.map(b=>b.assetId).join()).digest('hex'), d.byAsset.length)"; done
curl -s "$BF/api/assets/$MANY/projection" | grep -oE '"(positionCount|totalNetYield)":[^,]*'
```

(Os erros de criação, por exemplo `TreasuryBond` com `Cdi` ou aporte em feriado, não são da F13:
registre quantos e siga.)

Esperado: `inv13` e `coerencia13` sem divergência com cerca de 150 títulos a mais em `byAsset` (os de
vencimento 2026-12-10 entram como vencidos em 2030); o hash da ordem de `byAsset` igual nas cinco
chamadas; o título de ~250 aportes com `positionCount` igual ao número de aportes aceitos e o
`totalNetYield` igual à soma dos `netYield` (conferido pelo `inv13` no resumo).

- BUG (alta): soma divergente com muitos itens de valor quebrado (100,37).
- RISCO (baixa): ordem de `byAsset` mudando entre chamadas idênticas sem nenhuma escrita entre elas.

#### QA13-B04 — Desempenho do consolidado com `byAsset` [P2, B, 20 min]

```bash
for r in "" "?referenceDate=2026-11-30" "?referenceDate=2030-01-02" "?referenceDate=2050-12-30"; do
  printf 'resumo %-28s main ' "${r:-sem data}"; tempo $BM "/api/portfolio/summary$r"
  printf 'resumo %-28s fase ' "${r:-sem data}"; tempo $BF "/api/portfolio/summary$r"; done | tee "$QA/b/b04.txt"
for r in "" "?referenceDate=2050-12-30"; do
  printf 'título MUITOS %-20s main ' "${r:-sem data}"; tempo $BM "/api/assets/$MANY/projection$r"
  printf 'título MUITOS %-20s fase ' "${r:-sem data}"; tempo $BF "/api/assets/$MANY/projection$r"; done | tee -a "$QA/b/b04.txt"
seq 200 | xargs -P 20 -I{} sh -c 'curl -s -o /dev/null -w "%{http_code}\n" "'"$BF"'/api/portfolio/summary"' | sort | uniq -c
docker logs qaf13-fase 2>&1 | grep -ciE 'exception|fail:'
```

Esperado (bom senso: a F13 só agrupa e soma o que já estava calculado; risco 11 da F12, "resumo de 262
posições em 2050-12-30: 2,7 a 3,0 s"): a mediana da fase até 10% (mais 30 ms) acima da da `main` em cada
linha; o tamanho maior só pelos campos novos (registre o aumento em bytes e por posição); 200 nas 200
chamadas paralelas e nenhuma exceção no log. Registre as medianas ao lado do risco 11 da F12, para dizer
se ele piorou.

- BUG (média): fase mais de duas vezes mais lenta que a `main` na mesma linha.
- RISCO (baixa): fase entre 10% e 100% mais lenta; o risco 11 da F12 continua aberto (só registrar os
  números novos).
- BUG (alta): qualquer 500 sob concorrência.

#### QA13-B05 — IPCA+ estimado e projetado juntos [P2, B, 15 min]

```bash
restaura; tira Ipca ${M}-01                     # o último mês publicado vira "encerrado e não divulgado" (RN-10)
curl -s "$BF/api/indexes/Ipca?from=2026-01-01&to=2026-12-31" | tail -c 200; echo
BIPCA=$(titulo "QA-F13B IPCA6" Cdb Ipca 6 2026-01-01 2028-01-03); BP1=$(aporte $BIPCA 1000 2026-09-01); BP2=$(aporte $BIPCA 1000 2026-11-03)
BTD=$(titulo "QA-F13B TESOURO IPCA6" TreasuryBond Ipca 6 2026-01-01 2035-05-15); BP3=$(aporte $BTD 1000 2026-11-16)
for p in $BP1 $BP2 $BP3; do node "$QAW/js/linha.mjs" $BF $p $T 2026-12-15 2027-03-31; done
printf '%s\n' "/api/portfolio/summary" "/api/portfolio/summary?referenceDate=2026-12-15" "/api/assets/$BIPCA/projection" \
  "/api/assets/$BTD/projection?referenceDate=2027-03-31" "/api/positions/$BP1/projection?referenceDate=2026-12-15" | node "$QAW/js/compara.mjs" $BM $BF
restaura
```

Esperado (RN-10, RN-12; RN-16, "não muda `isProjected` nem `isEstimated`"): itens com `isEstimated` e
`isProjected` verdadeiros juntos, `OK` no `linha.mjs` (as datas do aporte, como sem marca), e o
`compara` sem divergência nos campos antigos.

- BUG (alta): marca ou valor antigo diferente da `main`; data nova mudando por causa da estimativa.

#### QA13-B06 — Importação parada e CDI projetado no limite [P2, B, 15 min]

```bash
B=$BF; BCDI=$(titulo "QA-F13B CDI100" Cdb Cdi 100 2026-01-01 2030-01-02); BP4=$(aporte $BCDI 1000 2026-11-16)
restaura; cdi_ate $(date -d "$P -5 days" +%F)     # sobra pelo menos um dia útil sem cotação antes de P: data depois de T é 400 (RN-12)
U="/api/portfolio/summary?referenceDate=2026-12-31
/api/assets/$BCDI/projection?referenceDate=2026-12-31
/api/positions/$BP4/projection?referenceDate=2026-12-31
/api/portfolio/summary"
echo "$U" | node "$QAW/js/compara.mjs" $BM $BF; curl -s "$BF/api/portfolio/summary?referenceDate=2026-12-31"; echo
restaura                                           # cópia com L = P − 1 dia útil ou L = P: a data depois de T é projetada
[ "$(sqlb "select max(reference_date) from index_rate where index_type='Cdi'")" \< "$(date -d "$P -4 days" +%F)" ] && sintetico $P 0.050788   # só se a cópia estiver mais velha
echo "$U" | node "$QAW/js/compara.mjs" $BM $BF; curl -s "$BF/api/portfolio/summary?referenceDate=2026-12-31" | node "$QAW/js/inv13.mjs" | tail -4
restaura
```

Esperado: com a importação parada, as quatro rotas com o mesmo status e a mesma mensagem nas duas APIs
(400 com "Cotação de CDI ausente para …" nas datas futuras, D-11 da F12) e `data` nulo, sem `byAsset`
parcial; com L = P, 200 projetado, `compara` sem divergência e `inv13` `OK`.

- BUG (alta): a fase respondendo 200 onde a `main` responde 400, ou o contrário.
- BUG (média): `data` não nulo no 400.

#### QA13-B07 — Data padrão resolvida duas vezes (ressalva da D-5) [P3, B, 10 min]

```bash
restaura; S1=$(curl -s "$BF/api/portfolio/summary"); D1=$(echo "$S1" | node -e "process.stdout.write(JSON.parse(require('fs').readFileSync(0,'utf8')).data.referenceDate)")
sintetico $D1 0.050788                             # simula a importação rodando entre as duas chamadas do front
curl -s "$BF/api/assets/$BCDI/projection" | grep -oE '"(referenceDate|totalNetYield|totalGrossAmount)":[^,]*' | head -3
echo "$S1" | node -e "const d=JSON.parse(require('fs').readFileSync(0,'utf8')).data; console.log(d.referenceDate, JSON.stringify(d.byAsset.find(b=>b.assetId==='$BCDI')))"
restaura
```

Esperado (spec D-5: "sem data informada, as duas requisições resolvem a data padrão cada uma"): o resumo
com D1 e a projeção por título com o dia útil seguinte, totais diferentes, **e as duas respostas com a
`referenceDate` que usaram**, para o front perceber a diferença. É a ressalva aceita pela spec: só
registrar.

- BUG (média): alguma das duas sem `referenceDate`, ou com a `referenceDate` diferente da data que de
  fato usou.

## O que pode rodar em paralelo

1. **Sequencial no início:** preparação comum, passos 0 a 5 (o `--build` do Compose gera a imagem que
   as pilhas isoladas congelam; o passo 2b sobe a pilha da Parte A), e a preparação da Parte B até as
   duas APIs de pé.
2. **Parte A e Parte B em paralelo**, em terminais separados (`B` diferente em cada um). Não
   compartilham banco (`qaf13a-db` e `qaf13-db`). No terminal da Parte B, refaça os passos 0 e 4 (variáveis e funções), redefina
   `T`, `P` e `M` com os valores lidos no passo 5, e as funções e scripts da preparação da Parte B. A cópia da carteira (`carteira.sql`) é tirada na preparação, antes de a Parte A
   criar os títulos `QA-F13A`: a Parte B não os tem, e não precisa.
3. **Dentro da Parte A:** a A01 primeiro (cria a carteira); depois qualquer ordem, exceto a A13 (muda
   taxa, nome e vencimento), que vem depois da A10, A11 e A12, e a A18, no fim. A A08 cria o aporte do
   `DEFL`.
4. **Dentro da Parte B:** B00 primeiro (carteira vazia, depois a carga); B01 e B02 antes da B03, para a
   comparação rodar sobre a carteira copiada sem o volume sintético; B04 depois da B03; B05, B06 e B07,
   um de cada vez, sempre entre dois `restaura`.

## Fora deste roteiro

- Testes automatizados, cobertura, Stryker e build: feitos pela validação.
- Os casos de referência num banco limpo (1a, LCA, 16, 4, 5, 6 e 10): a validação os fez ao vivo. Aqui
  só o caso 16 volta, como âncora numa carteira suja (A01).
- Recalcular de forma independente os valores em dinheiro (bruto, IR, IOF): não mudaram na F13 e são
  comparados com a `main` (B01). A conferência externa do CDI foi feita na F12.
- Ajuste de `nextTaxRateDate` e `iofFreeDate` para dia útil, vencimento em dia não útil pago no dia
  seguinte, liquidação do Tesouro Direto e carência de LCI e LCA: fora de escopo da fase (spec).
- As pendências herdadas da F12 (formatos de data, parâmetro repetido, `indexType` com espaços,
  mensagens de IPCA, `PreFixed` no enum do Swagger, escala da taxa no `PUT`): não são retestadas; o
  desempenho (risco 11) só é medido de novo na B04.
- Janelas de horário (fuso de Brasília, hoje em fim de semana): as datas novas não dependem de hoje.
- Qualquer alteração no `renda-fixa-web`; o snapshot do front é só lido.

## Limpeza ao fim

```bash
# Partes A e B: tudo some com os contêineres e as imagens próprias
docker rm -f qaf13a-fase qaf13a-db qaf13-fase qaf13-main qaf13-db; docker network rm qaf13-net
docker rmi qaf13-main:latest; docker rmi qaf13-fase:latest        # só remove as etiquetas; renda-fixa-api-api:latest fica
cd "$REPO" && docker compose stop        # decisão 2; nunca "down -v"
docker volume ls -q | sort | diff - "$QA/antes-volumes.txt" && echo "volumes iguais"
git status --short | diff - "$QA/git-status-antes.txt"      # só docs/QA-F13.md (e as partes) a mais
```

O relatório lista, na seção "Dados criados durante o QA", os ids dos títulos e aportes `QA-F13A` e
`QA-F13B`, e diz que todos sumiram com `qaf13a-db` e `qaf13-db`, e que o banco do Compose não recebeu
nenhuma escrita.

## Decisões antes de executar

**Respostas do Matheus (27/11/2026):** 1 sim; 2 `up -d --build` e `docker compose stop` no fim; 3 sim;
4 **Parte A na pilha isolada** (`qaf13a`, passo 2b), sem gravar nada no Compose; 5 escala do
`taxRate` sem mudança (a tabela antiga já usava `20.0m`, `17.5m` e `15.0m`: diferença de escala
contra a `main` é BUG); aporte no dia do vencimento é aceito (RN-05 só recusa data posterior; recusa
é BUG); os demais itens viram `RISCO (baixa)`, e o snapshot do front, observação sem severidade;
6 execução em 27/11/2026, com as datas do roteiro.

1. **Cópia completa da carteira do Compose para a pilha isolada.** A preparação da Parte B faz
   `pg_dump` do esquema, do mercado **e dos títulos e aportes** do `fixedincome-db` (os do Matheus e os
   de QAs anteriores) para o `qaf13-db`, apagado no fim com o contêiner. É o que dá à B01 uma comparação
   com a `main` sobre centenas de posições reais. Autoriza? Sem isso, a Parte B copia só o mercado (como
   na F12) e a B01 compara só sobre os títulos `QA-F13B` da B03, B05 e B06.
2. **`docker compose up -d --build`** recria `fixedincome-api` e `fixedincome-db` com o código da fase (a
   validação fez o mesmo; o volume é preservado). No fim: `docker compose stop`, deixar no ar ou
   `docker compose down` sem `-v`?
3. **Pilha isolada com duas APIs** (`qaf13-fase` em 28113 e `qaf13-main` em 28114, importação
   desligada) e a **imagem da `main` montada de `git archive origin/main`** no scratchpad, sem worktree
   nem checkout. `index_rate` só é manipulada em `qaf13-db`. Autoriza? Sem ela, B00 a B07 ficam `NÃO
   EXECUTADO` e o "só aditivo" fica só com a prova da validação (testes e Swagger).
4. **Volume de dados no Compose.** A Parte A cria cerca de 25 títulos e 30 aportes `QA-F13A`, que não
   saem pela API (RN-06). Os 150 títulos e ~550 aportes da B03 ficam só na pilha isolada, para não
   entrar no resumo do Matheus. De acordo? A alternativa é rodar a Parte A inteira em `qaf13-fase`
   (nada gravado no Compose), perdendo só a importação ao vivo (A18) e a carteira com os `QA-F13A` junto
   da real.
5. **Classificação de casos sem regra explícita**, para o QA não decidir sozinho. Contam como `BUG
   (baixa)` ou `RISCO (baixa)`?
   - `taxRate` existente com o mesmo valor e outra escala no texto em relação à `main` (`20` → `20.0`,
     `15` → `15.0`), por causa dos literais da D-2;
   - `netYield` zero como `0.00` num item e `totalNetYield` zero como `0` na carteira vazia;
   - `null` com quatro significados (isento, já em 15%, faixa depois do vencimento, título vencido), que
     o front só separa olhando o tipo e o vencimento do título; o README não cita o título vencido;
   - a ordem de `byAsset` entre títulos empatados (aportes do mesmo dia) variando entre chamadas;
   - totais de mesmo valor e texto diferente entre `byAsset` e a projeção por título;
   - o aporte no próprio dia do vencimento recusado (seria regra da F7, fora da fase);
   - o snapshot do front desatualizado (proposta: observação, sem severidade, fora da fase).
6. **Data da execução.** Nada depende de janela de horário. Execução em dezembro de 2026 ou depois muda
   a A03 (IPCA de novembro deixa de ser o mês de hoje) e as datas "futuras" da A01 e da A07 (a regra de
   troca está em "Ambiente"). Há preferência de dia?
