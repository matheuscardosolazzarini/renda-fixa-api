# Roteiro de QA — F12: Contrato v2 para o front

Roteiro exploratório para o agente `qa-exploratorio`. O relatório vai em `docs/QA-F12.md`.
**Status: aprovado pelo Matheus em 27/11/2026, com as recomendações do coordenador** para as
"Decisões antes de executar": (1) leitura do banco do Compose autorizada; (2) `docker compose up -d
--build` autorizado, e no fim `docker compose stop` (nunca `-v`); (3) pilha isolada da Parte B
autorizada, e o `index_rate` do Compose não é alterado; (4) rede para a Calculadora do Cidadão
autorizada (três consultas); (5) execução agora, B12 e B13 ficam `NÃO EXECUTADO` se a janela não
coincidir; (6) os oito casos sem regra na spec são `RISCO` de severidade baixa; (7) volume de dados
`QA-F12A` no Compose aceito. Os relatórios das partes vão em `docs/QA-F12-parteA.md` e
`docs/QA-F12-parteB.md`, consolidados em `docs/QA-F12.md` pelo coordenador.

A F12 entrega ao front três coisas novas: `isProjected` na projeção, no resumo e nos itens do
resumo (RN-12); `GET /api/indexes/{indexType}/accumulated` (RN-13); e
`GET /api/assets/{id}/projection` (RN-14). Muda o valor de duas respostas que já existiam (IPCA+ no
mês de hoje e data informada depois da última cotação de CDI). O "usuário" desta fase é o front
`renda-fixa-web`: chama as rotas com o que o usuário digitou, confia em `isProjected` para pôr o
aviso de "valor projetado" na tela e soma nada, porque os totais já vêm prontos.

A pergunta que guia cada cenário: **em que caso o front mostraria um número errado, um rótulo
errado (projetado ou não, estimado ou não), ou receberia 500 ou um 400 que não explica nada?**

## O que a validação já provou (não se repete aqui)

A `docs/VALIDACAO-F12.md` (27/11/2026) provou, e este roteiro não refaz como caso próprio:

- build sem warning, 861 testes, cobertura, Stryker com 96,96%;
- os casos de referência da spec por teste automatizado (1 a 3-ter, 6, 6-bis, 7, 14 a 21, 29);
- ao vivo, com hoje = 27/11/2026 e última cotação de CDI em 25/11: o acumulado de CDI de 01/09 a
  01/10/2026 (`1.081673`) e mais dois períodos de novembro contra a Calculadora do Cidadão; o corte
  com `to` em 31/12/2026, 31/01/2027 e 02/01/2051; `from > to`; `PreFixed`; `indexType` numérico
  `1`; `from` ausente; o IPCA acumulado em cinco períodos e a regra de 7 casas em 17 períodos;
- ao vivo: CDB 100% do CDI com aporte em 16/11 e `referenceDate=2026-11-30` (1.004,58,
  `isProjected true`); o mesmo sem data (1.003,56, `false`); IPCA+ com data futura e sem data;
  lacuna em 18/11 (projeção com e sem data e acumulado); importação parada (24 e 25/11 apagados);
  data de 2051 na projeção por título e na de posição;
- ao vivo: projeção por título com o caso 29, título sem aportes com e sem data, id inexistente e
  id `abc`; resumo sem data e com 30/11 (soma dos itens igual ao total); a forma do `swagger.json`.

## O que a validação não cobriu (foco deste roteiro)

- entradas fora do caminho feliz nas rotas novas: formatos de data, caixa e espaços no
  `indexType`, valores de enum combinados (`PreFixed,Cdi`), datas extremas (`0001-01-01`,
  `9999-12-31`), `from == to`, fins de semana e feriados nas pontas;
- coerência entre endpoints, campo a campo e no texto do JSON: acumulado contra a listagem de
  cotações e contra um CDB 100% do CDI; item da projeção por título contra a projeção da posição e
  contra o item do resumo; soma das projeções de todos os títulos contra o resumo;
- a matriz de `isProjected` e `isEstimated`: data padrão, hoje, véspera, datas curtas e longas,
  vencimento na fronteira (inclusive o IPCA+ vencendo em 02/11, feriado, e em 03/11), IPCA+ no mês
  de hoje, carteira mista;
- fronteiras de IR (RN-03) com dias projetados, e projeções de anos;
- títulos vencidos, aportes fora de ordem e no mesmo dia, título removido, título com taxa
  alterada, muitos aportes;
- as duas mudanças de comportamento com a última cotação controlada (T−1, T−2, T−3 dias úteis),
  feriado logo depois da última cotação, CDI sem nenhuma cotação, IPCA com janela incompleta (11
  meses, dois meses ausentes, nenhum), IPCA estimado e projetado juntos ao vivo (caso 6);
- as duas lacunas de teste que o Stryker apontou (O-1 da validação): mensagem de "período sem
  cotação" do acumulado com `from` depois do fim efetivo, e a mensagem da D-4 com dois meses
  ausentes na janela;
- fuso de Brasília na fronteira do "dia útil anterior a hoje" (janela de 21h a 24h);
- regressão do contrato antigo (chaves, mensagens, status), Swagger, `.http` e exemplos do README.

## Regras herdadas do agente, adaptadas à fase

- **Caixa-preta primeiro.** Use a API como o README, o Swagger e o `FixedIncome.Api.http` ensinam.
  O código de `src/` só é aberto para explicar um achado já reproduzido.
- **Tudo reproduzível.** Cada achado traz método, URL, corpo, a resposta obtida (status e trecho do
  JSON), a esperada e a fonte da expectativa (regra RN-xx, D-x da spec, README, bom senso de API).
- **Nada é corrigido.** O único arquivo que o QA cria no repositório é `docs/QA-F12.md`.
- **Não destrua dados.** Nada de `docker compose down -v`, nada de apagar volume. Títulos com
  prefixo `QA-F12A` (Parte A) e `QA-F12B` (Parte B) no nome e no emissor. A Parte A **não altera
  cotações** do banco do Compose; quem mexe em `index_rate` é só a Parte B, num banco próprio.
- **500 é sempre achado.** Entrada inválida deve virar 400 no envelope `ApiResponse`, com mensagem
  útil e `data` nulo.
- **Não invente resultado.** O que depender de rede, de janela de horário ou de autorização não
  dada fica `NÃO EXECUTADO`, com o motivo.
- **Números conferidos no texto do JSON.** Valores monetários e percentuais são comparados pelo
  texto que a API devolveu (os scripts abaixo leem a fonte do número), nunca por `float`.

## Ambiente

### O comportamento depende de hoje e da última cotação

Quase todo esperado desta fase muda com quatro datas. O executor as registra **antes de começar**,
de novo **no fim de cada parte**, e põe as duas leituras no cabeçalho do relatório:

| Símbolo | O que é | Como obter | Valor em 27/11/2026, sexta |
|---|---|---|---|
| **T** | hoje em Brasília (`America/Sao_Paulo`, sem horário de verão desde 2019) | `date -u -d '-3 hours' '+%F %T'` no Git Bash; conferir com o cabeçalho `Date` (UTC) da API | 2026-11-27 |
| **P** | dia útil anterior a hoje: maior dia útil estritamente antes de T (RN-12) | calendário: feriados de `holiday` (leitura) | 2026-11-26 |
| **L** | última cotação de CDI do banco | último item de `GET /api/indexes/Cdi?from=<T−20 dias>&to=<T>` | 2026-11-25 (a de 26/11 pode chegar durante a sessão) |
| **D** | data padrão: dia útil seguinte a L | `referenceDate` de `GET /api/assets/{QA-F12A VAZIO}/projection` (sem data) | 2026-11-26 (ou 27/11, se L virar 26/11) |
| **M** | último mês de IPCA publicado | último item de `GET /api/indexes/Ipca?from=<T−400 dias>&to=<T>` | 2026-10 (variação 0,14) |
| **μ** | variação mensal projetada do IPCA: média geométrica dos 12 meses até M, 2 casas (D-5) | `node "$QAW/js/ipca.mjs" --media` sobre a listagem | janela 11/2025 a 10/2026, 0,3777% → **0,38** |

Feriados próximos (R1 da spec): 02/11, 20/11 e 25/12/2026; 01/01/2027. Novembro de 2026 tem 19
dias úteis e dezembro, 22; o primeiro dia útil de novembro é 03/11.

Os esperados escritos neste roteiro assumem **T = 2026-11-27**, **L ∈ {25/11, 26/11}**, **M =
2026-10** e o CDI de 16/11 em diante a **0,050788** % a.d. (taxa de L também). Se a execução for
em outro dia, ou se a listagem mostrar outra taxa, o executor **recalcula a tabela antes de rodar**
(com os scripts de `$QA/js`) e cola no relatório a versão recalculada. Execução em dezembro de
2026, antes da divulgação do IPCA de novembro (por volta de 10/12), muda os casos de IPCA da
Parte A: novembro passa a mês encerrado e não divulgado (`isEstimated`), e dezembro a projetado.

A importação do Compose fica ligada (padrão, a cada 6 horas). Se L mudar durante a Parte A, os
casos já rodados não são repetidos: o relatório diz qual L valia em cada um (QA12-A30).

### Partes e isolamento

O roteiro tem duas partes independentes, que podem rodar em paralelo depois da preparação comum:

| | Parte A | Parte B |
|---|---|---|
| API | `fixedincome-api` do Compose, `http://localhost:8080` | `qaf12-api`, `http://localhost:28112`, importação desligada |
| Banco | `fixedincome-db` (do Matheus, com dados reais e de QAs anteriores) | `qaf12-db`, banco `qaf12`, sem porta publicada |
| Cotações | só leitura | copiadas do Compose e manipuladas à vontade, com restauração entre cenários |
| Prefixo dos títulos | `QA-F12A` | `QA-F12B` |
| Para quê | contrato, entradas, coerência, matriz de marcas com o hoje real | mudanças de comportamento com L controlado, janela de IPCA, lacunas, bordas, volume |

O Apache do Windows ocupa `127.0.0.1:8080` (R9-03 da F9): use sempre `localhost` e confira
`Server: Kestrel` na primeira resposta.

### O que não pode ser tocado

- **O banco do Compose** (`fixedincome-db`, volume `renda-fixa-api_fixedincome-db-data`): a Parte A
  só cria títulos e aportes pela API. Leitura por `psql` só para as fotos e o `pg_dump` da
  preparação (decisão 1). Nenhum `insert`, `update` ou `delete` direto.
- **`index_rate` do Compose**: nunca alterada. A validação apagou e reinseriu linhas; este roteiro
  não repete isso. Se a decisão 3 for negada (sem Parte B), o caminho alternativo está em
  "Restauração, se a Parte B não existir".
- **O repositório**: só leitura, exceto `docs/QA-F12.md`. Os scripts e saídas ficam no scratchpad.
- **A imagem `renda-fixa-api-api:latest`**: a Parte B só roda um contêiner novo a partir dela.
- **O Docker Desktop**: não parar, não reiniciar, não limpar cache.

### Preparação comum (Git Bash, sequencial)

No PowerShell 5.1, `curl` é alias de `Invoke-WebRequest`: use o Git Bash para tudo.

0. **Variáveis e foto do estado** (só leitura):

   ```bash
   export MSYS_NO_PATHCONV=1
   REPO=/c/Users/matheuss/Desktop/renda-fixa/renda-fixa-api
   QA="<scratchpad da sessão>/qaf12"; mkdir -p "$QA"/{a,b,js}
   QAW="$(cygpath -m "$QA")"          # caminho C:/... para o node (o MSYS_NO_PATHCONV desliga a conversão)
   cd "$REPO" && git rev-parse HEAD && git status --short > "$QA/git-status-antes.txt"
   docker ps -a --format '{{.Names}} {{.Image}} {{.Status}} {{.Ports}}' > "$QA/antes-ps.txt"
   docker volume ls -q | sort > "$QA/antes-volumes.txt"
   node --version          # 21 ou mais: os scripts leem o texto do número (JSON.parse com context.source)
   node -e "JSON.parse('1.50',(k,v,c)=>console.log(c.source))"   # imprime 1.50
   ```

1. **API do Compose com o código da fase** (o mesmo que a validação fez; recria os dois
   contêineres, preserva o volume):

   ```bash
   cd "$REPO" && docker compose up -d --build
   until curl -sf http://localhost:8080/health >/dev/null; do sleep 2; done
   curl -sI http://localhost:8080/health | grep -iE '^(HTTP|server|date)'
   docker exec fixedincome-db psql -U fixedincome -d fixedincome -tAc \
     'select "MigrationId" from "__EFMigrationsHistory" order by 1'      # 3 migrations, a última ExtendAnbimaHolidaysTo2050
   docker exec fixedincome-db psql -U fixedincome -d fixedincome -tAc \
     "select min(date), max(date), count(*) from holiday"                # cobertura até 2050
   ```

   Se o Compose estiver sem a migration de 2050 (QACF-R1 do contrato para o front), registre e siga:
   os casos com datas depois de 2030 passam a ser `NÃO EXECUTADO` na Parte A.

2. **Foto das cotações do Compose** (para provar no fim que só cresceram):

   ```bash
   docker exec fixedincome-db psql -U fixedincome -d fixedincome -tAc \
     "select index_type, count(*), min(reference_date), max(reference_date),
             md5(string_agg(reference_date||':'||value, ',' order by reference_date))
        from index_rate group by 1 order by 1" > "$QA/antes-index-rate.txt"
   cat "$QA/antes-index-rate.txt"
   ```

