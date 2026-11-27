# Spec de execução — F12: Contrato v2 para o front

Status: aprovada (27/11/2026); implementada e validada em 27/11/2026 (`docs/VALIDACAO-F12.md`, `docs/QA-F12.md`)

Decisões do Matheus na aprovação: D-5 mantida (2 casas no percentual); A-1 recusada, pelo mesmo
motivo da A-2 do contrato para o front; mudança de comportamento 1 aceita, com teste de 400 sem data
e com menos de 12 meses de IPCA; mudança de comportamento 2 ajustada: o CDI só é projetado a partir
do dia útil anterior a hoje (Brasília), e o dia sem cotação entre a última cotação e esse dia
continua falha de importação (400, R-10). A fronteira "dia útil anterior a hoje", e não "depois de
hoje", foi escolhida porque o CDI de hoje nunca está publicado no SGS durante o dia: com "depois de
hoje", toda projeção futura daria 400.

Documento de execução para agente. Objetivo: entregar ao front `renda-fixa-web` os dados que ele
passou a mostrar depois do redesenho para o público leigo — valor no vencimento, CDI e inflação
acumulados e o total de cada título — prontos, sem cálculo de dinheiro no navegador. A fonte é
`docs/ESPECIFICACAO.md` (RN-09 a RN-14 e a seção "Endpoints") e a seção F12 de `docs/ROADMAP.md`.
O contrato muda só por adição de campos e rotas; o significado de dois campos muda nos casos que a
RN-12 manda (ver "Mudanças de comportamento").

## Contexto

A especificação funcional está em `docs/ESPECIFICACAO.md`. As convenções de código estão em
`CLAUDE.md`. Ambas prevalecem sobre qualquer suposição. As decisões desta spec já estão escritas na
`ESPECIFICACAO.md` (RN-09, RN-10, RN-12, RN-13, RN-14 e "Endpoints"), corrigidas na preparação da
fase para não se contradizerem com a RN-12.

Estado de partida (linha de base medida em 26/11/2026): branch `f12-contrato-front-v2`, HEAD igual a
`origin/main` (`26eec24`), alterações só em `docs/` e `.claude/` (preparação da fase), build sem
warning, **712 testes verdes** (Domain 332, Application 132, Infrastructure 172, Api 68,
Architecture 8).

Como o código está hoje:

- `MarketData` (Domain) guarda o calendário e as séries de CDI e IPCA carregadas e lança
  `DomainException` em cotação duplicada. `GetCdi` e `GetIpca` devolvem nulo sem cotação.
  `LastPublishedIpca` é o maior mês **da série carregada**.
- `MarketDataLoader.LoadAsync(from, to)` (Application) carrega o CDI de `[from, to]`, o IPCA do dia
  1 do mês de `from` até `to` e, se existir, o último IPCA anterior a esse mês (achado I3).
  `GetDefaultReferenceDateAsync` devolve o dia útil seguinte à última cotação de CDI.
- `Position.Project` (Domain) calcula a projeção. `CdiFactor` lança `"Cotação de CDI ausente para
  …"` em qualquer dia útil sem cotação. `IpcaFactor` estima com a última variação o mês posterior
  ao último publicado (`isEstimated`) e lança `"Cotação de IPCA ausente para …"` no mês anterior.
- `GetPositionProjectionUseCase` e `GetPortfolioSummaryUseCase` resolvem a data padrão, carregam o
  mercado e projetam. O resumo faz o filtro por data (achado R-02), a projeção na data do aporte
  posterior à data padrão (`DateFor`) e o tratamento da mensagem de CDI ausente (R-10).
- `GetIndexRatesUseCase` lista as cotações de um período; `IndexesController` valida o
  `indexType` contra o nome do enum.
- `IPositionRepository.GetByAssetIdAsync` existe, sem ordenação; usado na remoção de título (RN-06)
  e no `UpdateAssetUseCase`, que não dependem de ordem.
- O `ApiTestBase` usa o `BrasiliaTimeProvider` real.

Esta fase toca:

- `src/FixedIncome.Domain` — `MarketData`, `Position`, `PositionProjection`, `PortfolioSummary` e
  a classe nova `IndexAccumulation`
- `src/FixedIncome.Application` — `MarketDataLoader`, DTOs, uma classe nova em `Services`, dois
  casos de uso novos e os dois existentes
- `src/FixedIncome.Infrastructure` — só a ordenação de `GetByAssetIdAsync`
- `src/FixedIncome.Api` — `IndexesController`, `AssetsController`, `FixedIncome.Api.http`
- testes dos cinco projetos e `README.md`

## Fora de escopo

- Expectativas do Boletim Focus, decisões do Copom ou qualquer fonte nova de dados (decisão técnica
  de 26/11/2026)
- Marcação a mercado do Tesouro Direto: o valor antes do vencimento continua o da curva
- Mudança de nome ou tipo de campo existente; mudança de cálculo para dia ou mês coberto por
  cotação publicada; mudança da data padrão
- Migration, tabela, coluna ou pacote NuGet novo
- Paginação, filtro ou ordenação configurável nos endpoints novos
- Qualquer arquivo em `../renda-fixa-web`

## Regras novas

As regras são as da `ESPECIFICACAO.md`, transcritas aqui no que a implementação precisa decidir.

**RN-12 — Projeção com data futura.** Dia útil de CDI sem cotação:

- anterior à última cotação de CDI do banco (do banco inteiro, não só do período carregado): falha
  de importação, como hoje (`"Cotação de CDI ausente para …"`, 400);
- posterior a ela e anterior ao dia útil anterior a hoje (Brasília): falha de importação, com a
  mesma mensagem da RN-09 (`"Cotação de CDI ausente para …"`), que os casos de uso completam com o
  aviso do R-10;
- posterior a ela e igual ou posterior ao dia útil anterior a hoje: usa o valor dessa última
  cotação, com o mesmo fator diário e o mesmo truncamento da RN-09 (inclusive com percentual
  diferente de 100), e marca `isProjected`;
- sem nenhuma cotação de CDI no banco: falha, como hoje.

Mês de IPCA sem variação publicada:

- anterior ao último mês publicado: falha de importação, como hoje;
- posterior ao último publicado e anterior ao mês de hoje (Brasília): estimado com a última
  variação, como hoje (RN-10, `isEstimated`);
- o mês de hoje e os seguintes: projetado com a variação mensal da média geométrica (D-5), com o
  pro rata em dias úteis da RN-10, e marca `isProjected`. Sem os 12 meses da janela (D-4), a
  projeção não é calculada: 400 com a mensagem da D-4.

Valor publicado sempre prevalece: estimativa e projeção só valem para dia ou mês sem cotação. A
RN-02 corta antes: com vencimento anterior aos dias ou meses sem cotação, nada é projetado.