3. **Scripts de conferência** em `$QA/js`. Todos leem o texto dos números; nenhum usa `float` para
   dinheiro.

   `cdi.mjs` — fator da RN-09 (fator diário exato, acumulado truncado em 16 casas, fator final em
   8) a partir da listagem de cotações; com o segundo argumento, completa até `DU` dias com a taxa
   da última cotação, como a RN-12 manda:

   ```bash
   cat > "$QA/js/cdi.mjs" <<'EOF'
   // uso: curl -s "$B/api/indexes/Cdi?from=<from>&to=<véspera do fim>" | node cdi.mjs [percentual=100] [DU total]
   import { readFileSync } from 'node:fs';
   const [p = '100', du] = process.argv.slice(2);
   const vals = JSON.parse(readFileSync(0, 'utf8'), (k, v, c) => typeof v === 'number' ? c.source : v).data.map(x => x.value);
   const sc = (s, n) => { const neg = s.startsWith('-'); const [i, f = ''] = s.replace('-', '').split('.'); const v = BigInt(i + (f + '0'.repeat(n)).slice(0, n)); return neg ? -v : v; };
   const S = 10n ** 16n, P = sc(p, 16);
   const taxas = [...vals]; if (du) while (taxas.length < Number(du)) taxas.push(vals.at(-1));
   let acc = S;
   for (const t of taxas) acc = acc * (S + sc(t, 16) / 100n * P / (S * 100n)) / S;
   const f8 = (acc + 5n * 10n ** 7n) / 10n ** 8n, d = f8 - 10n ** 8n, a = d < 0n ? -d : d;
   const pct = `${d < 0n ? '-' : ''}${a / 10n ** 6n}.${String(a % 10n ** 6n).padStart(6, '0')}`;
   const bruto = (f8 + 500n) / 1000n;
   console.log(`publicados ${vals.length} projetados ${taxas.length - vals.length} fator ${f8 / 10n ** 8n}.${String(f8 % 10n ** 8n).padStart(8, '0')} percentual ${pct} bruto_de_1000 ${bruto / 100n}.${String(bruto % 100n).padStart(2, '0')}`);
   EOF
   ```

   `ipca.mjs` — produto exato de `1 + IPCA/100`, fator em 7 casas e percentual em 6 (RN-13, D-10);
   com `--media`, a variação projetada μ dos 12 últimos meses da listagem (D-5):

   ```bash
   cat > "$QA/js/ipca.mjs" <<'EOF'
   // uso: curl -s "$B/api/indexes/Ipca?from=<AAAA-MM-01 do mês de from>&to=<to>" | node ipca.mjs [--media]
   import { readFileSync } from 'node:fs';
   const vals = JSON.parse(readFileSync(0, 'utf8'), (k, v, c) => typeof v === 'number' ? c.source : v).data.map(x => x.value);
   if (process.argv[2] === '--media') {
     const u = vals.slice(-12), prod = u.reduce((a, v) => a * (1 + Number(v) / 100), 1), m = (prod ** (1 / 12) - 1) * 100;
     console.log(`meses ${u.length} media ${m.toFixed(6)} -> ${(Math.sign(m) * Math.round(Math.abs(m) * 100) / 100).toFixed(2)}`); process.exit(0);
   }
   const v2 = s => { const neg = s.startsWith('-'); const [i, f = ''] = s.replace('-', '').split('.'); const v = BigInt(i + (f + '00').slice(0, 2)); return neg ? -v : v; };
   let num = 1n; for (const v of vals) num *= 10000n + v2(v);
   const esc = 4 * vals.length;
   const f7 = esc >= 7 ? (num + 5n * 10n ** BigInt(esc - 8)) / 10n ** BigInt(esc - 7) : num * 10n ** BigInt(7 - esc);
   const d = (f7 - 10n ** 7n) * 10n, a = d < 0n ? -d : d;
   console.log(`meses ${vals.length} fator ${f7 / 10n ** 7n}.${String(f7 % 10n ** 7n).padStart(7, '0')} percentual ${d < 0n ? '-' : ''}${a / 10n ** 6n}.${String(a % 10n ** 6n).padStart(6, '0')}`);
   EOF
   ```

   (Com um mês só, `esc` é 4 e o fator não precisa de arredondamento. Com dois, `esc − 8` é zero.)

   `soma.mjs` — invariantes da projeção por título (RN-07, RN-14, D-12):

   ```bash
   cat > "$QA/js/soma.mjs" <<'EOF'
   // uso: curl -s "$B/api/assets/<id>/projection[?referenceDate=...]" | node soma.mjs
   import { readFileSync } from 'node:fs';
   const r = JSON.parse(readFileSync(0, 'utf8'), (k, v, c) => typeof v === 'number' ? c.source : v).data;
   const big = s => { const neg = s.startsWith('-'); const [i, f = ''] = s.replace('-', '').split('.'); const v = BigInt(i + (f + '000000').slice(0, 6)); return neg ? -v : v; };
   const campos = [['totalInvestedAmount', 'investedAmount'], ['totalGrossAmount', 'grossAmount'], ['totalGrossYield', 'grossYield'], ['totalTaxAmount', 'taxAmount'], ['totalIofAmount', 'iofAmount'], ['totalNetAmount', 'netAmount']];
   for (const [t, i] of campos) { const s = r.positions.reduce((a, p) => a + big(p[i]), 0n); console.log(t, s === big(r[t]) ? 'OK' : `DIVERGE itens=${s} total=${big(r[t])}`); }
   console.log('positionCount', Number(r.positionCount) === r.positions.length ? 'OK' : 'DIVERGE');
   console.log('liquido = bruto - IR - IOF', big(r.totalNetAmount) === big(r.totalGrossAmount) - big(r.totalTaxAmount) - big(r.totalIofAmount) ? 'OK' : 'DIVERGE');
   console.log('assetId dos itens', r.positions.every(p => p.assetId === r.assetId) ? 'OK' : 'DIVERGE');
   const d = r.positions.map(p => p.applicationDate); console.log('ordem (mais recente primeiro)', d.every((x, k) => k === 0 || d[k - 1] >= x) ? 'OK' : 'DIVERGE ' + d.join(' '));
   console.log('chaves da raiz', Object.keys(r).join(','));
   EOF
   ```

   `coerencia.mjs` — cada item da projeção por título contra a projeção da posição (na
   `referenceDate` do item) e contra o item do resumo, campo a campo, no texto:

   ```bash
   cat > "$QA/js/coerencia.mjs" <<'EOF'
   // uso: node coerencia.mjs <base> <assetId> [referenceDate]
   const [base, assetId, ref] = process.argv.slice(2), q = ref ? `?referenceDate=${ref}` : '';
   const get = async u => { const r = await fetch(base + u); return { status: r.status, body: JSON.parse(await r.text(), (k, v, c) => typeof v === 'number' ? c.source : v) }; };
   const diff = (a, b) => [...new Set([...Object.keys(a), ...Object.keys(b)])].filter(k => a[k] !== b[k]).map(k => `${k}: ${a[k]} x ${b[k]}`);
   const t = await get(`/api/assets/${assetId}/projection${q}`);
   if (t.status !== 200) { console.log('titulo', t.status, t.body.message); process.exit(0); }
   const s = await get(`/api/portfolio/summary${q}`);
   const doResumo = s.status === 200 ? new Map(s.body.data.positions.map(p => [p.projection.positionId, p.projection])) : null;
   if (!doResumo) console.log('resumo', s.status, s.body.message);
   for (const it of t.body.data.positions) {
     const p = await get(`/api/positions/${it.positionId}/projection?referenceDate=${it.referenceDate}`);
     const d1 = p.status === 200 ? diff(it, p.body.data) : [`status ${p.status} ${p.body.message}`];
     const d2 = !doResumo ? ['resumo indisponível'] : doResumo.has(it.positionId) ? diff(it, doResumo.get(it.positionId)) : ['ausente no resumo'];
     console.log(it.positionId, it.applicationDate, '| posição:', d1.length ? d1.join('; ') : 'OK', '| resumo:', d2.length ? d2.join('; ') : 'OK');
   }
   if (doResumo) {
     const ids = t.body.data.positions.map(p => p.positionId);
     const noResumo = s.body.data.positions.filter(p => p.asset.id === assetId).map(p => p.projection.positionId);
     console.log('mesmos aportes no resumo', JSON.stringify([...ids].sort()) === JSON.stringify([...noResumo].sort()) ? 'OK' : `DIVERGE título=${ids.length} resumo=${noResumo.length}`);
     console.log('mesma ordem do resumo', JSON.stringify(ids) === JSON.stringify(noResumo) ? 'OK' : 'difere (aceitável só entre aportes do mesmo dia, D-12)');
   }
   EOF
   ```

   `somatitulos.mjs` — a soma das projeções de todos os títulos contra os totais do resumo (o
   mesmo projetor serve aos dois, D-11):

   ```bash
   cat > "$QA/js/somatitulos.mjs" <<'EOF'
   // uso: node somatitulos.mjs <base> [referenceDate]
   const [base, ref] = process.argv.slice(2), q = ref ? `?referenceDate=${ref}` : '';
   const get = async u => { const r = await fetch(base + u); return { status: r.status, body: JSON.parse(await r.text(), (k, v, c) => typeof v === 'number' ? c.source : v) }; };
   const big = s => { const neg = s.startsWith('-'); const [i, f = ''] = s.replace('-', '').split('.'); const v = BigInt(i + (f + '000000').slice(0, 6)); return neg ? -v : v; };
   const campos = ['totalInvestedAmount', 'totalGrossAmount', 'totalGrossYield', 'totalTaxAmount', 'totalIofAmount', 'totalNetAmount', 'positionCount'];
   const titulos = (await get('/api/assets')).body.data, soma = Object.fromEntries(campos.map(c => [c, 0n])), falhas = [];
   let algumProjetado = false;
   for (const t of titulos) {
     const r = await get(`/api/assets/${t.id}/projection${q}`);
     if (r.status !== 200) { falhas.push(`${t.name}: ${r.status} ${r.body.message}`); continue; }
     for (const c of campos) soma[c] += big(r.body.data[c]);
     algumProjetado ||= r.body.data.positions.some(p => p.isProjected === true);
   }
   console.log('títulos', titulos.length, 'com falha', falhas.length); falhas.forEach(f => console.log('  ', f));
   const s = await get(`/api/portfolio/summary${q}`);
   if (s.status !== 200) { console.log('resumo', s.status, s.body.message); process.exit(0); }
   for (const c of campos) console.log(c, soma[c] === big(s.body.data[c]) ? 'OK' : `DIVERGE títulos=${soma[c]} resumo=${big(s.body.data[c])}`);
   console.log('isProjected da raiz', s.body.data.isProjected, 'OR dos itens', s.body.data.positions.some(p => p.projection.isProjected === true), 'OR dos títulos', algumProjetado);
   EOF
   ```

4. **Funções da sessão:**

   ```bash
   B=http://localhost:8080                 # Parte A; na Parte B, B=http://localhost:28112
   get()  { curl -s -w '\n%{http_code} %{time_total}s\n' "$B$1"; }
   post() { curl -s -X POST -H 'Content-Type: application/json' -d "$2" "$B$1"; echo; }
   id()   { node -e "process.stdout.write(JSON.parse(require('fs').readFileSync(0,'utf8')).data.id)"; }
   chaves() { node -e "const d=JSON.parse(require('fs').readFileSync(0,'utf8')).data; console.log(Object.keys(d).join(','))"; }
   titulo() { post /api/assets "{\"name\":\"$1\",\"issuer\":\"${1%% *} Banco\",\"assetType\":\"$2\",\"indexType\":\"$3\",\"rate\":$4,\"issueDate\":\"$5\",\"maturityDate\":\"$6\"}" | id; }
   aporte() { post /api/positions "{\"assetId\":\"$1\",\"investedAmount\":$2,\"applicationDate\":\"$3\"}" | id; }
   ```

   Guarde cada id devolvido em `$QA/a/ids.txt` (ou `b/ids.txt`) com o nome do título: vai para a
   seção "Dados criados" do relatório.

5. **Leitura das datas T, P, L, D, M e μ** (tabela acima). Crie primeiro o título vazio da Parte A,
   porque é ele que dá D sem depender da carteira de ninguém:

   ```bash
   VAZIO=$(titulo "QA-F12A VAZIO" Cdb PreFixed 10 2026-01-01 2028-01-03); echo "VAZIO $VAZIO" >> "$QA/a/ids.txt"
   date -u '+%F %T UTC'; date -u -d '-3 hours' '+%F %T BRT'
   get "/api/indexes/Cdi?from=2026-11-01&to=2026-12-31"            # L é o último item; taxa de L
   get "/api/indexes/Ipca?from=2025-09-01&to=2026-12-31"           # M é o último item
   curl -s "$B/api/indexes/Ipca?from=2025-09-01&to=2026-12-31" | node "$QAW/js/ipca.mjs" --media
   get "/api/assets/$VAZIO/projection"                             # referenceDate = D; deve ser o dia útil seguinte a L
   ```

   Esperado: D é o dia útil seguinte a L. Diferente disso é **BUG (alta)** e invalida a matriz da
   Parte A: pare e registre.

### Preparação da Parte B (pilha isolada)

Só depois do passo 1 (a imagem `renda-fixa-api-api:latest` precisa ter o código da fase).

```bash
PG=QaF12Senha123
docker network create qaf12-net
docker run -d --name qaf12-db --network qaf12-net -e POSTGRES_USER=qaf12 -e POSTGRES_PASSWORD=$PG \
  -e POSTGRES_DB=qaf12 postgres:16-alpine
until docker exec qaf12-db pg_isready -U qaf12 >/dev/null 2>&1; do sleep 1; done
# esquema completo e só os dados de mercado do Compose (pg_dump é leitura)
docker exec fixedincome-db pg_dump -U fixedincome -d fixedincome --schema-only --no-owner --no-privileges > "$QA/b/schema.sql"
docker exec fixedincome-db pg_dump -U fixedincome -d fixedincome --data-only --no-owner \
  -t holiday -t index_rate -t '"__EFMigrationsHistory"' > "$QA/b/mercado.sql"
docker exec -i qaf12-db psql -U qaf12 -d qaf12 -q < "$QA/b/schema.sql"
docker exec -i qaf12-db psql -U qaf12 -d qaf12 -q -v ON_ERROR_STOP=1 < "$QA/b/mercado.sql"
sqlb() { docker exec qaf12-db psql -U qaf12 -d qaf12 -tAc "$1"; }
sqlb "select count(*) from fixed_income_asset"            # 0: nenhum título do Matheus foi copiado
sqlb "select index_type, count(*), max(reference_date) from index_rate group by 1 order by 1" | tee "$QA/b/base-index-rate.txt"
sqlb "create table qa_index_rate_bkp as table index_rate"
docker run -d --name qaf12-api --network qaf12-net -p 28112:8080 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e ConnectionStrings__DefaultConnection="Host=qaf12-db;Port=5432;Database=qaf12;Username=qaf12;Password=$PG" \
  -e IndexRatesImport__Enabled=false -e HTTPS_PROXY=http://127.0.0.1:9 -e HTTP_PROXY=http://127.0.0.1:9 \
  renda-fixa-api-api:latest
until curl -sf http://localhost:28112/health >/dev/null; do sleep 1; done
docker logs qaf12-api 2>&1 | grep -iE 'hosting environment|sgs|import' | head
```