**RN-13 — Variação acumulada.** Só cotações publicadas, nunca estimativa nem projeção. CDI: produto
de `1 + CDI do dia / 100` em cada dia útil de `[from, effectiveTo)`, com as casas da RN-09. IPCA:
produto de `1 + IPCA do mês / 100` dos meses de competência do mês de `from` ao mês de
`effectiveTo`, inclusive, sem pro rata. Resultado em percentual, `(fator − 1) × 100`.

**RN-14 — Projeção por título.** Mesmas regras de data do consolidado, totais pela RN-07, lista dos
aportes incluídos, 200 com zeros para título sem aportes incluídos, 404 para título inexistente.

## Mudanças de comportamento

A RN-12 muda duas respostas que existem hoje. Ficam escritas no README e na `ESPECIFICACAO.md`, e
têm teste:

1. **IPCA+ sem data, ou com data no mês de hoje.** O mês de hoje entra no intervalo e passa de
   estimado (última variação, `isEstimated`) a projetado (média geométrica, `isProjected`), com
   outro valor. Na tela padrão, todo IPCA+ com dia útil no mês de hoje sai projetado, e a raiz do
   resumo também. Com menos de 12 meses de IPCA no banco, o resumo e a projeção sem data de um
   IPCA+ passam de 200 a 400. Referência no primeiro dia útil do mês de hoje (fim exclusivo) não
   tem dia desse mês no intervalo e não muda.
2. **Data informada depois da última cotação de CDI, com a importação em dia.** Quando a última
   cotação é de dois dias úteis antes de hoje ou mais recente (ou seja, nenhum dia sem cotação fica
   antes do dia útil anterior a hoje), deixa de ser 400 e passa a 200 projetado, com `isProjected`. Com a
   importação parada, o primeiro dia sem cotação antes do dia útil anterior a hoje continua 400 com
   o aviso do R-10, como hoje. A data padrão nunca projeta CDI, porque é o dia útil seguinte à
   última cotação e o fim do intervalo é exclusivo.

O teste do `swagger.json` prova nome e tipo; estas duas mudanças são provadas pelos testes de
Application listados abaixo.

## Decisões de desenho

**D-1. "Última cotação" é a do banco, não a do período carregado.** Decidir "lacuna" ou "projeção"
pela série carregada esconderia lacunas: CDI importado até 25/11/2026, sem a cotação de 18/11, e
projeção até 19/11 — a série carregada termina em 17/11, e o dia 18 viraria projeção em vez de 400.
Por isso `MarketDataLoader.LoadAsync` passa a incluir sempre, além do período:

- a última cotação de CDI do banco (`GetLastReferenceDateAsync(Cdi)` e
  `GetByIndexTypeAndPeriodAsync(Cdi, última, última)`);
- os 12 meses de IPCA da janela da D-4 (`GetLastReferenceDateAsync(Ipca)` e
  `GetByIndexTypeAndPeriodAsync(Ipca, último − 11 meses, último)`).

As faixas se sobrepõem no caso comum (período até a última cotação, ou dentro dos 12 meses), e o
`MarketData` recusa duplicata: a união é deduplicada por indexador e data antes de construir o
`MarketData`. A busca do achado I3 (`GetLastBeforeAsync`) sai do loader, porque o último IPCA
publicado está sempre na janela; o método continua no repositório. Com isso, o maior dia e o maior
mês de cada série do `MarketData` são os do banco, e o `MarketData` continua sem consultar nada. O
contrato fica escrito no comentário do `MarketData`. Nenhum método novo de repositório.

**D-2. "Hoje" entra no `MarketData`.** Duas fronteiras dependem de hoje: o mês de IPCA estimado ou
projetado (D-3) e o dia de CDI com falha de importação ou projetado. O "dia útil anterior a hoje" é
o maior dia útil estritamente anterior a hoje, seja hoje dia útil ou não (hoje sábado 21/11/2026,
domingo 22/11 ou feriado 20/11 → 19/11). A regra é implementada na forma equivalente que não leva
`today` ao calendário: **um dia útil sem cotação, posterior à última cotação, é falha de importação
se `NextBusinessDay(dia) < today`; senão é projetado.** Assim o calendário só é consultado dentro do
intervalo, já coberto, e um `today` fora da cobertura (como o dos testes) não lança. Nada de
`PreviousBusinessDay(today)`. O `MarketData` recebe `DateOnly today` no construtor; o
`MarketDataLoader` recebe o `TimeProvider` (já registrado como `BrasiliaTimeProvider`) e passa
`DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime)`, a mesma conversão dos casos de uso. O
domínio recebe só uma data, sem relógio. Em produção os relógios do loader e dos casos de uso são o
mesmo singleton; nos testes são construídos separados.

**D-3. O mês de hoje (Brasília) e os seguintes são projetados.** O IPCA do mês M mede M inteiro e só
sai em M+1: no último dia de M, ele não está encerrado. A comparação é
`mês >= primeiro dia do mês de hoje`. Com a fronteira na virada de ano (31/12 → 01/01) testada.

**D-4. A janela da média são 12 meses de competência seguidos**, do último publicado menos 11 até
ele: a convenção do "IPCA acumulado em 12 meses" do IBGE (SIDRA, tabela 1737). Se faltar algum, a
projeção que precisa de mês projetado falha com
`"A projeção futura do IPCA exige os 12 meses de {início:yyyy-MM} a {fim:yyyy-MM}; há {N}, falta {primeiro ausente:yyyy-MM}."`.
Projeção sem mês projetado não exige a janela.

**D-5. A variação mensal projetada sai em percentual com 2 casas.**
`IPCA projetado (%) = round(((∏ (1 + IPCA do mês / 100)) ^ (1 / 12) − 1) × 100, 2, AwayFromZero)`,
com o produtório sem arredondamento intermediário, um único arredondamento, no percentual (nunca na
fração), e potência por `DecimalMath.Pow`. Pode ser negativo. O valor entra no pro rata da RN-10 como
se fosse uma variação publicada. Precedente: o IPCA projetado que a ANBIMA usa no VNA da NTN-B é uma
variação mensal em % com 2 casas. Fica no mesmo formato da série 433, o caso de referência confere à
mão e o teste é igualdade exata. Custo: viés de até ±0,005 p.p. ao mês. Alternativa, se o Matheus
preferir fidelidade ao acumulado de 12 meses: a fração com 8 casas.

**D-6. `isProjected` diz se alguma taxa projetada entrou no cálculo.** Na projeção, verdadeiro se
algum dia útil de CDI ou algum mês de IPCA do intervalo usou valor projetado. PreFixed nunca é
projetado: a taxa é contratada. No consolidado, a raiz é verdadeira se algum item for, calculada em
`PortfolioSummary.Create` (Domain), como último parâmetro do record. `isEstimated` continua só nos
itens (decisão A-2 do contrato para o front).