`index_type` é gravado como texto (`'Cdi'`, `'Ipca'`). Funções de manipulação, só em `qaf12-db`:

```bash
restaura()  { sqlb "begin; delete from index_rate; insert into index_rate select * from qa_index_rate_bkp; commit;"
              sqlb "select index_type, count(*), max(reference_date) from index_rate group by 1 order by 1" | diff - "$QA/b/base-index-rate.txt" && echo "cotações restauradas"; }
cdi_ate()   { sqlb "delete from index_rate where index_type='Cdi' and reference_date > '$1'"; }      # L passa a ser o último dia útil <= $1
tira()      { sqlb "delete from index_rate where index_type='$1' and reference_date = '$2'"; }      # tira Cdi 2026-11-18 / tira Ipca 2025-11-01
sintetico() { sqlb "insert into index_rate (id, index_type, reference_date, value) values (gen_random_uuid(), 'Cdi', '$1', $2) on conflict do nothing"; }
```

Depois de cada manipulação, confira pela própria API (`GET /api/indexes/...`) que a mudança
aparece. Se a resposta parecer não refletir a mudança, rode `docker restart qaf12-api`, repita, e
registre como **RISCO** (cache de mercado que a spec não prevê). **Cada cenário da Parte B começa e
termina com `restaura`.** Cotação sintética (`sintetico`) só existe na Parte B e é marcada como
tal no relatório.

Se o Docker não estiver disponível, todo o roteiro fica `NÃO EXECUTADO`, exceto a leitura de README,
`.http` e `swagger.json` versionado em teste (QA12-A28 e QA12-A29 só por leitura).

### Restauração, se a Parte B não existir

Só com a decisão 3 negada **e** autorização explícita para mexer em `index_rate` do Compose. Antes
de apagar qualquer linha, salve-a; depois, reinsira com o mesmo `id` e confira a foto:

```bash
sqla() { docker exec fixedincome-db psql -U fixedincome -d fixedincome -tAc "$1"; }
sqla "copy (select * from index_rate where index_type='Cdi' and reference_date > '2026-11-23') to stdout" > "$QA/a/salvas.tsv"
sqla "delete from index_rate where index_type='Cdi' and reference_date > '2026-11-23'"
# ... cenário ...
docker exec -i fixedincome-db psql -U fixedincome -d fixedincome -c "copy index_rate from stdin" < "$QA/a/salvas.tsv"
# a mesma consulta do passo 2, comparada com antes-index-rate.txt (a importação pode ter acrescentado linhas novas)
```

A importação ligada do Compose pode recolocar as linhas apagadas no meio do cenário (ciclo de 6
horas). Por isso a Parte B é o caminho preferido.

## Cenários

Legenda: **BUG** quando a resposta contraria a especificação, a spec da fase, o README ou uma regra
do agente (500, valor diferente do recálculo, marca errada, envelope fora do padrão); **RISCO**
quando é defensável pela spec, mas surpreende o front ou o usuário, ou quando só uma janela de
horário confirmaria. Cada cenário traz prioridade (P1 a P3), parte e tempo estimado.

Severidade dos bugs no relatório:

- **alta**: 500; valor monetário ou percentual diferente do recálculo independente (mesmo em
  R$ 0,01); `isProjected` ou `isEstimated` errados (o front rotularia errado um valor); 200 onde a
  regra manda 400 por falha de importação (esconde a importação parada); total da projeção por
  título diferente da soma dos itens; item da projeção por título diferente da projeção da posição;
  "hoje" calculado em UTC.
- **média**: 400 com mensagem enganosa ou sem a data culpada; mesma entrada tratada de forma
  diferente em dois endpoints; `effectiveTo` errado com o percentual certo; ordem dos aportes
  errada; `swagger.json` divergente do que a rota faz; uma posição que derruba o resumo inteiro
  contra a regra.
- **baixa**: escala ou formatação do número sem efeito de valor; divergência de documentação; `.http`
  ou exemplo do README que não funciona como descrito.

### Parte A — API do Compose, cotações reais (`QA-F12A`)

#### QA12-A00 — Reconhecimento [P1, A, 10 min]

Passos 0 a 5 da preparação comum. Registre também:

```bash
get "/api/indexes/Cdi/accumulated?from=2026-09-01&to=2026-10-01"      # o caso da validação ainda dá 1.081673
get "/api/portfolio/summary" | tail -c 400                             # status e isProjected da raiz com a carteira real
```

Esperado: `1.081673` e `effectiveTo 2026-10-01`; resumo 200 (com 400, anote a mensagem: a carteira
tem dado de QA anterior que pode derrubá-lo, QACF-R2, e a matriz da A24 passa a usar só a projeção
por título).

#### QA12-A01 — Acumulado: formatos de data [P1, A, 15 min]

```bash
U=/api/indexes/Cdi/accumulated
for q in "from=2026-13-01&to=2026-12-01" "from=2026-02-30&to=2026-03-10" "from=31/12/2026&to=2027-01-10" \
         "from=01/09/2026&to=01/10/2026" "from=09/01/2026&to=10/01/2026" "from=2026-9-1&to=2026-10-1" \
         "from=20260901&to=20261001" "from=2026/09/01&to=2026/10/01" "from=ontem&to=hoje" \
         "from=&to=2026-10-01" "from=%20&to=2026-10-01" "from=2026-09-01&to=" \
         "from=2026-09-01T00:00:00&to=2026-10-01" "from=2026-09-01T23:30:00-03:00&to=2026-10-01" \
         "from=2026-09-01&to=2026-10-01&from=2026-09-15" "FROM=2026-09-01&TO=2026-10-01" \
         "from_date=2026-09-01&to=2026-10-01" "from=%202026-09-01&to=2026-10-01" ""; do
  printf '%-60s ' "$q"; curl -s "$B$U?$q" | head -c 220; echo; done
```

Repita as linhas 1, 4, 5, 10 e 13 com `Ipca` no lugar de `Cdi`.

Esperado (README, "formato `yyyy-MM-dd`"; spec P3, `from` e `to` `[Required]`): data malformada,
400 "O valor do campo 'from' é inválido." (ou `to`); vazio e ausente, 400 "O campo 'from' é
obrigatório."; `FROM`/`TO` em maiúsculas, 200 (binder sem caixa); sem nada, 400 citando os dois
campos ou o primeiro. Registre como cada formato alternativo é interpretado (o `from` ecoado na
resposta diz a data que a API entendeu).

- BUG (alta): 500 em qualquer linha.
- BUG (média): campo vazio aceito como data qualquer (200) num parâmetro obrigatório; mensagem que não
  diz qual campo.
- RISCO (média): `01/09/2026` aceito como **9 de janeiro** (`MM/dd` da cultura invariante) em vez de
  400. Um usuário brasileiro que digite a data no formato local recebe outro período sem aviso.
- RISCO (baixa): data com hora aceita, e o parâmetro repetido usando um dos valores sem aviso
  (QACF-R3 já registrou o mesmo no resumo; anote se é igual aqui).

#### QA12-A02 — Acumulado: `indexType` [P1, A, 15 min]

```bash
for t in Cdi cdi CDI cDi Ipca ipca IPCA %20Cdi%20 Cdi%20 PreFixed prefixed Selic accumulated \
         0 1 2 3 -1 +1 %201 1.0 0x1 Cdi,Ipca PreFixed,Cdi Cdi,Cdi 'Cdi%2Caccumulated' Cdi%2Faccumulated; do
  printf '%-22s acum: ' "$t"; curl -s "$B/api/indexes/$t/accumulated?from=2026-09-01&to=2026-10-01" | head -c 160; echo
  printf '%-22s list: ' "$t"; curl -s "$B/api/indexes/$t?from=2026-09-14&to=2026-09-15" | head -c 120; echo; done
curl -si "$B/api/indexes/Cdi/accumulated/?from=2026-09-01&to=2026-10-01" | head -1
curl -si "$B/api/indexes/Cdi/ACCUMULATED?from=2026-09-01&to=2026-10-01" | head -1
curl -si "$B/api/Indexes/Cdi/accumulated?from=2026-09-01&to=2026-10-01" | head -1      # grafia do Swagger
curl -si "$B/api/indexes/accumulated?from=2026-09-01&to=2026-10-01" | head -1
for m in POST PUT DELETE PATCH; do curl -si -X $m "$B/api/indexes/Cdi/accumulated?from=2026-09-01&to=2026-10-01" | grep -iE '^(HTTP|allow)'; done
```

Esperado (README, "O indexador vai pelo nome, sem diferenciar maiúsculas; o número do enum também
devolve 400"; spec P3, "a mesma validação do `indexType` por nome da ação existente"): `cdi`,
`CDI`, `ipca` com 200 e `indexType` canônico na resposta (`"Cdi"`, `"Ipca"`); `PreFixed`/`prefixed`
400 "Só há cotação para CDI e IPCA."; números, `Selic`, `accumulated` e combinações 400 "O valor
do campo 'indexType' é inválido."; o **mesmo** resultado nas duas rotas para cada valor; verbos
diferentes de GET com 405 no envelope e `Allow: GET`.

- BUG (alta): 500; `PreFixed,Cdi` ou `Cdi,Cdi` respondendo 200 como CDI (combinação de flags do
  `Enum.TryParse` passando pela validação por nome).
- BUG (média): um valor aceito numa rota e recusado na outra; `indexType` da resposta ecoando a
  grafia pedida (`"cdi"`) em vez do nome do enum; 405 sem envelope ou sem `Allow`.
- RISCO (baixa): `%20Cdi%20` aceito (o `Enum.TryParse` tolera espaço) sem que o README diga; só é
  BUG se as duas rotas divergirem.

#### QA12-A03 — Acumulado: datas extremas [P1, A, 15 min]

```bash
for i in Cdi Ipca; do for q in "from=0001-01-01&to=9999-12-31" "from=0001-01-01&to=0001-01-02" \
    "from=0001-01-01&to=0001-01-01" "from=2019-12-31&to=2020-01-10" "from=2020-01-01&to=2020-01-03" \
    "from=2020-01-01&to=9999-12-31" "from=2026-11-02&to=9999-12-31" "from=2050-12-30&to=2050-12-31" \
    "from=2051-01-01&to=2051-02-01" "from=9999-12-01&to=9999-12-31" "from=9999-12-31&to=9999-12-31"; do
  printf '%-5s %-34s ' $i "$q"; curl -s -w ' [%{http_code} %{time_total}s]' "$B/api/indexes/$i/accumulated?$q" | head -c 240; echo; done; done
```

Esperado (D-9; RN-13):

- CDI com `from` fora da cobertura do calendário (antes de 2020-01-01 ou depois de 2050-12-31):
  400 com a mensagem do calendário ("Data … fora do período coberto pelo calendário (2020-01-01 a
  2050-12-31)."), **ou** a de período sem cotação quando o corte já deixa o período vazio
  (`from` depois de D). Registre qual sai em cada linha: a D-9 diz que o corte é calculado antes de
  percorrer o intervalo.
- CDI `from=2020-01-01&to=2020-01-03`: 200, um dia útil (02/01; 01/01 é feriado), percentual igual à
  taxa de 2020-01-02.
- CDI `to=9999-12-31` com `from` dentro da cobertura: 200 com corte, `effectiveTo` = D (o `to`
  além da cobertura não é erro, D-9).
- IPCA não consulta o calendário (D-9): `from=0001-01-01` dá 400 de lacuna (mês sem cotação antes
  do último publicado) citando o primeiro mês ausente; `from=2020-01-01&to=9999-12-31` dá 200 com
  `effectiveTo` = último dia de M; `from=9999-12-01` dá 400 de período sem cotação.
- Tempo de resposta de cada linha abaixo de 2 s.

- BUG (alta): 500 (estouro de `DateOnly` ao somar um dia ou um mês a 9999-12-31, por exemplo).
- BUG (média): IPCA com `from` antigo respondendo com a mensagem do calendário (a D-9 diz que o IPCA
  não o consulta); mensagem de lacuna sem o mês.
- RISCO (baixa): tempo acima de 2 s em `0001-01-01` a `9999-12-31` (laço por mês ou por dia sem
  limite).

#### QA12-A04 — Acumulado: `from == to` e períodos de um dia [P1, A, 10 min]

Com L = 25/11 (ajuste se L mudou):

| Chamada | Esperado | Regra |
|---|---|---|
| `Cdi/accumulated?from=2026-11-24&to=2026-11-24` | 400 período sem cotação | D-9: nenhum dia útil em `[from, from)` |
| `Cdi/accumulated?from=2026-11-24&to=2026-11-25` | 200, `0.050788`, `effectiveTo 2026-11-25` | um dia, fim exclusivo |
| `Cdi/accumulated?from=2026-11-20&to=2026-11-23` | 400 | 20/11 feriado, 21 e 22 fim de semana |
| `Cdi/accumulated?from=2026-11-21&to=2026-11-24` | 200, `0.050788` | só 23/11 |
| `Cdi/accumulated?from=2026-11-20&to=2026-11-24` | 200, `0.050788` | `from` em feriado não é erro |
| `Cdi/accumulated?from=2026-11-27&to=2026-11-27` | 400 | depois de L |
| `Ipca/accumulated?from=2026-10-01&to=2026-10-01` | 200, `0.140000` | D-9: `from == to` num mês publicado |
| `Ipca/accumulated?from=2026-10-31&to=2026-10-31` | 200, `0.140000` | idem, último dia |
| `Ipca/accumulated?from=2026-10-31&to=2026-10-01` | 400 "A data inicial não pode ser posterior à data final." | comparação por dia, mesmo no mesmo mês |
| `Ipca/accumulated?from=2026-11-01&to=2026-11-01` | 400 período sem cotação | mês depois de M |

- BUG (alta): 500; percentual diferente do esperado.
- RISCO (baixa): a mensagem de `from == to` num dia útil **com** cotação dizer "Não há cotação de CDI
  publicada no período" (há cotação no dia; o que falta é dia no intervalo). O front mostraria
  "sem cotação" para uma data que tem cotação. Anote o texto exato e a mensagem do fim de semana.

#### QA12-A05 — Acumulado: corte e idempotência com a última cotação real [P1, A, 15 min]

Com L = 25/11 (quarta) e D = 26/11. Se L for 26/11, desloque um dia útil (D = 27/11).

| `to` | `effectiveTo` esperado | Observação |
|---|---|---|
| L (2026-11-25) | 2026-11-25 | sem corte; L fica de fora (fim exclusivo) |
| D (2026-11-26) | 2026-11-26 | sem corte |
| 2026-11-27 | 2026-11-26 | corte |
| 2026-11-28 (sábado) | 2026-11-26 | corte |
| 2026-12-25 (feriado) | 2026-11-26 | corte |

```bash
for to in 2026-11-25 2026-11-26 2026-11-27 2026-11-28 2026-12-25; do
  curl -s "$B/api/indexes/Cdi/accumulated?from=2026-11-16&to=$to"; echo; done
# idempotência: chamar de novo com to = effectiveTo devolve o mesmo percentual e o mesmo effectiveTo
for to in 2026-10-15 2026-10-31 2026-11-01 2026-11-30 2027-03-01; do
  curl -s "$B/api/indexes/Ipca/accumulated?from=2026-01-15&to=$to"; echo; done
curl -s "$B/api/indexes/Ipca/accumulated?from=2026-10-31&to=2026-11-15"; echo     # effectiveTo 2026-10-31, igual a from
```

Esperado: `to` ecoado como pedido (inclusive o sábado e o feriado); CDI com D e com o corte dando
o mesmo percentual; IPCA com `to=2026-10-15` sem corte (`effectiveTo 2026-10-15`, a D-9 diz que
sem corte é o próprio `to`), e do 01/11 em diante `effectiveTo 2026-10-31` e o mesmo percentual
(`4.096850` de 01/2026 a 10/2026); `from=2026-10-31&to=2026-11-15` 200 com `0.140000`.

- BUG (alta): percentual diferente entre o corte e a chamada com `to = effectiveTo`.
- BUG (média): `effectiveTo` antes de `from`, ou `to` ecoado já cortado (o front perderia a data que o
  usuário pediu); IPCA com `to` no meio de M cortado para o último dia (o contrário da D-9).

#### QA12-A06 — Acumulado: períodos longos e tempo [P2, A, 10 min]

```bash
for q in "Cdi/accumulated?from=2020-01-02&to=2026-12-31" "Cdi/accumulated?from=2020-01-01&to=2050-12-31" \
         "Ipca/accumulated?from=2020-01-01&to=2026-12-31" "Cdi/accumulated?from=2024-01-02&to=2025-01-02"; do
  for k in 1 2 3 4 5; do curl -s -o /dev/null -w "%{time_total} " "$B/api/indexes/$q"; done; echo " $q"; done
```

Esperado: 200; tempo de cada chamada abaixo de 1 s a quente. Os valores são conferidos na A07.

- RISCO (baixa): mais de 1 s a quente no período inteiro (o front pode pedir "desde o primeiro
  aporte").

#### QA12-A07 — Acumulado contra a listagem de cotações [P1, A, 20 min]

Recalcule cada acumulado a partir de `GET /api/indexes/{indexType}` (só cotações publicadas):

```bash
ac() { curl -s "$B/api/indexes/$1/accumulated?from=$2&to=$3"; echo; }
# CDI: lista de from até a véspera de effectiveTo
for p in "2026-09-01 2026-10-01 2026-09-30" "2025-12-30 2026-01-05 2026-01-04" "2020-01-02 2020-03-02 2020-03-01" \
         "2024-01-02 2025-01-02 2025-01-01" "2020-01-02 2026-12-31 <D−1>"; do set -- $p
  ac Cdi $1 $2; curl -s "$B/api/indexes/Cdi?from=$1&to=$3" | node "$QAW/js/cdi.mjs"; done
# IPCA: lista do dia 1 do mês de from até to
for p in "2020-01-01 2026-12-31 2020-01-01" "2023-03-10 2024-02-20 2023-03-01" "2026-08-15 2026-08-15 2026-08-01"; do set -- $p
  ac Ipca $1 $2; curl -s "$B/api/indexes/Ipca?from=$3&to=$2" | node "$QAW/js/ipca.mjs"; done
```

Esperado: percentual da API igual, dígito a dígito, ao do script (RN-13, D-10). Referências
conhecidas: setembro de 2026 `1.081673`; IPCA 03/2023 a 02/2024 `4.496270` (Calculadora, na
validação); 08/2026 `-0.320000`.

Compare também as duas convenções de período na mesma consulta:

```bash
curl -s "$B/api/indexes/Ipca?from=2026-01-15&to=2026-09-20"; echo          # README: from=01-15 não inclui janeiro
ac Ipca 2026-01-15 2026-09-20                                               # RN-13: o mês de from entra (3.951320)
```

- BUG (alta): qualquer diferença de dígito entre a API e o recálculo.
- RISCO (baixa): o mesmo `from` incluir janeiro no acumulado e excluir na listagem. As duas regras
  estão escritas (README, "Exemplo de chamada" e "Variação acumulada"), mas um front que confira o
  acumulado pela listagem chega a outro número. Registre como observação se o README deixar a
  diferença clara para quem lê só uma das seções.

#### QA12-A08 — Acumulado contra a projeção de um CDB 100% do CDI [P1, A, 20 min]

```bash
CDI100=$(titulo "QA-F12A CDI100" Cdb Cdi 100 2026-01-01 2030-01-02)
LCI100=$(titulo "QA-F12A LCI100" Lci Cdi 100 2026-01-01 2028-01-03)
P1=$(aporte $CDI100 1000 2026-09-01); P2=$(aporte $CDI100 1000 2026-11-16); P3=$(aporte $LCI100 1000 2026-11-16)
get "/api/positions/$P1/projection?referenceDate=2026-10-01"     # bruto 1010.82 (caso 14), isProjected false
ac Cdi 2026-09-01 2026-10-01
get "/api/positions/$P2/projection?referenceDate=<D>"            # isProjected false
ac Cdi 2026-11-16 <D>
get "/api/positions/$P2/projection?referenceDate=2026-12-31"     # isProjected true
ac Cdi 2026-11-16 2026-12-31                                     # effectiveTo D
get "/api/positions/$P3/projection?referenceDate=<D>"            # LCI: mesmo bruto do CDB
```

Esperado (RN-13: "o do CDI fecha com o de um título 100% do CDI"; README): `grossAmount` =
`round(1000 × (1 + accumulatedPercentage/100), 2)` nas datas até D, com diferença zero; LCI com o
mesmo bruto e `taxRate 0`. Com `referenceDate` depois de D, o acumulado corta em D e a projeção
segue com dias projetados: o bruto da projeção bate com `node cdi.mjs 100 <businessDays>` sobre a
listagem de 16/11 até L, e o acumulado não (é esperado, não é achado).

- BUG (alta): bruto diferente do acumulado em datas até D; acumulado usando taxa projetada (passaria
  de D, ou daria o mesmo número da projeção depois de D).

#### QA12-A09 — Serialização e escala dos números novos [P2, A, 10 min]

Junte as respostas de A03 a A08 e confira no texto cru:

```bash
grep -ohE '"accumulatedPercentage":[^,}]*' "$QA/a/"*.txt | sort | uniq -c    # se as saídas foram salvas
curl -s "$B/api/indexes/Ipca/accumulated?from=2026-08-01&to=2026-08-31"; echo
curl -s "$B/api/indexes/Ipca/accumulated?from=2026-09-01&to=2026-09-30"; echo
```

Esperado (D-10: percentual com `Math.Round(…, 6)` "explícito para o JSON sair `1.081673`"; README:
"com até 6 casas"): número JSON (não texto), sem notação científica, com no máximo 6 casas;
negativo com sinal (`-0.320000`); `effectiveTo`, `from` e `to` em `yyyy-MM-dd`; `indexType` texto.

- BUG (baixa): mais de 6 casas, ou o mesmo valor com escalas diferentes em respostas diferentes
  (por exemplo, `0.8200` num lugar e `0.820000` noutro). A spec registra a escala `0.8200` como
  possível no Domain (casos 17b e 18) e a validação viu `0.140000` ao vivo: anote qual sai.
- BUG (média): percentual como texto, ou com exponencial.

#### QA12-A10 — Conferência externa de períodos novos [P3, A, 20 min, só com a decisão 4]

Calculadora do Cidadão (aba CDI, POST descrito em `docs/VALIDACAO-F7.md`), R$ 1.000, 100%:

| Período (calculadora, data final inclusiva no índice) | Chamada da API |
|---|---|
| 02/01/2020 a L | `Cdi/accumulated?from=2020-01-02&to=L` |
| 30/12/2025 a 05/01/2026 | `Cdi/accumulated?from=2025-12-30&to=2026-01-05` |
| 02/01/2024 a 02/01/2025 | `Cdi/accumulated?from=2024-01-02&to=2025-01-02` |

Esperado (critério 4 do ROADMAP): diferença de no máximo R$ 0,01 sobre R$ 1.000; registre o índice
da calculadora e o da API com 8 casas. A validação só conferiu períodos de 2026.

- BUG (alta): diferença maior que R$ 0,01.
- NÃO EXECUTADO: sem rede ou sem autorização.

#### QA12-A11 — Projeção por título: ids e rotas malformados [P1, A, 10 min]

```bash
G=$(curl -s "$B/api/assets" | node -e "const d=JSON.parse(require('fs').readFileSync(0,'utf8')).data; console.log(d.find(a=>a.name==='QA-F12A CDI100').id)")
for i in abc 00000000-0000-0000-0000-000000000000 "{$G}" "$(echo $G | tr a-f A-F)" "$(echo $G | tr -d -)" \
         "($G)" "$G%20" "%20$G" "${G}0" "'%20or%201=1--" "$(printf 'x%.0s' {1..2000})"; do
  printf '%-45.45s ' "$i"; curl -s -w ' [%{http_code}]' "$B/api/assets/$i/projection" | head -c 200; echo; done
curl -si "$B/api/assets//projection" | head -1
curl -si "$B/api/assets/$G/projection/" | head -1
curl -si "$B/api/assets/$G/Projection" | head -1
curl -si "$B/api/Assets/$G/projection" | head -1
for m in POST PUT DELETE PATCH; do curl -si -X $m "$B/api/assets/$G/projection" | grep -iE '^(HTTP|allow)'; done
curl -s "$B/api/assets/00000000-0000-0000-0000-000000000000/projection?referenceDate=2051-01-01"; echo
curl -s "$B/api/assets/00000000-0000-0000-0000-000000000000/projection?referenceDate=abc"; echo
```

Esperado (RN-14; QACF-13 para a rota irmã): id inválido, 400 "O valor do campo 'id' é inválido.";
GUID inexistente, 404 "Título com id … não encontrado." com `data` nulo; formatos que o `Guid`
aceita (chaves, maiúsculas, sem hífen, parênteses) respondendo igual ao GUID normal; outros verbos,
405 no envelope com `Allow: GET`; **404 antes do calendário** com id inexistente e data de 2051
(D-11: "o 404 fica em cada caso de uso, antes de chamar a classe"); data `abc`, 400 de formato.

- BUG (alta): 500.
- BUG (média): id inexistente com data fora do calendário respondendo 400 do calendário em vez de
  404; 405 sem envelope.
- RISCO (baixa): o id de 2.000 caracteres responder 404 HTML ou 414 sem envelope.

#### QA12-A12 — Projeção por título: `referenceDate` malformada ou vazia [P2, A, 10 min]

Com o título `QA-F12A CDI100` (`$G`):

```bash
for q in abc 2026-02-30 2026-13-01 31/12/2026 01/12/2026 26-11-16 "" %20 2026-11-26T00:00:00 \
         "2026-11-26&referenceDate=2026-06-15"; do
  printf '%-40s ' "$q"; curl -s -w ' [%{http_code}]' "$B/api/assets/$G/projection?referenceDate=$q" | head -c 160; echo; done
```

Esperado: o mesmo tratamento do resumo e da projeção da posição (QACF-10, QACF-14, QACF-R3):
malformada 400 "O valor do campo 'referenceDate' é inválido."; vazia ou só espaço tratada como
omitida (200 com D). Compare, para cada linha, com `GET /api/portfolio/summary?referenceDate=…`.

- BUG (média): tratamento diferente do resumo para a mesma entrada.
- RISCO (média): `01/12/2026` aceito como 12 de janeiro (mesmo risco da A01).

#### QA12-A13 — Carteira de teste da Parte A e valores esperados [P1, A, 20 min]

Crie os títulos e aportes (ids em `$QA/a/ids.txt`):

```bash
PRE=$(titulo "QA-F12A PRE10" Cdb PreFixed 10 2025-01-01 2030-01-02)
for d in 2025-04-01 2025-04-01 2026-11-16; do aporte $PRE 1000 $d; done; aporte $PRE 500 2026-12-15   # o último é posterior a D
CDI110=$(titulo "QA-F12A CDI110" Cdb Cdi 110 2026-01-01 2030-01-02); aporte $CDI110 1000 2026-11-16
LCAPRE=$(titulo "QA-F12A LCA PRE9" Lca PreFixed 9 2026-01-01 2028-01-03); aporte $LCAPRE 1000 2026-11-16
IPCA=$(titulo "QA-F12A IPCA6" Cdb Ipca 6 2026-01-01 2028-01-03); aporte $IPCA 1000 2026-10-01
```

Esperados, todos conferidos por recálculo (CDI a 0,050788 de 16/11 em diante; IPCA de outubro 0,14 e
μ = 0,38; IOF de 14 dias = 53%):

| Chamada | Bruto | IOF | IR | Líquido | Corridos / úteis | isProjected | isEstimated |
|---|---|---|---|---|---|---|---|
| `/api/assets/$PRE/projection?referenceDate=2025-04-22` (README, caso 29) | 2.009,86 (total) | 2,96 | 1,56 | 2.005,34 | 21 / 13 por item | false | false |
| aporte de `$CDI100` em 16/11, `referenceDate=2026-11-30` (validação) | 1.004,58 | 2,43 | 0,48 | 1.001,67 | 14 / 9 | true | false |
| aporte de `$CDI110`, `referenceDate=2026-11-30` | **1.005,04** | **2,67** | **0,53** | **1.001,84** | 14 / 9 | true | false |
| aporte de `$LCI100`, `referenceDate=2026-11-30` | 1.004,58 | 2,43 | **0,00** (`taxRate 0`) | **1.002,15** | 14 / 9 | true | false |
| aporte de `$PRE` em 16/11, `referenceDate=2026-11-30` | **1.003,41** | **1,81** | **0,36** | **1.001,24** | 14 / 9 | **false** | false |
| aporte de `$LCAPRE`, `referenceDate=2026-11-30` | **1.003,08** | 0,00 | 0,00 | **1.003,08** | 14 / 9 | false | false |
| aporte de `$IPCA`, `referenceDate=2026-11-03` | **1.006,27** | 0 | **1,41** | **1.004,86** | 33 / 21 | **false** | false |
| aporte de `$IPCA`, `referenceDate=2026-11-04` | **1.006,71** | 0 | **1,51** | **1.005,20** | 34 / 22 | **true** | false |

O CDB 110% usa `(1 + 0,00050788 × 1,1)^9` truncado a cada dia: fator 1,00503926. O IPCA+ em 04/11
usa `1,0014 × 1,0038^(1/19) × 1,06^(22/252)`. Se a taxa de 26/11 já estiver publicada e for diferente
de 0,050788, recalcule as linhas de CDI com `cdi.mjs`.

- BUG (alta): qualquer valor diferente do recálculo; `isProjected` verdadeiro no PreFixed (RN-12,
  "Um PreFixed nunca é projetado"); IPCA+ em 03/11 projetado (o mês de hoje não tem dia útil no
  intervalo, mudança de comportamento 1).

#### QA12-A14 — Data antes de todos os aportes, entre aportes e no dia do aporte [P1, A, 10 min]

```bash
for r in 0001-01-01 2000-01-01 2024-12-31 2025-03-31 2025-04-01 2025-04-02 2026-11-15 2026-11-16 2026-12-14 2026-12-15; do
  printf '%s ' $r; curl -s "$B/api/assets/$PRE/projection?referenceDate=$r" | node "$QAW/js/soma.mjs" | tr '\n' ' ' | head -c 300; echo
  curl -s "$B/api/assets/$PRE/projection?referenceDate=$r" | head -c 300; echo; done
```

Esperado (RN-14): `positionCount` 0, totais zero, `positions []` e `referenceDate` igual à pedida em
0001-01-01, 2000-01-01, 2024-12-31 e 2025-03-31 (antes da emissão também: não é 400); dois itens com
rendimento zero em 2025-04-01 (aporte no próprio dia entra); dois em 2025-04-02; três em 2026-11-16
(o de 16/11 com rendimento zero); três em 2026-12-14; quatro em 2026-12-15. `soma.mjs` com `OK` em
todas as linhas.

- BUG (alta): aporte posterior à data incluído; total diferente da soma.
- BUG (média): 400 em data antes dos aportes ou da emissão (a RN-14 diz 200 com zeros).

#### QA12-A15 — Aporte posterior à data padrão [P1, A, 10 min]

O aporte de R$ 500 de `$PRE` em 2026-12-15 é posterior a D.

```bash
get "/api/assets/$PRE/projection" > "$QA/a/a15.txt"; cat "$QA/a/a15.txt"
P500=<positionId do aporte de 2026-12-15>
get "/api/positions/$P500/projection"                               # documentado: 400 "Informe referenceDate"
get "/api/positions/$P500/projection?referenceDate=2026-12-15"
node "$QAW/js/coerencia.mjs" "$B" "$PRE"
```

Esperado (RN-14; ESPECIFICACAO, "Endpoints"): sem data, raiz com `referenceDate` = D e quatro
itens; o de 2026-12-15 com `referenceDate 2026-12-15`, rendimento, IOF e IR zero,
`isProjected false`; os outros com `referenceDate` = D. A projeção da posição sem data responde 400
pedindo `referenceDate` (comportamento documentado); com a data do item, igual ao item campo a campo.

- BUG (alta): o aporte futuro fora dos itens ou dos totais; item com `referenceDate` = D (projeção
  "para trás").
- BUG (média): `coerencia.mjs` com divergência.

#### QA12-A16 — Título vencido e vencimento na fronteira [P1, A, 15 min]

```bash
VENC=$(titulo "QA-F12A VENCIDO PRE" Cdb PreFixed 10 2024-01-01 2025-01-02); aporte $VENC 1000 2024-01-02
for r in "" "?referenceDate=2025-01-02" "?referenceDate=2030-06-03" "?referenceDate=2050-12-31"; do
  curl -s "$B/api/assets/$VENC/projection$r" | head -c 600; echo; done
node "$QAW/js/coerencia.mjs" "$B" "$VENC"
```

Esperado (RN-02; ESPECIFICACAO, "Numa posição vencida, `referenceDate` continua sendo a data
pedida"): mesmos valores nas quatro chamadas (os do vencimento), `referenceDate` da raiz e do item
igual à pedida (ou D), `isProjected false`, `elapsedDays` 366.

- BUG (alta): valores diferentes entre as chamadas; `isProjected true` num título vencido antes de L.

#### QA12-A17 — Coerência campo a campo entre os três endpoints [P1, A, 20 min]

Para cada título da Parte A e para as datas: sem data, D, P, T, 2026-11-30, 2026-12-31 e
2028-01-03:

```bash
for t in $PRE $CDI100 $CDI110 $LCI100 $LCAPRE $IPCA $VENC; do for r in "" <D> <P> <T> 2026-11-30 2026-12-31 2028-01-03; do
  echo "== $t $r"; node "$QAW/js/coerencia.mjs" "$B" $t $r
  curl -s "$B/api/assets/$t/projection${r:+?referenceDate=$r}" | node "$QAW/js/soma.mjs"; done; done 2>&1 | tee "$QA/a/a17.txt"
grep -cE 'DIVERGE|ausente|status [45]' "$QA/a/a17.txt"
```

Esperado (D-11: o mesmo projetor; RN-07; RN-14): zero divergências. Linhas com "resumo
indisponível" (o resumo da carteira real deu 400 por dado de outro QA, QACF-R2) não são achado desta
fase: anote a mensagem e confira se a projeção por título do mesmo dia deu 200.

- BUG (alta): qualquer campo diferente entre o item do título, a projeção da posição e o item do
  resumo, inclusive `isProjected`, `isEstimated` e `referenceDate`; soma diferente do total.
- RISCO (média): o resumo dar 400 por uma posição de outro título enquanto a projeção por título
  responde 200. É a divisão de responsabilidade já registrada (QACF-R2), mas a projeção por título
  passa a ser o caminho que o front tem para mostrar o resto da carteira.

#### QA12-A18 — Soma das projeções de todos os títulos contra o resumo [P1, A, 15 min]

```bash
for r in "" <D> <T> 2026-11-30 2026-12-31; do echo "== $r"; node "$QAW/js/somatitulos.mjs" "$B" $r; done | tee "$QA/a/a18.txt"
```

Esperado (D-11, RN-07): os sete totais iguais; `isProjected` da raiz igual ao OR dos itens e ao OR
dos títulos (D-6). Se o resumo der 400, o título culpado (o da mensagem) aparece entre os títulos com
falha, com a mesma mensagem, e todos os outros respondem 200.

- BUG (alta): soma diferente; raiz `isProjected` diferente do OR dos itens.
- BUG (média): resumo 400 citando um título cuja projeção por título dá 200 na mesma data.

#### QA12-A19 — Ordem dos aportes [P2, A, 10 min]

```bash
ORD=$(titulo "QA-F12A ORDEM PRE12" Cdb PreFixed 12 2024-01-01 2029-01-02)
for d in 2026-03-02 2025-01-06 2026-06-01 2025-01-06 2026-11-16 2024-07-01 2025-01-06; do aporte $ORD 100 $d; done
for r in "" 2026-06-01 2025-01-06; do curl -s "$B/api/assets/$ORD/projection${r:+?referenceDate=$r}" | node "$QAW/js/soma.mjs" | grep ordem; done
node "$QAW/js/coerencia.mjs" "$B" "$ORD"; for k in 1 2 3; do curl -s "$B/api/assets/$ORD/projection" | node -e "console.log(JSON.parse(require('fs').readFileSync(0,'utf8')).data.positions.map(p=>p.positionId.slice(0,8)+'@'+p.applicationDate).join(' '))"; done
```

Esperado (D-12, RN-14): do mais recente para o mais antigo; os três de 2025-01-06 juntos (sem ordem
garantida entre eles). Registre se a ordem entre os do mesmo dia muda entre chamadas.

- BUG (média): ordem fora de data; ordem do título diferente da do resumo fora dos empates.
- RISCO (baixa): ordem dos empates variando entre chamadas idênticas (lista "piscando" no front). A
  spec permite; só registrar.

#### QA12-A20 — Título removido, taxa alterada e aporte novo [P2, A, 10 min]

```bash
DEL=$(titulo "QA-F12A REMOVIDO" Cdb PreFixed 10 2026-01-01 2028-01-03)
get "/api/assets/$DEL/projection"; curl -s -X DELETE "$B/api/assets/$DEL"; echo
get "/api/assets/$DEL/projection"; get "/api/assets/$DEL/projection?referenceDate=2051-01-01"
curl -s -X DELETE "$B/api/assets/$CDI110"; echo                       # tem aporte: rejeitado (RN-06)
get "/api/assets/$CDI110/projection?referenceDate=2026-11-30"         # continua funcionando
curl -s -X PUT -H 'Content-Type: application/json' -d '{"name":"QA-F12A CDI110 alterado","issuer":"QA-F12A Banco","rate":120,"issueDate":"2026-01-01","maturityDate":"2030-01-02"}' "$B/api/assets/$CDI110"; echo
node "$QAW/js/coerencia.mjs" "$B" "$CDI110" 2026-11-30                 # taxa nova nos três endpoints
aporte $CDI110 1000 2026-11-23; node "$QAW/js/coerencia.mjs" "$B" "$CDI110" 2026-11-30
```

Esperado: 200 com zeros antes da remoção, 404 depois (com e sem a data de 2051); remoção com aporte
recusada; depois do PUT (RN-06 permite alterar `Rate`), os três endpoints com o bruto de 120% do CDI;
o aporte novo aparece no título e no resumo na hora.

- BUG (alta): título removido respondendo 200; endpoints com taxas diferentes depois do PUT.

#### QA12-A21 — Matriz de `isProjected` e `isEstimated` com o hoje real [P1, A, 20 min]

Aportes de 16/11 (CDI100, CDI110, LCI100, PRE, LCAPRE) e de 01/10 (IPCA). Para cada data, a
projeção da posição e o item no título:

| `referenceDate` | CDI (L = 25/11) | CDI (L = 26/11) | PreFixed | IPCA+ (M = 10/2026) |
|---|---|---|---|---|
| sem data (D) | false | false | false | **true** (novembro no intervalo, mudança 1) |
| 2026-11-25 | false | false | false | true |
| P = 2026-11-26 | false (só 25/11, publicado) | false | false | true |
| T = 2026-11-27 | **true** (26/11 projetado) | false | false | true |
| 2026-11-30 | true | true | false | true |
| 2026-12-31 | true | true | false | true |
| 2027-11-26 | true | true | false | true |
| 2029-11-27 | true | true | false | true |

`isEstimated` falso em todas as linhas (outubro publicado; não há mês encerrado sem divulgação).

```bash
for r in "" 2026-11-25 2026-11-26 2026-11-27 2026-11-30 2026-12-31 2027-11-26 2029-11-27; do
  for t in $CDI100 $CDI110 $LCI100 $PRE $LCAPRE $IPCA; do
    printf '%-11s %s ' "${r:-padrão}" "${t:0:8}"
    curl -s "$B/api/assets/$t/projection${r:+?referenceDate=$r}" | node -e "const d=JSON.parse(require('fs').readFileSync(0,'utf8')).data; console.log(d ? d.positions.map(p=>p.applicationDate+':P'+(+p.isProjected)+'E'+(+p.isEstimated)+':'+p.grossAmount).join(' ') : 'erro')"
  done; done
```

Esperado: a tabela acima (RN-12; README, "Projeção com data futura"; a data padrão nunca projeta
CDI). Os itens de aporte posterior à data pedida não aparecem (data informada).

- BUG (alta): qualquer célula diferente da tabela (com o L da execução).
- BUG (alta): 400 em T ou em 2026-11-30 com L = 25/11: a última cotação é de dois dias úteis antes de
  hoje, e a mudança 2 diz que projeta.

#### QA12-A22 — Vencimento na fronteira da projeção [P1, A, 15 min]

```bash
for v in 2026-11-26 2026-11-27 2026-11-28 2026-11-30; do
  t=$(titulo "QA-F12A VENC CDI $v" Cdb Cdi 100 2026-11-01 $v); aporte $t 1000 2026-11-16
  printf 'CDI venc %s: ' $v; curl -s "$B/api/assets/$t/projection?referenceDate=2027-01-15" | head -c 400; echo; done
for v in 2026-10-31 2026-11-02 2026-11-03 2026-11-04; do
  t=$(titulo "QA-F12A VENC IPCA $v" Cdb Ipca 6 2026-09-01 $v); aporte $t 1000 2026-10-01
  printf 'IPCA venc %s: ' $v; curl -s "$B/api/assets/$t/projection?referenceDate=2027-01-15" | head -c 400; echo; done
```

Esperado (RN-02 corta antes da RN-12):

| Título | Vencimento | `isProjected` (L = 25/11) | `isProjected` (L = 26/11) | Valores |
|---|---|---|---|---|
| CDI | 2026-11-26 | false | false | os do vencimento |
| CDI | 2026-11-27 | **true** (26/11 projetado) | false | |
| CDI | 2026-11-28 (sábado) | true | true (27/11 projetado) | iguais aos de 2026-11-30 |
| CDI | 2026-11-30 | true | true | |
| IPCA+ | 2026-10-31 | false | false | outubro inteiro: 1.006,27 |
| IPCA+ | 2026-11-02 (feriado) | **false** (nenhum dia útil de novembro antes de 02/11) | false | 1.006,27 |
| IPCA+ | 2026-11-03 | **false** (01/11 domingo, 02/11 feriado) | false | 1.006,27 |
| IPCA+ | 2026-11-04 | **true** (03/11) | true | 1.006,71 |

O IPCA+ com vencimento em 31/10 tem 30 dias corridos (IR 22,5% sobre 6,27 = 1,41); os outros, 32 a
34. A `referenceDate` da resposta continua 2027-01-15.

- BUG (alta): título vencido marcado como projetado, ou não marcado quando o intervalo até o
  vencimento tem dia projetado; IPCA+ com mês de zero dias úteis no intervalo exigindo a janela ou
  marcando `isProjected`.

#### QA12-A23 — IPCA+ no mês de hoje, mudança de comportamento 1 [P1, A, 10 min]

Já coberta em parte pela A13 e A21. Confira a fronteira com mais aportes no mesmo título:

```bash
aporte $IPCA 1000 2026-11-03; aporte $IPCA 1000 2026-11-26
for r in "" 2026-11-03 2026-11-04 <D> 2026-12-01 2027-01-04; do curl -s "$B/api/assets/$IPCA/projection${r:+?referenceDate=$r}" | node "$QAW/js/soma.mjs" | head -3; curl -s "$B/api/assets/$IPCA/projection${r:+?referenceDate=$r}" | node -e "const d=JSON.parse(require('fs').readFileSync(0,'utf8')).data; console.log(d.positions.map(p=>p.applicationDate+'->'+p.referenceDate+' P'+(+p.isProjected)+' '+p.grossAmount).join(' | '))"; done
```

Esperado: aporte de 03/11 com referência 03/11 (rendimento zero, `isProjected false`); com
referência 04/11, projetado; o aporte de 26/11 sem data entra com `referenceDate` igual a D se D ≥
26/11 (ou à própria data, se D < 26/11), sem exigir cotação; em 2027-01-04, os três projetados
(novembro e dezembro com μ). Recalcule o bruto do aporte de 01/10 em 2027-01-04 com
`1000 × 1,0014 × 1,0038 × 1,0038 × 1,06^(DU/252)` (novembro e dezembro inteiros) e `DU` da resposta.

- BUG (alta): valor diferente do recálculo (com μ de 2 casas; com μ sem arredondar o bruto muda);
  item com rendimento zero marcado como projetado.

#### QA12-A24 — Resumo com carteira mista [P1, A, 10 min]

O A18 já compara a raiz com o OR dos itens. Aqui, confira o contrato da raiz e dos itens:

```bash
for r in "" <P> <T> 2026-11-30; do curl -s "$B/api/portfolio/summary${r:+?referenceDate=$r}" > "$QA/a/a24-${r:-padrao}.json"
  node -e "const d=JSON.parse(require('fs').readFileSync(0,'utf8')).data; if(!d){console.log('erro');process.exit()} const k=Object.keys(d); console.log(k.join(','), '| raiz isProjected', d.isProjected, '| itens projetados', d.positions.filter(p=>p.projection.isProjected).length, '| estimados', d.positions.filter(p=>p.projection.isEstimated).length, '| isEstimated na raiz?', 'isEstimated' in d)" < "$QA/a/a24-${r:-padrao}.json"; done
curl -s "$B/api/assets/$CDI100/projection" | chaves        # sem isProjected e sem byAssetType na raiz (A-1 recusada, D-11)
```

Esperado (D-6; spec, "Contrato resultante"): raiz do resumo com `isProjected` depois de
`positions`, sem `isEstimated` (A-2 do contrato para o front); raiz da projeção por título com
exatamente `assetId`, os seis totais, `positionCount`, `referenceDate` e `positions`.

- BUG (média): `isProjected` ou `byAssetType` na raiz da projeção por título; `isEstimated` na raiz do
  resumo; `isProjected` ausente no resumo com carteira vazia ou com todos os aportes fora.

#### QA12-A25 — Projeções longas e as faixas de IR com dias projetados [P2, A, 20 min]

Aporte de `$CDI100` em 16/11 (`$P2`) e o de `$PRE` em 16/11:

| `referenceDate` | Corridos | Alíquota de IR (RN-03) |
|---|---|---|
| 2027-05-15 (sábado) | 180 | 22,5 |
| 2027-05-16 (domingo) | 181 | 20,0 |
| 2027-11-11 | 360 | 20,0 |
| 2027-11-12 | 361 | 17,5 |
| 2028-11-05 (domingo) | 720 | 17,5 |
| 2028-11-06 | 721 | 15,0 |

```bash
for r in 2027-05-14 2027-05-15 2027-05-16 2027-05-17 2027-11-11 2027-11-12 2028-11-05 2028-11-06 2036-11-17 2050-12-30; do
  curl -s -w " [%{time_total}s]" "$B/api/positions/$P2/projection?referenceDate=$r"; echo; done
# recálculo do bruto: publicados de 16/11 até L, completados até businessDays com a taxa de L
curl -s "$B/api/indexes/Cdi?from=2026-11-16&to=<L>" | node "$QAW/js/cdi.mjs" 100 <businessDays da resposta>
```

Esperado: `taxRate` da tabela; bruto igual ao do `cdi.mjs` em cada data; o bruto de 15/05 e de 16/05
iguais entre si e iguais ao de 17/05 (os dias úteis são os mesmos), mudando só IR e líquido;
`isProjected true`; `businessDays` igual ao do aporte de `$PRE` nas mesmas datas; tempo abaixo de
1 s mesmo em 2050.

- BUG (alta): alíquota errada; bruto diferente do recálculo; `businessDays` diferente entre CDI e
  PreFixed nas mesmas datas.

#### QA12-A26 — Regressão: chaves e tipos das quatro respostas [P1, A, 10 min]

```bash
curl -s "$B/api/positions/$P2/projection?referenceDate=2026-11-30" | chaves
curl -s "$B/api/portfolio/summary?referenceDate=<D>" | chaves
curl -s "$B/api/assets/$CDI100/projection" | chaves
curl -s "$B/api/indexes/Cdi/accumulated?from=2026-09-01&to=2026-10-01" | chaves
curl -s "$B/api/indexes/Cdi?from=2026-09-14&to=2026-09-15"; echo
```

Esperado (ESPECIFICACAO, "nenhum campo existente muda de nome ou de tipo"; spec, "Contrato
resultante"):

- projeção: `positionId, assetId, investedAmount, grossAmount, grossYield, taxRate, taxAmount,
  netAmount, elapsedDays, businessDays, iofRate, iofAmount, isEstimated, referenceDate,
  applicationDate, isProjected`, nessa ordem, com `isProjected` booleano;
- resumo: as 8 chaves antigas, `referenceDate`, `positions`, `isProjected`; cada item com só
  `projection` e `asset`;
- projeção por título: `assetId`, os seis totais com os nomes do resumo, `positionCount`,
  `referenceDate`, `positions`;
- acumulado: `indexType, from, to, effectiveTo, accumulatedPercentage`;
- listagem de cotações igual à do README.

- BUG (média): chave a mais, a menos, renomeada ou com outro tipo.

#### QA12-A27 — Regressão: mensagens e status antigos [P2, A, 10 min]

```bash
get "/api/positions/$P2/projection?referenceDate=2026-11-15"          # "A data de referência não pode ser anterior à data de aporte."
get "/api/positions/00000000-0000-0000-0000-000000000000/projection"   # 404 "Posição com id ... não encontrada."
get "/api/positions/xyz/projection"                                    # 400 id inválido
get "/api/indexes/Cdi?from=2026-10-01&to=2026-09-01"                   # 400 data inicial posterior
get "/api/indexes/PreFixed?from=2026-09-14&to=2026-09-15"              # 400
get "/api/indexes/1?from=2026-09-14&to=2026-09-15"                     # 400
get "/api/positions/$P2/projection?referenceDate=2051-01-01"           # vencimento 2030: 200 (RN-02)
get "/api/foo"                                                         # 404 no envelope
get "/api/portfolio/summary?referenceDate=2000-01-01"                  # 200, zeros, isProjected false
```

Esperado: as mensagens e os status do contrato para o front e da F7, sem mudança de texto.

- BUG (média): mensagem antiga alterada (o front pode comparar texto); status diferente.

#### QA12-A28 — Swagger [P2, A, 15 min]

```bash
curl -s "$B/swagger/v1/swagger.json" > "$QA/a/swagger.json"
node -e "
const s=JSON.parse(require('fs').readFileSync(process.argv[1],'utf8'));
for (const [p,o] of Object.entries(s.paths)) if (/accumulated|\/projection/.test(p)) for (const [m,op] of Object.entries(o))
  console.log(m.toUpperCase(), p, JSON.stringify(op.parameters.map(x=>[x.name,x.in,x.required||false,x.schema.type+'/'+(x.schema.format||'')])), Object.keys(op.responses).join(','));
for (const n of ['IndexAccumulationResponse','AssetProjectionResponse','PositionProjectionResponse','PortfolioSummaryResponse'])
  console.log(n, JSON.stringify(Object.fromEntries(Object.entries(s.components.schemas[n].properties).map(([k,v])=>[k,(v.type||v.\$ref)+(v.format?'/'+v.format:'')]))));
" "$QAW/a/swagger.json"
curl -s -o /dev/null -w '%{http_code}\n' "$B/swagger/index.html"
```

Esperado (critério 7 do ROADMAP; spec P3 e P4b): as duas rotas com `indexType`, `from` e `to`
obrigatórios (`string/date` nas datas) e `id` obrigatório, `referenceDate` opcional; respostas 200 e
400 declaradas nas duas e 404 na projeção por título; schemas com os campos da A26 e tipos
coerentes (`accumulatedPercentage` `number/double`, datas `string/date`, `isProjected` `boolean`).
Abra o Swagger UI se houver navegador disponível e execute as duas rotas pelo "Try it out".

- BUG (média): parâmetro obrigatório marcado opcional ou o contrário; 404 não declarado na projeção
  por título; schema diferente da resposta real.
- RISCO (baixa): 404 declarado no acumulado, que não tem 404 (documentação enganosa).

#### QA12-A29 — `.http` e exemplos do README [P2, A, 20 min]

Rode as requisições novas do `FixedIncome.Api.http` pela porta 8080, trocando os nomes de título
por `QA-F12A HTTP …` (o arquivo usa "CDB Banco XYZ", sem o prefixo do QA): acumulado de CDI e de
IPCA, projeção por título com e sem data, "Projeção de aporte com data futura (isProjected true…)"
com `referenceDate=2026-12-31`. E os exemplos do README: acumulado de setembro, projeção por título
do caso 29 (já na A13), o exemplo da projeção da posição.

Esperado: cada resposta faz o que o comentário do `.http` e o texto do README dizem (por exemplo,
`to=2026-09-01` no IPCA inclui setembro; a projeção de 2026-12-31 do CDB aportado em 2026-06-01 sai
com `isProjected true` se a importação estiver em dia).

- BUG (baixa): exemplo que não reproduz, comentário que descreve outro comportamento, rota do `.http`
  que dá 400 no caminho feliz.
- RISCO (baixa): o `.http` cria títulos sem prefixo de teste num banco compartilhado (registre só se
  o README não avisar).

#### QA12-A30 — Deriva de L durante a sessão [P1, A, 5 min, no fim]

Repita o passo 5 da preparação e a foto do passo 2. Se L mudou, diga em quais casos (pela hora
registrada) valia cada L, e repita só a A21 com o L novo. A foto de `index_rate` deve mostrar só
linhas novas depois do L inicial: o `md5` das linhas até o L inicial igual ao de antes.

- BUG (alta): cotação anterior ao L inicial alterada ou apagada (nada nesta parte escreve em
  `index_rate`; só a importação poderia).

### Parte B — Pilha isolada, cotações controladas (`QA-F12B`)

`B=http://localhost:28112`. Todos os cenários começam e terminam com `restaura`. Os esperados
assumem T = 2026-11-27 (sexta) e as cotações copiadas com L = 25/11; para outro T, use a regra
geral de cada cenário.

#### QA12-B00 — Linha de base e carteira da Parte B [P1, B, 15 min]

```bash
B=http://localhost:28112
BVAZIO=$(titulo "QA-F12B VAZIO" Cdb PreFixed 10 2026-01-01 2028-01-03)
BCDI=$(titulo "QA-F12B CDI100" Cdb Cdi 100 2026-01-01 2030-01-02); BP1=$(aporte $BCDI 1000 2026-11-16)
BPRE=$(titulo "QA-F12B PRE10" Cdb PreFixed 10 2026-01-01 2030-01-02); BP2=$(aporte $BPRE 1000 2026-11-16)
BIPCA=$(titulo "QA-F12B IPCA6" Cdb Ipca 6 2026-01-01 2028-01-03); BP3=$(aporte $BIPCA 1000 2026-10-01)
get "/api/assets/$BVAZIO/projection"                         # D da Parte B
get "/api/positions/$BP1/projection?referenceDate=2026-11-30" # 1004.58, isProjected true (igual à Parte A)
get "/api/indexes/Cdi/accumulated?from=2026-09-01&to=2026-10-01"
```

Esperado: as mesmas respostas da Parte A para os mesmos dados (as cotações são cópia). Diferença
aqui invalida a Parte B: pare e registre.

#### QA12-B01 — Mudança de comportamento 2 com L controlado [P1, B, 25 min]

Regra (RN-12, D-2): dia útil sem cotação depois de L é falha de importação se o dia útil seguinte a
ele for anterior a T; senão é projetado. Com T = 27/11 (P = 26/11):

| L | Como montar | `referenceDate=2026-11-30` | `=2026-11-27` (T) | `=P` | sem data (D) |
|---|---|---|---|---|---|
| 26/11 (T−1) | `sintetico 2026-11-26 0.050788` (se não houver) | 200 true | 200 false | 200 false | D = 27/11, false |
| 25/11 (T−2) | cópia como está | 200 true | 200 true | 200 false | D = 26/11, false |
| 24/11 (T−3) | `cdi_ate 2026-11-24` | **400** "Cotação de CDI ausente para 2026-11-25. A importação de cotações pode estar desligada (IndexRatesImport:Enabled) ou ainda não ter rodado." | 400, mesma data | 400, mesma data (o intervalo `[16/11, 26/11)` inclui 25/11) | D = 25/11, false |
| 19/11 (antes do feriado) | `cdi_ate 2026-11-19` | 400 citando 2026-11-23 | 400 | 400 | D = 23/11, false |

Na linha de L = 24/11, só `referenceDate=2026-11-25` (= D) responde 200 com `isProjected false`.

Para cada L, rode as quatro colunas na projeção da posição (`$BP1`), na projeção por título
(`$BCDI`) e no resumo:

```bash
for r in 2026-11-30 2026-11-27 2026-11-26 2026-11-25 ""; do
  for u in "/api/positions/$BP1/projection" "/api/assets/$BCDI/projection" "/api/portfolio/summary"; do
    printf '%-11s %-45.45s ' "${r:-padrão}" "$u"; curl -s "$B$u${r:+?referenceDate=$r}" | head -c 260; echo; done; done
get "/api/indexes/Cdi/accumulated?from=2026-11-16&to=2026-11-30"     # o acumulado não olha T: 200 com effectiveTo = D
restaura
```

Esperado: a tabela; no resumo e na projeção por título, o 400 cita a posição e o título
("Posição … (título 'QA-F12B CDI100'): Cotação de CDI ausente para …", D-11) e o aviso da
importação; o PreFixed sozinho (`$BPRE`) responde 200 em todas as linhas; o acumulado responde 200 com
o corte em D mesmo com a importação parada (RN-13 não depende de T).

- BUG (alta): 200 onde a tabela diz 400 (projeção escondendo a importação parada); 400 onde diz 200
  (a mudança 2 não funciona); data citada diferente da primeira sem cotação.
- BUG (média): mensagem do resumo ou do título sem a posição e o título, ou sem o aviso do R-10.

#### QA12-B02 — Feriado logo depois da última cotação [P1, B, 15 min]

`cdi_ate 2026-11-19` (L = 19/11, quinta; 20/11 feriado; D = 23/11). Reproduz ao vivo a tabela do
caso 15 da spec e o caso 3a:

| Chamada | Esperado |
|---|---|
| `Cdi/accumulated?from=2026-11-16&to=2026-11-20` (feriado) | 200, `effectiveTo 2026-11-20`, `0.203307` |
| `…&to=2026-11-21` (sábado) | 200, `effectiveTo 2026-11-21`, `0.203307` |
| `…&to=2026-11-23` | 200, `effectiveTo 2026-11-23`, `0.203307` |
| `…&to=2026-11-24` e `…&to=2026-11-26` | 200, `effectiveTo 2026-11-23` (corte), `0.203307` |
| `…&to=2051-01-02` | 200, `effectiveTo 2026-11-23`, sem erro |
| `Cdi/accumulated?from=2026-11-23&to=2026-11-26` | 400 período sem cotação (D-9) |
| `Cdi/accumulated?from=2026-11-24&to=2026-12-10` | 400 com a **mensagem de período sem cotação**, não a do calendário (O-1 da validação) |
| `positions/$BP1/projection` sem data | 200, `referenceDate 2026-11-23`, 1.002,03, IOF 1,54, IR 0,11, líquido 1.000,38, `isProjected false` (caso 3a) |
| `positions/$BP1/projection?referenceDate=2026-11-24` | 400 citando 2026-11-23 (com T = 27/11, 23/11 fica antes de P; o caso 3b da spec só projeta com T ≤ 24/11) |

- BUG (alta): percentual ou `effectiveTo` diferente; o acumulado projetando 23, 24 ou 25/11
  (`0.356058`).
- BUG (baixa): mensagem do calendário no lugar da de período sem cotação (sem efeito no status, mas
  engana o front).

`restaura`.

#### QA12-B03 — Lacunas antes da última cotação [P1, B, 20 min]

| Montagem | Chamada | Esperado |
|---|---|---|
| `tira Cdi 2026-11-18` | `positions/$BP1/projection?referenceDate=2026-11-19` | 400 "Cotação de CDI ausente para 2026-11-18." com o aviso (D-1: período termina logo depois da lacuna, e a série carregada acabaria em 17/11 sem a última cotação do banco) |
| idem | `…?referenceDate=2026-11-18` | 200 (só 16 e 17/11), `isProjected false` |
| idem | `Cdi/accumulated?from=2026-11-16&to=2026-11-19` | 400 citando 2026-11-18 |
| `restaura; tira Cdi 2026-11-24` | `positions/$BP1/projection?referenceDate=2026-11-30` | 400 citando 2026-11-24 (lacuna antes de L, não projeção) |
| `restaura; tira Cdi 2023-05-10` | `positions/$BP1/projection?referenceDate=2026-11-30` | 200, igual ao B00 (lacuna fora do período não importa) |
| idem | `Cdi/accumulated?from=2023-05-01&to=2023-06-01` | 400 citando 2023-05-10 |
| idem, com `BOLD=$(aporte $BCDI 1000 2023-05-02)` | `assets/$BCDI/projection?referenceDate=2026-11-30` e o resumo | 400 com a posição antiga e o título; o aporte de 16/11 sozinho continua 200 na projeção da posição |

- BUG (alta): 200 com lacuna dentro do intervalo (a mais importante: dia 18/11 virar "projetado");
  400 por lacuna fora do intervalo.

`restaura` (e anote o id de `$BOLD`).

#### QA12-B04 — CDI antes da primeira cotação importada [P3, B, 10 min]

`sqlb "delete from index_rate where index_type='Cdi' and reference_date < '2020-02-03'"`.

| Chamada | Esperado |
|---|---|
| `Cdi/accumulated?from=2020-01-02&to=2020-03-02` | 400 "Cotação de CDI ausente para 2020-01-02." (dia útil sem cotação antes de L é lacuna, RN-13) |
| `Cdi/accumulated?from=2020-02-03&to=2020-03-02` | 200 |
| aporte em 2020-01-02 num CDI (`titulo "QA-F12B CDI ANTIGO" Cdb Cdi 100 2020-01-01 2030-01-02`), projeção em 2020-03-02 | 400 citando 2020-01-02 |

- RISCO (baixa): a mensagem "ausente" para um período que nunca foi importado não diz que a série
  começa depois; registre o texto.

`restaura`.

#### QA12-B05 — Banco sem nenhuma cotação de CDI [P2, B, 15 min]

`sqlb "delete from index_rate where index_type='Cdi'"`.

| Chamada | Esperado |
|---|---|
| `assets/$BVAZIO/projection` e `assets/$BPRE/projection` sem data | 200, `referenceDate` = T de Brasília (ESPECIFICACAO, "Endpoints") |
| `positions/$BP2/projection` (PreFixed) sem data | 200 com `referenceDate` T |
| `positions/$BP1/projection` (CDI) com e sem data, inclusive 2026-12-31 | 400 de cotação ausente (RN-12: "sem nenhuma cotação de CDI no banco: falha") |
| `positions/$BP3/projection` (IPCA+) sem data | 200, `isProjected true` (a data padrão é T, novembro no intervalo) |
| `Cdi/accumulated?from=2026-09-01&to=2026-10-01` | 400 dizendo que não há CDI |
| resumo sem data | 400 por causa do CDI, citando a posição e o título |

- BUG (alta): `referenceDate` com o dia de UTC (execução entre 21h e 24h, ver B12); 500.
- RISCO (média): mensagem do acumulado ou da projeção que não diga que a série está vazia ou que a
  importação pode estar desligada.

`restaura`.

#### QA12-B06 — IPCA estimado e projetado juntos, ao vivo [P1, B, 15 min]

`tira Ipca 2026-10-01` (M = 09/2026; outubro encerrado e não divulgado; novembro é o mês de hoje).
Vale com T em novembro de 2026.

| Chamada (aporte `$BP3`, R$ 1.000 em 01/10) | Esperado |
|---|---|
| `referenceDate=2026-12-15` | **caso 6 da spec**: bruto 1.025,42, rendimento 25,42, IOF 0, IR 22,5% 5,72, líquido 1.019,70, 75 corridos, 50 úteis, `isEstimated true`, `isProjected true` |
| sem data (D = 26/11) | bruto **1.020,03** (`1,0082 × 1,0037^(16/19) × 1,06^(37/252)`), IR 4,51, líquido 1.015,52, `isEstimated true`, `isProjected true` |
| `referenceDate=2026-11-03` | bruto `1000 × 1,0082 × 1,06^(21/252)`, `isEstimated true`, `isProjected false` |
| `referenceDate=2026-10-30` | `isEstimated true`, `isProjected false` |
| `Ipca/accumulated?from=2026-09-01&to=2026-10-20` | 200, `effectiveTo 2026-09-30`, `0.820000` (o mês estimado não entra, teste 21) |
| `node ipca.mjs --media` sobre a listagem até 09/2026 | μ = 0,37 |

Com T em dezembro de 2026: tire também `Ipca 2026-11-01` e confira o caso 6-bis (1.030,01, IR 6,75,
líquido 1.023,26, as duas marcas verdadeiras).

- BUG (alta): qualquer valor diferente; uma das duas marcas falsa; o mês encerrado usando μ em vez
  da última variação (ou o contrário).

`restaura`.

#### QA12-B07 — Janela de 12 meses incompleta [P1, B, 25 min]

Janela com M = 10/2026: de 2025-11 a 2026-10. Aporte `$BP3` em 01/10/2026.

| Montagem | Chamada | Esperado |
|---|---|---|
| a. `tira Ipca 2025-11-01` | `positions/$BP3/projection` sem data | 400 "A projeção futura do IPCA exige os 12 meses de 2025-11 a 2026-10; há 11, falta 2025-11." (D-4) |
| a | `…?referenceDate=2026-11-03` | 200, 1.006,27, `isProjected false` (sem mês projetado, a janela não é exigida) |
| a | `…?referenceDate=2026-11-04` e `2027-06-01` | 400, mesma mensagem |
| a | `assets/$BIPCA/projection` sem data e resumo sem data | 400 com a mensagem da D-4 (mudança 1: "Antes respondia 200"); no resumo, identificando a posição |
| a | resumo com `referenceDate=2026-11-03` | 200 |
| a | `Ipca/accumulated?from=2026-01-15&to=2026-09-20` | 200, `3.951320` (o acumulado não usa a janela) |
| a | `Ipca/accumulated?from=2025-10-15&to=2025-12-31` | 400 de lacuna citando 2025-11 |
| b. `restaura; tira Ipca 2025-11-01; tira Ipca 2026-03-01` | `positions/$BP3/projection` sem data | 400 "… há 10, falta **2025-11**." (o primeiro ausente; é a lacuna de teste O-1: a mutação sobrevivente citaria 2026-03) |
| c. `restaura; tira Ipca 2026-03-01` | `positions/$BP3/projection` sem data | 400 "… há 11, falta 2026-03." |
| c | IPCA+ com aporte em 2026-02-02 (`BIPCA2`), sem data | 400; registre qual mensagem sai, a de lacuna de março (RN-10) ou a da janela (D-4). As duas são 400; a de lacuna diz ao usuário o que importar |
| d. `restaura; sqlb "delete from index_rate where index_type='Ipca' and reference_date < '2025-11-01'"` | `positions/$BP3/projection` sem data | 200, `isProjected true` (exatamente 12 meses bastam), μ = 0,38 |
| d | `Ipca/accumulated?from=2025-01-10&to=2025-12-20` | 400 de lacuna citando 2025-01 |
| e. `restaura; sqlb "delete from index_rate where index_type='Ipca'"` | `positions/$BP3/projection` sem data | 400 com mensagem que diga que não há IPCA publicado |
| e | `Ipca/accumulated?from=2026-01-01&to=2026-10-31` | 400 "banco sem IPCA" |
| e | `positions/$BP2/projection` (PreFixed) e `$BP1` (CDI) | 200, sem efeito |

- BUG (alta): 200 projetado com a janela incompleta (μ calculado com 11 meses); 400 com
  `referenceDate=2026-11-03`.
- BUG (média): mensagem da D-4 com contagem ou mês ausente errados (no caso b, citar 2026-03).
- RISCO (baixa): no caso c, a mensagem da janela no lugar da de lacuna esconde que é o mês de março
  que falta importar (registre o texto e deixe a classificação para a decisão 6).

`restaura`.

#### QA12-B08 — Lacuna de IPCA antes do último publicado [P2, B, 10 min]

`tira Ipca 2026-08-01`.

| Chamada | Esperado |
|---|---|
| `Ipca/accumulated?from=2026-01-15&to=2026-09-20` | 400 citando 2026-08 (RN-13: lacuna é falha de importação) |
| `Ipca/accumulated?from=2026-09-01&to=2026-10-31` | 200, produto de setembro e outubro |
| IPCA+ com aporte em 2026-07-01 (`BIPCA3`), `referenceDate=2026-09-01` | 400 citando 2026-08 (RN-10) |
| `positions/$BP3/projection` sem data | 400 da janela ("há 11, falta 2026-08"): agosto está na janela |

- BUG (alta): agosto tratado como estimado (`isEstimated`) em vez de falha; 200 no acumulado.

`restaura`.

#### QA12-B09 — Borda do calendário (2050) [P2, B, 15 min]

```bash
BORDA=$(titulo "QA-F12B BORDA PRE" Cdb PreFixed 10 2026-01-01 2051-06-01)
BORDACDI=$(titulo "QA-F12B BORDA CDI" Cdb Cdi 100 2026-01-01 2051-06-01)
aporte $BORDA 1000 2050-12-30; aporte $BORDA 1000 2051-01-02          # o segundo: 400 (fora do calendário)
aporte $BORDACDI 1000 2026-11-16; aporte $BORDACDI 1000 2050-12-30
for u in "/api/assets/$BORDA/projection" "/api/assets/$BORDACDI/projection" "/api/portfolio/summary"; do
  for r in "" "?referenceDate=2050-12-30" "?referenceDate=2050-12-31" "?referenceDate=2051-01-02"; do
    printf '%-40.40s %-28s ' "$u" "$r"; curl -s -w ' [%{http_code} %{time_total}s]' "$B$u$r" | head -c 220; echo; done; done
```

Esperado (ESPECIFICACAO, "Endpoints": o aporte posterior à data padrão "é projetado na própria data
do aporte (rendimento zero, sem exigir cotação)"; RN-08): aporte em 2051 recusado com 400; sem
data, os aportes de 2050-12-30 entram com rendimento zero e o resumo responde 200; `2050-12-30` e
`2050-12-31` com 200 (o CDI de 16/11 projetado por 24 anos, `isProjected true`, IR 15%); `2051-01-02`
com 400 do calendário. Tempo abaixo de 2 s.

- BUG (média): o resumo ou a projeção por título sem data respondendo 400 do calendário por causa de
  um aporte em 2050-12-30 (uma posição válida derrubando a tela padrão de toda a carteira).
- BUG (alta): 500; valor não finito.

#### QA12-B10 — Muitos aportes [P2, B, 20 min]

```bash
MANY=$(titulo "QA-F12B MUITOS CDI" Cdb Cdi 100 2024-01-01 2030-01-02)
node -e "let d=new Date('2024-01-02');const o=[];while(o.length<300){const w=d.getUTCDay();if(w>0&&w<6)o.push(d.toISOString().slice(0,10));d.setUTCDate(d.getUTCDate()+3)}console.log(o.filter(x=>x<='2026-11-25').join('\n'))" > "$QA/b/datas.txt"
while read d; do aporte $MANY 100.37 $d >> "$QA/b/many-ids.txt"; echo >> "$QA/b/many-ids.txt"; done < "$QA/b/datas.txt"
for r in "" "?referenceDate=2026-11-30" "?referenceDate=2028-06-30"; do
  for k in 1 2 3; do curl -s -o /dev/null -w '%{time_total} ' "$B/api/assets/$MANY/projection$r"; done
  curl -s "$B/api/assets/$MANY/projection$r" | node "$QAW/js/soma.mjs"; done
for k in 1 2 3; do curl -s -o /dev/null -w '%{time_total} %{size_download}\n' "$B/api/portfolio/summary"; done
```

(Datas que caírem em feriado são recusadas ou aceitas conforme a RN-05 e a RN-08; registre quantas
entraram. O valor quebrado, 100,37, força arredondamento em cada item.)

Esperado: `soma.mjs` com `OK` em tudo; `positionCount` igual ao número de aportes aceitos; ordem
decrescente; tempo a quente abaixo de 1 s (a validação mediu 34 a 40 ms para um resumo de 323
posições).

- BUG (alta): soma diferente do total com muitos itens arredondados.
- RISCO (baixa): mais de 1 s a quente, ou a projeção por título bem mais lenta que o resumo com os
  mesmos aportes (carregamento de mercado por aporte em vez de um só, contra a D-11).

#### QA12-B11 — Carga paralela nas rotas novas [P3, B, 10 min]

```bash
seq 200 | xargs -P 20 -I{} sh -c 'curl -s -o /dev/null -w "%{http_code}\n" "'"$B"'/api/indexes/Cdi/accumulated?from=2020-01-02&to=2026-12-31"' | sort | uniq -c
seq 200 | xargs -P 20 -I{} sh -c 'curl -s -o /dev/null -w "%{http_code}\n" "'"$B"'/api/assets/'"$MANY"'/projection?referenceDate=2026-11-30"' | sort | uniq -c
docker logs qaf12-api 2>&1 | grep -ciE 'exception|fail:'
```

Esperado: 200 em todas; nenhuma exceção no log.

- BUG (alta): qualquer 500 sob concorrência (estado compartilhado no carregamento de mercado).

#### QA12-B12 — Fuso de Brasília na fronteira [P1, B, 15 min, só entre 21h e 23h59 de Brasília]

Entre 21h e 24h de Brasília, o UTC já está no dia seguinte. Com T = sexta 27/11 às 22h (UTC sábado
28/11, 01h), L = 25/11 (cópia como está):

| Chamada | Esperado (hoje = 27/11) | Se a API usar UTC (hoje = 28/11) |
|---|---|---|
| `positions/$BP1/projection?referenceDate=2026-11-30` | 200, `isProjected true` | 400 citando 2026-11-26 |
| B05 de novo (`delete` de todo o CDI): `assets/$BVAZIO/projection` | `referenceDate 2026-11-27` | `2026-11-28` |

Para outros dias, monte L = P − 1 dia útil (`cdi_ate`) e use uma data depois de T. No último dia
do mês (30/11), confira também o IPCA: `positions/$BP3/projection?referenceDate=2026-12-15` com
`tira Ipca 2026-10-01` deve continuar com novembro **projetado** (mês de hoje), não estimado.

Registre `date -u` e o cabeçalho `Date` em cada chamada.

- BUG (alta): qualquer coluna "se a API usar UTC".
- NÃO EXECUTADO: fora da janela, com o motivo.

`restaura`.

#### QA12-B13 — Hoje em fim de semana ou feriado [P3, B, 15 min, só em sábado, domingo ou feriado]

RN-12: "num sábado, é a sexta, ou o dia útil antes dela se for feriado". Com T = sábado 28/11
(P = 27/11): L = 26/11 (`sintetico 2026-11-26 0.050788`) e `referenceDate=2026-11-30` → 200
projetado; L = 25/11 → 400 citando 26/11. Com T = domingo, o mesmo. Monte o equivalente para o dia da
execução.

- BUG (alta): o resultado de "último dia útil igual ou anterior a hoje" (a implementação ingênua do
  caso 3-ter).
- NÃO EXECUTADO: execução em dia útil.

`restaura`.

#### QA12-B14 — Fator exato com R$ 100 milhões [P2, B, 10 min]

Com R$ 100.000.000,00, o bruto é o fator de 8 casas vezes 10^8, sem arredondamento de centavo:

```bash
EXATO=$(titulo "QA-F12B EXATO CDI100" Cdb Cdi 100 2020-01-01 2030-01-02)
E1=$(aporte $EXATO 100000000 2026-09-01); E2=$(aporte $EXATO 100000000 2020-01-02)
get "/api/positions/$E1/projection?referenceDate=2026-10-01"     # grossAmount 101081673.00 (fator 1,01081673)
get "/api/positions/$E2/projection"                              # bruto / 10^8 = fator do acumulado abaixo
get "/api/indexes/Cdi/accumulated?from=2020-01-02&to=<D>"
get "/api/positions/$E2/projection?referenceDate=2026-12-31"
curl -s "$B/api/indexes/Cdi?from=2020-01-02&to=<L>" | node "$QAW/js/cdi.mjs" 100 <businessDays>
```

Esperado (RN-09, D-7): `grossAmount` = `100000000 × (1 + accumulatedPercentage/100)` exatamente
até D; depois de D, igual ao `cdi.mjs` com os dias projetados.

- BUG (alta): diferença em qualquer dígito (fator de projeção e de acumulado divergindo, contra a
  D-7: "O produtório do CDI é um só").
- RISCO (baixa): o aporte de R$ 100 milhões recusado (registre o limite e a mensagem).

## O que pode rodar em paralelo

1. **Sequencial no início:** preparação comum, passos 0 a 5 (o `--build` do Compose gera a imagem
   que a Parte B usa).
2. **Parte A e Parte B em paralelo**, em terminais separados (`B` diferente em cada um). Não
   compartilham banco.
3. **Dentro da Parte A**, tudo em qualquer ordem depois da A08 e da A13 (que criam a carteira),
   exceto a A30, no fim. A A20 altera a taxa do `CDI110`: rode-a depois da A13, A17, A21 e A25, ou recalcule.
4. **Dentro da Parte B**, um cenário de cada vez (todos mexem em `index_rate` de `qaf12-db`),
   sempre entre dois `restaura`. B10, B11 e B14 não mexem em cotações e podem rodar junto com
   qualquer um deles **só se** o executor aceitar que os valores com data mudem durante a
   manipulação; o mais simples é deixá-los para o fim. B12 e B13 dependem da janela de horário.

## Fora deste roteiro

- Testes automatizados, cobertura, Stryker e build: feitos pela validação.
- A conferência da regra de 7 casas do IPCA na Calculadora do Cidadão: a validação fez 17 períodos.
- IPCA+ contra uma calculadora externa: não existe referência pública com o mês civil e o pro rata
  desta API (README, "Simplificações do IPCA").
- O SGS publicando de fato com atraso: simulado na Parte B pela remoção de linhas, não observado.
- Mudar o relógio da API: a imagem não tem como fixar a hora sem alterar código ou subir com
  ferramenta extra; a fronteira de horário fica na janela do B12 e do B13.
- Decisão do Copom e expectativas: fora do escopo da fase (spec, "Fora de escopo").
- Qualquer verificação no `renda-fixa-web`.

## Limpeza ao fim

```bash
# Parte B: tudo some com os contêineres próprios
docker rm -f qaf12-api qaf12-db; docker network rm qaf12-net
# Parte A: nada é apagado; os ids ficam no relatório (o título com aporte não pode ser removido, RN-06,
# e não há rota para remover aporte). Só o QA-F12A VAZIO pode ser removido pela API, se o Matheus quiser.
cd "$REPO" && docker compose stop        # ou deixar no ar, conforme a decisão 2; nunca "down -v"
docker volume ls -q | sort | diff - "$QA/antes-volumes.txt" && echo "volumes iguais"
git status --short | diff - "$QA/git-status-antes.txt"      # só docs/QA-F12.md a mais
```

O relatório lista, na seção "Dados criados durante o QA", os ids de todos os títulos e aportes
`QA-F12A` (no banco do Compose) e diz que os `QA-F12B` sumiram com `qaf12-db`.

## Decisões antes de executar

1. **Leitura do banco do Compose.** O passo 1 lê `__EFMigrationsHistory` e `holiday`, o passo 2 tira
   a foto de `index_rate` e a Parte B faz um `pg_dump` (esquema, `holiday`, `index_rate`,
   `__EFMigrationsHistory`) do `fixedincome-db`. Tudo leitura. Autoriza? Sem isso, a Parte B
   precisa aplicar as migrations por conta própria (README, contêiner do SDK) e importar as
   cotações do Banco Central uma vez (rede), e a foto de `index_rate` fica sem prova.
2. **`docker compose up -d --build`** recria `fixedincome-api` e `fixedincome-db` (o validador fez o
   mesmo; o volume é preservado). No fim: `docker compose stop`, deixar no ar, ou `docker compose
   down` sem `-v` (o estado em que a validação deixou)?
3. **Pilha isolada da Parte B** (`qaf12-db` e `qaf12-api` a partir de `renda-fixa-api-api:latest`,
   porta 28112, importação desligada). Autoriza? Sem ela, B01 a B09 e B12 a B14 ficam `NÃO
   EXECUTADO`, ou rodam no Compose pelo caminho de "Restauração, se a Parte B não existir", que
   exige autorização expressa para alterar `index_rate` do Compose.
4. **Rede para a Calculadora do Cidadão** (A10): autoriza três consultas? Sem isso, A10 fica `NÃO
   EXECUTADO`.
5. **Janela de execução.** Rodar uma parte entre 21h e 24h de Brasília dá o B12; num sábado,
   domingo ou feriado, o B13; no dia 30/11, a fronteira do mês no IPCA. Execução em dezembro muda os
   esperados de IPCA da Parte A (ver "Ambiente"). Há preferência de dia e hora?
6. **Classificação de casos sem regra na spec**, para o QA não decidir sozinho: `%20Cdi%20` aceito;
   data com hora aceita; `01/09/2026` interpretado como `MM/dd`; mensagem "Não há cotação" para
   `from == to` num dia com cotação; a escala `0.8200` contra `0.820000`; o IPCA da listagem sem o
   mês de `from` e o acumulado com ele; a mensagem da janela no lugar da de lacuna (B07 c); a
   ordem dos empates do mesmo dia variando entre chamadas. Contam como `BUG` ou `RISCO`?
7. **Volume de dados.** A Parte A cria cerca de 25 títulos e 30 aportes `QA-F12A` no banco do
   Compose, que não podem ser removidos pela API (RN-06). Os 300 aportes da B10 e os de R$ 100
   milhões da B14 ficam só na Parte B, justamente para não entrar no resumo do Matheus. De acordo?