**D-7. O produtório do CDI é um só.** O fator da RN-09 (fator diário `1 + CDI/100 × p/100`,
acumulado truncado em 16 casas a cada dia, fator final em 8) vira um método estático de
`IndexAccumulation` que recebe as taxas diárias já resolvidas e o percentual, sem saber de lacuna
nem de projeção. `Position` resolve cada dia (publicado, projetado ou falha) e chama esse método; a
RN-13 o chama com percentual 100. Com percentual 100 o fator diário é exatamente `1 + CDI/100`, e o
acumulado da RN-13 é o fator da RN-09 por construção.

**D-8. `IndexAccumulation` é uma classe estática do Domain**, em `ValueObjects`, com os métodos do
CDI e do IPCA da RN-13 sobre o `MarketData` carregado e um record de resultado
`(decimal AccumulatedPercentage, DateOnly EffectiveTo)`. Só lê cotações publicadas.

**D-9. `effectiveTo` e os erros do acumulado, separados por indexador.**

CDI:

- `effectiveTo = min(to, dia útil seguinte à última cotação)`: o menor fim que inclui a última
  cotação no fim exclusivo da RN-08, a mesma data que a projeção usa como padrão. Sem corte,
  `effectiveTo` é o próprio `to`. Chamar de novo com `to = effectiveTo` dá o mesmo resultado.
- O corte é calculado antes de percorrer o intervalo: `to` posterior à cobertura do calendário não é
  erro, porque o período termina na última cotação. `from` fora da cobertura é 400 (calendário).
- 400: `from > to`; banco sem CDI; nenhum dia útil em `[from, effectiveTo)` (`from` depois da última
  cotação, fim de semana); lacuna antes da última cotação, com a mensagem da RN-09.
- `from == to` é 400 com mensagem própria, conferida antes da série: "O período de … a … está vazio:
  no CDI a data final não entra no acumulado, então to precisa ser posterior a from." Acrescentado
  depois do QA (risco 4, caso A04), a pedido do Matheus em 27/11/2026: antes a resposta era "Não há
  cotação de CDI publicada", errada para um dia com cotação.

IPCA:

- Todas as comparações (corte, período sem cotação, lacuna) são por mês de competência.
- Sem corte (mês de `to` até o último publicado), `effectiveTo` é o próprio `to`. Com corte,
  `effectiveTo` é o **último dia** do último mês publicado: nunca fica antes de `from` (o mês de
  `from` é no máximo o último publicado, senão é 400) e "até 31/10" é a leitura certa de um índice
  que mede o mês inteiro. Chamar de novo com `to = effectiveTo` dá o mesmo resultado.
- O IPCA não consulta o calendário.
- 400: `from > to`; banco sem IPCA; mês de `from` posterior ao último publicado; lacuna antes do
  último publicado, com a mensagem da RN-10. `from == to` dentro de um mês publicado é 200, com a
  variação desse mês.

Os dois: `indexType` PreFixed ou numérico é 400, como em `GET /api/indexes/{indexType}`.

**D-10. Arredondamento do acumulado.** CDI: fator de 8 casas (o da RN-09). IPCA: fator
`Math.Round(produto, 7, AwayFromZero)`, que é o que reproduz a Calculadora do Cidadão (consultas de
26/11/2026: 01/2026–09/2026 dá 1,03951320 e 3,951320 %; 01/2025–12/2025 dá 1,04264380 e 4,264380 %;
com 8 casas a 6ª casa do percentual diverge). A regra de 7 casas foi inferida de duas consultas, não
de metodologia publicada pelo BC (**a confirmar** na validação). Nos dois, o percentual é
`Math.Round((fator − 1) × 100, 6, AwayFromZero)`, explícito para o JSON sair `1.081673` e não
`1.08167300`.

**D-11. Projeção por título reaproveita o consolidado.** As regras de data do resumo saem de
`GetPortfolioSummaryUseCase` para uma classe concreta em `Application/Services`, sem interface (não
cruza camada, como o `MarketDataLoader`), usada pelos dois casos de uso:

- entrada: os aportes, os títulos por id e a `referenceDate` informada (ou nula);
- saída: a data resolvida e a lista de (posição, título, projeção, data usada), na ordem de entrada;
- faz o filtro por data informada (R-02), a resolução da data padrão, `DateFor`, um único
  carregamento de mercado do aporte mais antigo à maior data e a mensagem do R-10 com a posição e o
  título, que sai como `DomainException` já formatada;
- o 404 fica em cada caso de uso, antes de chamar a classe (antes do calendário).

Os totais vêm de `PortfolioSummary.Create` (RN-07), e o `ByAssetType` dele não entra na resposta do
título. Alternativa descartada: copiar a lógica, que é como as duas respostas passariam a divergir.

**D-12. Ordem dos aportes no título.** A do resumo: aporte mais recente primeiro, sem garantia entre
aportes do mesmo dia. `GetByAssetIdAsync` ganha `OrderByDescending(ApplicationDate)`, a mesma de
`GetAllAsync`. Registrada na `ESPECIFICACAO.md`.

### Propostas das revisões além do escopo pedido

| # | Proposta | Origem | Decisão do Matheus |
|---|---|---|---|
| A-1 | `isProjected` na raiz da projeção por título, para o front dizer "total projetado" sem varrer a lista. Não está na RN-14 nem no ROADMAP | coordenador; o `revisor-arquitetura` recomenda tirar | **Recusada**: mesmo motivo da A-2 do contrato para o front, o front detecta pelos itens |

### Contrato resultante

`GET /api/positions/{id}/projection` — `data` ganha `isProjected` (bool), depois de
`applicationDate`.

`GET /api/portfolio/summary` — `data` ganha `isProjected` (bool) na raiz, depois de `positions`;
cada `positions[].projection` ganha `isProjected` (é o mesmo DTO da projeção).

`GET /api/indexes/{indexType}/accumulated?from=&to=` — novo:

```json
{
  "indexType": "Cdi",
  "from": "2026-09-01",
  "to": "2026-10-01",
  "effectiveTo": "2026-10-01",
  "accumulatedPercentage": 1.081673
}
```

`GET /api/assets/{id}/projection?referenceDate=` — novo:

```json
{
  "assetId": "…",
  "totalInvestedAmount": 2000, "totalGrossAmount": 2009.86, "totalGrossYield": 9.86,
  "totalTaxAmount": 1.56, "totalIofAmount": 2.96, "totalNetAmount": 2005.34,
  "positionCount": 2,
  "referenceDate": "2025-04-22",
  "positions": [ { "positionId": "…", "assetId": "…", "…": "…", "isProjected": false } ]
}
```

Os itens de `positions` são o `PositionProjectionResponse` do endpoint de projeção; os nomes dos
totais são os do resumo. Sem `isProjected` na raiz (A-1 recusada). Respostas de erro
(400, 404) seguem o envelope com `data` nulo.

## Implementação em passos

Cada passo: teste antes do código, `dotnet build` sem warning e `dotnet test` verde ao final, sem
queda de contagem. Os testes de API e as asserções do `swagger.json` de cada campo ou rota nascem no
passo que cria o código.

### Testes existentes que mudam

Só estes. Qualquer outro teste existente que quebrar: o implementador para e reporta.

| Teste | Por quê | Adaptação |
|---|---|---|
| `GetPositionProjectionUseCaseTests.Cotacao_de_cdi_ausente_retorna_400_com_a_primeira_data_faltante` e `…_lembra_que_a_importacao_pode_estar_desligada`; `GetPortfolioSummaryUseCaseTests.Data_explicita_sem_cotacao_de_cdi_retorna_400_com_a_primeira_data_faltante` e `Cotacao_de_cdi_ausente_identifica_a_posicao_e_lembra_da_importacao` | Com só o CDI de 01/04/2025, o dia 02/04 fica depois da última cotação | **Nenhuma**, desde que o loader receba o relógio do arquivo (2025-12-01): 02/04 fica antes do dia útil anterior a hoje e continua falha de importação, com a mesma mensagem. Se quebrarem, o implementador para e reporta |
| `GetPortfolioSummaryUseCaseTests.Item_reflete_o_is_estimated_da_projecao_com_o_caso_ipca_de_mes_sem_divulgacao` | O relógio do arquivo (2025-12-01) tornaria outubro/2026 um mês projetado | O loader recebe um relógio em novembro/2026 (outubro encerrado e não divulgado). Valores iguais |
| `MarketDataLoaderTests.Carrega_o_cdi_do_periodo_com_as_duas_pontas_inclusivas_e_nada_alem_delas` | A última cotação do banco (16/09) passa a entrar sempre (D-1) | O "nada além" passa a excluir só o que não é a última cotação; nome ajustado |
| `MarketDataLoaderTests.Ipca_estimado_fora_do_periodo_marca_a_projecao_como_estimada` e `Inclui_o_ultimo_ipca_publicado_anterior_ao_periodo_para_a_estimativa` | Dependem do relógio do loader e do I3, que sai (D-1) | Relógio fixo depois dos meses estimados; o último publicado vem da janela. Valores iguais |
| `PositionProjectionFlowTests` (L185) e `PortfolioSummaryFlowTests` (L174) | Lista fechada de propriedades | `isProjected` entra na lista (P2) |
| Construções do `MarketData` (16), do `MarketDataLoader` (4) e `PortfolioSummaryTests` (L254, record posicional) | Assinatura nova | Mecânica. `TestMarket.Create(rates)` mantém a assinatura com `today = 2031-01-01` (depois da cobertura dos testes, nenhum mês projetado), com uma sobrecarga que recebe `today` para os testes novos |

### Passos

**P1 — RN-12 no domínio e no carregamento (Sonnet).**
`MarketData` recebe `today` (D-2) e expõe a última cotação de CDI da série e a variação projetada do
IPCA (D-4, D-5), ou o motivo de não haver. `Position.Project` aplica as "Regras novas" e devolve
`IsProjected` como último parâmetro de `PositionProjection`. O produtório do CDI vai para
`IndexAccumulation` (D-7), ainda só com esse método. `PortfolioSummary` ganha `IsProjected` (D-6).
`MarketDataLoader` recebe o `TimeProvider` e monta a série da D-1, com deduplicação e sem o I3. As
adaptações da tabela acima, exceto as de API. Vão juntos porque o construtor novo do `MarketData`
quebra o loader, e o domínio sem a última cotação do banco teria o defeito da D-1.

**P2 — RN-12 no contrato (Sonnet).**
`PositionProjectionResponse` e `PortfolioSummaryResponse` ganham `IsProjected` como último
parâmetro. Testes de Application das "Mudanças de comportamento" e da mensagem da D-4; testes de
API com `isProjected` (só CDI e PreFixed, ver "Testes"); as listas fechadas dos dois testes de API;
asserção de `isProjected` nos dois schemas do `swagger.json`.

**P3 — RN-13 (Sonnet).**
Métodos de CDI e IPCA em `IndexAccumulation` (D-8, D-9, D-10). `GetIndexAccumulationUseCase` com
interface, `IndexAccumulationResponse(IndexType IndexType, DateOnly From, DateOnly To, DateOnly
EffectiveTo, decimal AccumulatedPercentage)`, registro em DI e a ação `GetAccumulated` em
`IndexesController` (`[HttpGet("{indexType}/accumulated")]`), com `from` e `to` `[Required]` e a
mesma validação do `indexType` por nome da ação existente. Testes de API e do `swagger.json` da rota.

**P4a — Extração da lógica do resumo (Sonnet).**
A classe da D-11, com `GetPortfolioSummaryUseCase` passando a usá-la. Todos os testes do resumo
verdes sem alteração de valor; nenhum teste novo de comportamento além dos da própria classe.

**P4b — RN-14 (Sonnet).**
`GetAssetProjectionUseCase` com interface, `AssetProjectionResponse` (contrato acima), ordenação de
`GetByAssetIdAsync` (D-12), registro em DI e a ação `GetProjection` em `AssetsController`
(`[HttpGet("{id}/projection")]`, `referenceDate` opcional). Testes de API e do `swagger.json`.

**P5 — Documentação (Haiku).**
`README.md`: os endpoints novos, `isProjected`, as "Mudanças de comportamento", o aviso de que taxa
projetada não é previsão (não considera decisão do Copom já anunciada nem expectativas; com a
importação em dia, a data informada projeta com a última taxa importada; com ela parada há mais de um
dia útil, ou se o SGS atrasar um dia inteiro, a resposta é 400 com o aviso do R-10) e o aviso de que o IPCA
acumulado (meses cheios) não fecha com o bruto de um IPCA+ (pro rata), enquanto o CDI fecha.
`FixedIncome.Api.http`: as duas rotas novas e uma projeção com data futura. Conferir que a
`ESPECIFICACAO.md` bate com o implementado.

## Testes

Padrão do `CLAUDE.md` e da skill `testes-xunit`. Os valores esperados dos casos marcados com
**(ref)** são os da seção "Casos de referência". Testes de API usam o `BrasiliaTimeProvider` real,
então não usam IPCA (o resultado dependeria do mês em que rodam): IPCA projetado só em Domain e
Application. O teste de API que projeta CDI (35) depende de hoje pela fronteira do dia útil anterior:
ele registra um `TimeProvider` fixo na factory (`ConfigureTestServices`; o registro da Application é
`TryAddSingleton`), com as cotações gravadas relativas a essa data.

Domain — projeção (P1):

1. **(ref)** CDB 100% do CDI com data futura e `today` 2026-11-19: dias depois da última cotação
   usam o valor dela, `IsProjected` verdadeiro, `IsEstimated` falso. E o 1-bis.
2. **(ref)** O mesmo com 110% do CDI.
3. **(ref)** Fronteira do CDI com feriado logo depois da última cotação (última em 19/11/2026, 20/11
   feriado, `today` 2026-11-19): referência 23/11 não projeta; 24/11 projeta um dia (23/11).
3-bis. **(ref)** Fronteira do dia útil anterior a hoje (caso 3-bis): mesma série, referência
   2026-11-26. `today` 2026-11-26: `DomainException` de cotação ausente para 2026-11-23 (os dias 23 e
   24 ficam antes do dia útil anterior a hoje, 25/11). `today` 2026-11-24 (dia útil anterior 23/11):
   projeta, com os valores do caso 1. `today` 2026-11-25 (dia útil anterior 24/11): falha em 23/11.
   `today` 2026-11-23 (dia útil anterior 19/11, por causa do feriado e do fim de semana): projeta.
3-ter. **(ref)** `today` em dia não útil (caso 3-ter): última cotação 18/11 e `today` sábado 21/11 ou
   feriado 20/11, projeta com os valores do caso 1; última cotação 17/11 e `today` 21/11, falha em
   2026-11-18.
4. Lacuna de CDI antes da última cotação, com o período terminando logo depois da lacuna (cenário da
   D-1): `DomainException` de cotação ausente com a data da lacuna.
5. Vencimento (RN-02) antes do primeiro dia sem cotação e referência depois: `IsProjected` falso.
6. **(ref)** IPCA+ com data futura e `today` num mês posterior ao último publicado, com um mês
   encerrado e não divulgado no meio: o mês encerrado usa a última variação (`IsEstimated`), o mês de
   hoje e os seguintes usam a média (`IsProjected`); as duas marcas verdadeiras juntas.
7. **(ref)** Variação projetada: a média de uma janela conhecida, arredondada em 2 casas (D-5); uma
   janela deflacionária com média negativa; com 13 meses ou mais na série, só os 12 da janela entram.
8. Fronteira da D-3 na virada de ano: `today` 31/12 sem IPCA de dezembro, dezembro projetado; `today`
   no primeiro dia de janeiro, dezembro passa a estimado.
9. Janela incompleta: série com só 11 meses e, separado, 12 meses com uma lacuna dentro.
   `DomainException` com a mensagem da D-4 (contagem e primeiro mês ausente). IPCA+ sem mês projetado
   no intervalo não exige a janela.
10. Mês com IPCA publicado no mês de hoje (stub): usa o publicado, sem `IsProjected`.
11. PreFixed com data futura: `IsProjected` falso.
12. Data posterior à cobertura do calendário continua `DomainException` do calendário.

Domain — consolidado (P1):

13. `PortfolioSummary.IsProjected` verdadeiro com um item projetado entre outros; falso sem nenhum;
    falso com a lista vazia.

Domain — acumulado (P3):

14. **(ref)** CDI de 2026-09-01 a 2026-10-01 com as cotações do caso 4 da SPEC-F7: fator 1,01081673,
    percentual 1,081673; vezes R$ 1.000 dá R$ 1.010,82, o valor da Calculadora do Cidadão
    (`docs/VALIDACAO-F7.md`, critério do ROADMAP). E é igual ao `GrossAmount` de um CDB 100% do CDI
    de R$ 1.000 nas mesmas datas (prova da D-7).
15. CDI com `to` depois da última cotação: `EffectiveTo` é o dia útil seguinte a ela, e o percentual
    é igual ao de chamar com `to = EffectiveTo`; `to` igual a esse dia, sem corte; `to` depois da
    cobertura do calendário, corta sem erro. O acumulado nunca usa a última taxa como projeção.
16. **(ref)** IPCA de 01/2026 a 09/2026 (com deflação em agosto) e de 01/2025 a 12/2025, iguais aos
    da Calculadora do Cidadão (D-10), com `from` e `to` no meio dos meses (sem pro rata).
17. IPCA com o mês de `to` depois do último publicado: `EffectiveTo` é o último dia do último
    publicado; com `from` no meio do último publicado, 200 com um mês e `EffectiveTo` depois de `from`.
18. IPCA com `from == to` no meio de um mês publicado: a variação desse mês.
19. Lacuna antes da última cotação, CDI e IPCA: `DomainException` de cotação ausente.
20. Período sem cotação, por indexador (D-9): CDI com `from` depois da última cotação, fim de
    semana e série vazia; CDI com `from == to`, com a mensagem de período vazio, com e sem série; CDI com `from` fora da cobertura; IPCA com mês de `from` depois do
    último publicado e série vazia. Todos `DomainException`.
21. O acumulado do IPCA ignora a estimativa: mês encerrado e não divulgado não entra.

Application (P1, P2, P3, P4), com stubs `private sealed`:

22. Loader: a série contém a última cotação de CDI e os 12 meses de IPCA do repositório, mesmo com o
    período longe deles; período sobreposto à janela e à última cotação não gera duplicata; `today`
    vem do `TimeProvider`.
23. Projeção e resumo com data futura: `IsProjected` no DTO; raiz do resumo verdadeira com um item
    projetado.
24. Mudança de comportamento 1: projeção de IPCA+ sem data com dia útil no mês de hoje,
    `IsProjected` verdadeiro e `IsEstimated` falso; referência no primeiro dia útil do mês de hoje,
    não projetado e sem exigir 12 meses. Com menos de 12 meses de IPCA no repositório, **a projeção
    sem data e o resumo sem data** de um IPCA+ com dia útil no mês de hoje respondem 400 com a
    mensagem da D-4 (dois testes, pedidos pelo Matheus na aprovação).
25. Mudança de comportamento 2, nos dois casos de uso: com a última cotação de CDI dois dias úteis
    antes de hoje, data informada futura responde 200 projetado (fronteira); com três dias úteis
    antes, 400 citando o dia T−2, com a mensagem de CDI ausente e o aviso do R-10 (no resumo, com a
    posição e o título).
26. Projeção futura de IPCA+ com a janela incompleta: 400 com a mensagem da D-4.
27. Acumulado: `from > to` 400; PreFixed 400; período sem cotação 400; resposta com `From`, `To` e
    `EffectiveTo`.
28. Classe da D-11 (P4a): mesma saída do resumo de hoje para os cenários de data já cobertos.
29. Projeção por título: **(ref)** dois aportes do mesmo CDB PreFixed (caso 12a do contrato para o
    front: totais 2.000,00 / 2.009,86 / 9,86 / IR 1,56 / IOF 2,96 / líquido 2.005,34); a soma de cada
    campo monetário dos itens é igual ao total; `PositionCount` igual ao tamanho da lista.
30. Projeção por título sem aportes, e com todos os aportes depois da data informada: 200, totais
    zero, lista vazia, `ReferenceDate` presente (com e sem data informada).
31. Título inexistente: 404, inclusive com o calendário vazio no stub (o 404 vem antes do calendário).
32. Projeção por título com data informada exclui o aporte posterior; sem data, o aporte posterior à
    data padrão é projetado na própria data do aporte.
33. Projeção por título só traz os aportes daquele título, na ordem da D-12, e a mensagem do R-10 cita
    a posição e o título.

Infrastructure (P4b):

34. `GetByAssetIdAsync` devolve os aportes do título do mais recente para o mais antigo.

Api, contra o PostgreSQL do Testcontainers, só CDI e PreFixed:

35. (P2) Com `TimeProvider` fixo na factory e a última cotação de CDI gravada no dia útil anterior a
    essa data: projeção de posição com data futura, `isProjected` verdadeiro; resumo com o mesmo
    aporte, raiz e item verdadeiros.
36. (P3) `GET /api/indexes/Cdi/accumulated` com cotações gravadas: 200, camelCase, datas
    `yyyy-MM-dd`, `accumulatedPercentage` conferido no literal do JSON (`1.081673`); sem `from` ou
    `to`, 400; `indexType` numérico, 400.
37. (P4b) `GET /api/assets/{id}/projection`: 200 com o contrato acima; 404 para id inexistente; 400
    para data posterior à cobertura do calendário (título com vencimento depois de 2050).
38. (P2, P3, P4b) `swagger.json`: os dois caminhos novos, os schemas `IndexAccumulationResponse` e
    `AssetProjectionResponse`, `isProjected` na projeção e no resumo, e os campos existentes de
    projeção e resumo com o mesmo nome e tipo (lista fechada).

Mutation testing: o Domain muda, então o Stryker roda no fim da fase (`dotnet stryker` em
`tests/FixedIncome.Domain.Tests`), com score de 80% ou mais.

## Casos de referência

Produzidos pelo `especialista-renda-fixa` (modo oráculo) em 26/11/2026, à mão em decimal, sem sair
do código. Os produtos de IPCA foram conferidos na Calculadora do Cidadão. Os casos 1, 1-bis, 2, 3,
6, 6-bis, 7a, 14 e os produtos de 16 foram recalculados pelo coordenador em `decimal` (PowerShell),
de forma independente, com o mesmo resultado. Nenhum arredondamento final fica a menos de 0,0002 do
ponto médio.

### Convenções

| Id | Convenção | Fonte |
|---|---|---|
| R1 | Dias úteis pela RN-08 (inclui a inicial, exclui a final). Feriados: 12/10, 02/11, 20/11 (nacional desde 2024) e 25/12/2026; 01/01/2027. 15/11/2026 é domingo | RN-08; ANBIMA |
| R2 | CDI: fator diário `1 + CDI/100 × p/100` exato, acumulado truncado em 16 casas a cada dia, fator final em 8 casas. Dia útil depois da última cotação usa o valor dela | RN-09, RN-12, D-7; B3, Caderno de Fórmulas, 2.1.1 |
| R3 | IPCA+: `Π_M (1+IPCA_M/100)^(DU_M no intervalo / DU do mês civil) × 1,06^(DU/252)`, com `DecimalMath.Pow` | RN-10, RN-12, D-3 |
| R4 | Variação projetada: `round(((Π 12 meses)^(1/12) − 1) × 100, 2, AwayFromZero)` | D-5 |
| R5 | IOF pela RN-11 (dia 7 = 76%, 8 = 73%, 10 = 66%, 30 em diante = 0%); IR sobre (rendimento − IOF), 22,5% até 180 dias | RN-03, RN-11; Decreto 6.306/2007 |
| R6 | Acumulado: CDI fator em 8 casas; IPCA fator `Math.Round(produto, 7)`; percentual `Math.Round((fator − 1) × 100, 6)` | RN-13, D-10 |

Os casos 6, 6-bis e 7 dependem da D-5 como está (2 casas no percentual): se a alternativa for
escolhida, são recalculados (a D-5 foi mantida na aprovação).

### 1, 1-bis, 2, 3 e 3-bis. CDB com data futura (testes 1, 2, 3 e 3-bis)

**Stub:** série 12 real cortada em 19/11/2026, que é a última cotação do banco no stub: 0,050788%
a.d. em 16, 17, 18 e 19/11/2026. (No SGS real, 23, 24 e 25/11 também são 0,050788; por isso o
1-bis.) **`today` = 2026-11-19** nos casos 1, 1-bis, 2 e 3 (decisão de 27/11/2026): o dia útil anterior
a hoje é 18/11, e 23, 24 e 25/11 ficam depois dele, então são projetados.

**Entrada:** R$ 1.000, CDB, aplicação 16/11/2026 (segunda), emissão 01/01/2026, vencimento
01/01/2028. Dias úteis de [16/11, 26/11): 16, 17, 18, 19 publicados; 20/11 feriado; 23, 24, 25
projetados. 7 DU, 10 dias corridos.

Com `a` = 0,00050788: `(1+a)^4` truncado = 1,0020330681766474 (fator 1,00203307); `(1+a)^5` =
1,0025419807313130 (1,00254198); `(1+a)^7` = 1,0035605813714389 (1,00356058). Passo a passo: A1 =
1,00050788; A2 = 1,0010160179420944; A3 = 1,0015244139572868; A4 = 1,0020330681766474. Com `b` =
0,000558668 (110%): `(1+b)^7` = 1,0039172364148334 (1,00391724).

| # | Título | Referência | DU (publ. + proj.) | Corridos | Fator | Bruto | IOF | IR | Líquido | IsProjected |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | CDB 100% | 2026-11-26 | 7 (4 + 3) | 10 | 1,00356058 | **1.003,56** | 66% → **2,35** | **0,27** | **1.000,94** | true |
| 2 | CDB 110% | 2026-11-26 | 7 (4 + 3) | 10 | 1,00391724 | **1.003,92** | 66% → **2,59** | **0,30** | **1.001,03** | true |
| 3a | CDB 100% | 2026-11-23 | 4 (4 + 0) | 7 | 1,00203307 | **1.002,03** | 76% → **1,54** | **0,11** | **1.000,38** | **false** |
| 3b | CDB 100% | 2026-11-24 | 5 (4 + 1) | 8 | 1,00254198 | **1.002,54** | 73% → **1,85** | **0,16** | **1.000,53** | **true** |

`TaxRate` 22,5 e `IsEstimated` falso em todos. 3a é a data padrão (dia útil seguinte a 19/11): nada
é projetado. 3b projeta só 23/11.

**1-bis (sintético, prova que a projeção usa a última cotação).** Igual ao caso 1, com 19/11 =
0,051660. Dias 16, 17, 18 a 0,050788; 19 publicado e 23, 24, 25 projetados a 0,051660. A3 =
1,0015244139572868; `(1,0005166)^4` = 1,0020680018048229; fator **1,00359557**. Bruto **1.003,60**;
IOF 66% × 3,60 → **2,38**; IR 22,5% × 1,22 → **0,27**; líquido **1.000,95**; `IsProjected` true. Se a
implementação usar 0,050788 nos dias projetados, o bruto dá 1.003,56.

**3-bis (fronteira do dia útil anterior a hoje).** Mesmo stub e mesmo título do caso 1, referência
2026-11-26; varia só `today`.

| `today` | Dia útil anterior a hoje | 23/11 | 24/11 | 25/11 | Resultado |
|---|---|---|---|---|---|
| 2026-11-26 (qui) | 25/11 | falha | falha | projetado | **`DomainException` "Cotação de CDI ausente para 2026-11-23"**; no caso de uso, 400 com o aviso do R-10 |
| 2026-11-25 (qua) | 24/11 | falha | projetado | projetado | `DomainException` para 2026-11-23 |
| 2026-11-24 (ter) | 23/11 | projetado | projetado | projetado | igual ao caso 1: bruto 1.003,56, líquido 1.000,94, `IsProjected` true |
| 2026-11-23 (seg) | 19/11 (20/11 feriado, 21 e 22 fim de semana) | projetado | projetado | projetado | igual ao caso 1 |

Confirmada pelo `especialista-renda-fixa` em 27/11/2026, com os casos 1, 1-bis, 2, 3a e 3b inalterados
com `today` = 2026-11-19. (O stub com a cotação do próprio `today` não acontece no SGS real, onde o CDI
de D sai no dia útil seguinte; não muda nenhum valor.)

**3-ter (`today` em dia não útil).** Mesmo título, referência 2026-11-26. Pega a implementação ingênua
que, num sábado, parte do último dia útil igual ou anterior a hoje e chega a 18/11.

| Stub (última cotação) | `today` | Dia útil anterior | Resultado |
|---|---|---|---|
| 18/11 (cotações de 16, 17 e 18/11) | 2026-11-21 (sábado) | 19/11 | 19, 23, 24 e 25 projetados; bruto 1.003,56, IOF 2,35, IR 0,27, líquido 1.000,94, `IsProjected` true (taxa constante, igual ao caso 1) |
| 18/11 | 2026-11-20 (feriado) | 19/11 | o mesmo |
| 17/11 | 2026-11-21 (sábado) | 19/11 | `DomainException` "Cotação de CDI ausente para 2026-11-18" |

Fontes: SGS 12 (26/10 a 16/11/2026 e 17/11 a 26/11/2026); B3, caderno citado; Decreto 6.306/2007.

### 6 e 6-bis. IPCA+ 6% com data futura, as duas marcas juntas (teste 6, fronteira do teste 8)

**Entrada:** R$ 1.000, CDB IPCA+ 6% a.a., aplicação 01/10/2026 (quinta), referência 15/12/2026
(terça), emissão 01/01/2026, vencimento 01/01/2028, `today` = **2026-11-05**.

**Stub de IPCA:** janela real da série 433, 10/2025 a 09/2026: 0,09; 0,18; 0,33; 0,33; 0,70; 0,88;
0,67; 0,58; 0,16; 0,07; −0,32; 0,82. Último publicado: 09/2026. O IPCA de 10/2026 (0,14) fica fora do
stub: em 05/11 ainda não tinha sido divulgado.

**Variação projetada:** produto = 1,0457611967507 (Calculadora 10/2025–09/2026: 1,04576120); média
0,37357136% → **0,37%**.

| Mês | Situação | Variação | DU no intervalo / DU do mês | Fator do mês |
|---|---|---|---|---|
| 10/2026 | estimado | 0,82 | 21/21 (12/10 feriado) | 1,0082 |
| 11/2026 | projetado | 0,37 | 19/19 (02/11 e 20/11 feriados) | 1,0037 |
| 12/2026 | projetado | 0,37 | 10/22 (25/12 feriado) | 1,0037^(10/22) = 1,0016801243016 |

Fator IPCA = **1,0136305087556**; DU 50, corridos 75; fator real 1,06^(50/252) = **1,0116283813225**;
fator total 1,0254173908317.

| Campo | Esperado |
|---|---|
| GrossAmount | **1.025,42** |
| GrossYield | 25,42 |
| IofRate / IofAmount | 0 / 0,00 |
| TaxRate / TaxAmount | 22,5 / **5,72** |
| NetAmount | **1.019,70** |
| ElapsedDays / BusinessDays | 75 / 50 |
| IsEstimated / IsProjected | **true / true** |

Com a média sem arredondar (0,373571%), o bruto daria cerca de 1.025,47: o caso discrimina a D-5.

**6-bis (fronteira da D-3), mesmo título com `today` = 2026-12-01:** novembro passa a estimado
(0,82), dezembro continua projetado. Fator IPCA = 1,0082² × 1,0016801243016 = 1,0181750313118; fator
total 1,0300147588. Bruto **1.030,01**; IR 6,75225 → **6,75**; líquido **1.023,26**; `IsEstimated` e
`IsProjected` true. Com `today` = 2026-11-30, o resultado é o do caso 6.

Fontes: SGS 433 (09/2025 a 11/2026); Calculadora do Cidadão, índices de preços (10/2025–09/2026);
ANBIMA, VNA (precedente da D-5).

### 7. Variação projetada (teste 7)

| # | Janela (12 meses) | Produto | Média sem arredondar | Esperado | Observação |
|---|---|---|---|---|---|
| 7a | Real, 10/2025–09/2026 | 1,0457611967507 | 0,37357136% | **0,37** | Calculadora: 1,04576120 |
| 7b | Sintética: seis meses −2,00 e seis +1,80, de 10/2025 a 09/2026 | 0,99764^6 | −0,11806970% | **−0,12** | A média aritmética daria −0,10: discrimina geométrica e arredondamento negativo |
| 7c | Sintética: seis −0,60 e seis +0,40 | 0,997976^6 | −0,10125126% | **−0,10** | Média negativa simples |
| 7d | 13 meses, 09/2025 (0,48) a 09/2026 | 1,0457611967507 | 0,37357136% | **0,37** | Se o 13º mês entrar: 0,41 (expoente 1/12) ou 0,38 (1/13) |

Em todos, último publicado 09/2026 e janela 10/2025–09/2026.

### 14. CDI acumulado de 2026-09-01 a 2026-10-01 (teste 14)

Cotações do caso 4 da SPEC-F7: 0,051660 em 01, 02, 03, 04, 08, 09, 10, 11, 14, 15 e 16/09; 0,050788
em 17, 18, 21, 22, 23, 24, 25, 28, 29 e 30/09; 07/09 feriado; 21 DU. Com ou sem a cotação de 01/10 no
stub, `effectiveTo` = 2026-10-01.

Acumulado truncado 1,0108167257286023, fator **1,01081673**, percentual **1,081673**,
`EffectiveTo` **2026-10-01**; R$ 1.000 × fator = **R$ 1.010,82**. Calculadora do Cidadão (aba CDI,
consultada em 26/11/2026): índice 1,01081673, 1,081673 %, R$ 1.010,82. Diferença zero.

Prova da D-7: CDB 100% do CDI, R$ 1.000, mesmas datas: bruto **1.010,82**, IOF 0 (30 dias), IR
2,4345 → **2,43**, líquido **1.008,39**, 21 DU, `IsProjected` falso.

### 15. Corte do CDI, stub dos casos 1 a 3 (teste 15)

Última cotação 19/11/2026; dia útil seguinte 23/11/2026.

| `from` | `to` | `EffectiveTo` | Dias usados | Fator | Percentual |
|---|---|---|---|---|---|
| 2026-11-16 | 2026-11-26 | **2026-11-23** (corte) | 16, 17, 18, 19 | 1,00203307 | **0,203307** |
| 2026-11-16 | 2026-11-24 | 2026-11-23 (corte) | idem | 1,00203307 | 0,203307 |
| 2026-11-16 | 2026-11-23 | 2026-11-23 (sem corte) | idem | 1,00203307 | 0,203307 |
| 2026-11-16 | 2026-11-21 (sábado) | **2026-11-21** (sem corte) | idem | 1,00203307 | 0,203307 |
| 2026-11-16 | 2051-01-02 (além da cobertura) | 2026-11-23, sem erro | idem | 1,00203307 | 0,203307 |
| 2026-11-23 | 2026-11-26 | 2026-11-23 | nenhum | — | `DomainException` (período sem cotação) |

Se o acumulado projetasse 23, 24 e 25/11, o percentual daria 0,356058.

### 16, 17, 18 e 21. IPCA acumulado (testes 16, 17, 18 e 21)

Produtos exatos (série 433):

- 01/2025–12/2025 (0,16; 1,31; 0,56; 0,43; 0,26; 0,24; 0,26; −0,11; 0,48; 0,09; 0,18; 0,33):
  1,0426438490457
- 01/2026–09/2026 (0,33; 0,70; 0,88; 0,67; 0,58; 0,16; 0,07; −0,32; 0,82): 1,039513166123
- 01/2026–10/2026: o anterior × 1,0014 = 1,0409684845557

| # | Stub (último publicado) | `from` | `to` | `EffectiveTo` | Fator, 7 casas | Percentual | Calculadora | Com 8 casas (errado) |
|---|---|---|---|---|---|---|---|---|
| 16a | até 12/2025 | 2025-01-10 | 2025-12-20 | 2025-12-20 | 1,0426438 | **4,264380** | 1,04264380 / 4,264380 % | 4,264385 |
| 16b | até 09/2026 | 2026-01-15 | 2026-09-20 | 2026-09-20 | 1,0395132 | **3,951320** | 1,03951320 / 3,951320 % | 3,951317 |
| 17a | até 09/2026 | 2026-01-15 | 2026-11-26 | **2026-09-30** | 1,0395132 | **3,951320** | idem 16b | — |
| 17b | até 09/2026 | 2026-09-10 | 2026-11-26 | **2026-09-30** (> `from`) | 1,0082 | **0,82** | — | — |
| 17c | até 10/2026 | 2026-01-15 | 2026-11-26 | **2026-10-31** | 1,0409685 | **4,096850** | 1,04096850 / 4,096850 % | 4,096848 |
| 17d | até 09/2026 | 2026-01-15 | 2026-09-30 | 2026-09-30 (sem corte) | 1,0395132 | 3,951320 | — | — |
| 18 | até 09/2026 | 2026-08-15 | 2026-08-15 | 2026-08-15 | 0,9968 | **−0,32** | — | — |
| 21 | até 09/2026, `today` 2026-11-05 | 2026-09-01 | 2026-10-20 | 2026-09-30 | 1,0082 | 0,82 | — | outubro, encerrado e não divulgado, não entra |

Em 17b e 18 o valor é numericamente 0,82 e −0,32, mas a escala do decimal faz serializar `0.8200` e
`-0.3200`: o teste de Domain compara por valor; o teste de API (36) é só CDI. A regra de 7 casas
bate nos quatro períodos consultados na Calculadora (01–12/2025, 01–09/2026, 10/2025–09/2026 e
01–10/2026); com 8 casas, três divergem. **A confirmar:** é inferida das saídas da calculadora, não de
metodologia publicada pelo BC.

### 29. Projeção por título: dois aportes do mesmo CDB PreFixed (teste 29)

CDB PreFixed 10% a.a., dois aportes de R$ 1.000 em 01/04/2025, referência 22/04/2025. 13 DU (18/04 e
21/04 feriados), 21 dias corridos. Fator 1,10^(13/252) = **1,0049289023**.

| Campo | Cada item | Total (RN-07) | Recalculado sobre R$ 2.000 (o teste recusa) |
|---|---|---|---|
| InvestedAmount | 1.000,00 | **2.000,00** | 2.000,00 |
| GrossAmount | 1.004,93 | **2.009,86** | 2.009,86 |
| GrossYield | 4,93 | **9,86** | 9,86 |
| IofAmount (dia 21, 30%) | 1,48 | **2,96** | 2,96 |
| TaxAmount (22,5%) | 0,78 | **1,56** | 1,55 |
| NetAmount | 1.002,67 | **2.005,34** | 2.005,35 |

`PositionCount` 2, `ReferenceDate` 2025-04-22, itens com `IsProjected` e `IsEstimated` falsos.

## Validação

O critério da Calculadora do Cidadão é validado com período fechado (01/09/2026 a 01/10/2026), como
o teste 14: a calculadora recusa data final sem cotação publicada ("Índice não disponível"), então um
caso com corte só pode ser conferido nela depois que a cotação do dia `effectiveTo` sair. O IPCA
acumulado é conferido na aba de índices de preços com os períodos do teste 16, o que confirma ou não
a regra de 7 casas da D-10.

## Critérios

Os do ROADMAP, seção F12, marcados só com evidência em `docs/VALIDACAO-F12.md`:

- [ ] A projeção de um CDB 100% do CDI com data futura bate com o caso de referência, calculado com
  a última taxa diária
- [ ] A projeção de um IPCA+ com data futura usa a média geométrica de 12 meses e bate com o caso de
  referência; mês encerrado e não divulgado continua com `isEstimated`
- [ ] Lacuna de cotação anterior à última cotação continua 400, e data posterior à cobertura do
  calendário também
- [ ] O CDI acumulado de um período bate com a Calculadora do Cidadão do Banco Central, com
  diferença de no máximo R$ 0,01 sobre R$ 1.000
- [ ] Acumulado com `to` posterior à última cotação devolve `effectiveTo` em vez de erro
- [ ] Na projeção por título, os totais são iguais à soma dos itens; título sem aportes responde 200
  com zeros, e título inexistente responde 404
- [ ] Só aditivo: o teste do `swagger.json` mostra os campos e endpoints novos e nenhum campo
  existente alterado
- [ ] `dotnet build` sem warning e `dotnet test` verde, com 712 testes ou mais
- [ ] Mutation score do Domain continua em 80% ou mais
