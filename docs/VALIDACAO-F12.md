# Validação da F12 — Contrato v2 para o front

Verificação independente da F12, feita em 27/11/2026. Cada afirmação vem de comando rodado ou arquivo
lido nesta verificação. Nenhum código foi alterado, nada foi commitado nem adicionado ao índice. O
único arquivo criado é este.

- Data: 2026-11-27
- Commit-base: `26eec24` (HEAD da branch `f12-contrato-front-v2`, igual a `origin/main`). A árvore tinha
  alterações não commitadas, esperado porque a fase não foi commitada: **48 entradas** em `git status
  --short` antes deste arquivo (28 modificadas e 20 não rastreadas, entre elas `docs/SPEC-F12-EXECUCAO.md`,
  `IndexAccumulation.cs`, `PositionProjector.cs`, os dois casos de uso e os dois DTOs novos e 13
  arquivos de teste novos). Com este arquivo são 49. `git diff --cached` vazio.
- Fontes: seção F12 de `docs/ROADMAP.md` (9 critérios), `docs/SPEC-F12-EXECUCAO.md` (casos de
  referência e seção "Validação"), `docs/ESPECIFICACAO.md` (RN-12 a RN-14), `CLAUDE.md`.
- Ambiente: Windows 11, .NET SDK 8.0.x, Docker em execução, rede liberada (SGS e Calculadora do
  Cidadão responderam).

## Baseline e execução

| Verificação | Comando | Resultado |
|---|---|---|
| Build | `dotnet build --no-incremental`; e `dotnet build --configuration Release` | `0 Warning(s)  0 Error(s)` nos dois |
| Testes | `dotnet test --no-build` (solution, Docker em execução) | Domain 410, Application 184, Architecture 8, Infrastructure 173, Api 86 = **861**, 0 falhas, 0 ignorados |
| Skip | `grep -rnE "Skip\s*=" tests --include=*.cs` (fora de `obj`) | zero ocorrências |
| Cobertura | `dotnet test tests/FixedIncome.<X>.Tests --configuration Release --no-build --settings coverlet.runsettings --collect:"XPlat Code Coverage"` (Domain e Application) | mesmas contagens, `Failed: 0, Skipped: 0` |

Contagem de testes, contra a linha de base de 26/11/2026 informada pelo coordenador: **712 antes, 861
depois** (+149). Nenhum projeto caiu:

| Projeto | Antes | Depois | Diferença |
|---|---|---|---|
| Domain | 332 | 410 | +78 |
| Application | 132 | 184 | +52 |
| Infrastructure | 172 | 173 | +1 |
| Api | 68 | 86 | +18 |
| Architecture | 8 | 8 | 0 |
| **Total** | **712** | **861** | **+149** |

A contagem do coordenador (861) foi confirmada. Testes existentes alterados: `git diff -U0 -- tests |
grep "^-[^-]"` lista só remoções da tabela "Testes existentes que mudam" da spec (construtores de
`MarketData` e `MarketDataLoader` com a assinatura nova, o helper `TestMarket.Create`, as duas
listas fechadas de propriedades de API que ganharam `isProjected`, o teste de loader renomeado e a
assinatura do helper de `PortfolioSummaryTests`). Nenhum valor esperado foi alterado nem asserção
removida sem substituta.

## Cobertura

Método do `ci.yml`: Release, um projeto por vez, `coverlet.runsettings`, lendo `line-rate` e
`branch-rate` do `<package>` da própria camada no `coverage.cobertura.xml`.

| Camada | Testes | Linha | Branch | Antes (VALIDACAO-CONTRATO-FRONT) |
|---|---|---|---|---|
| Domain | 410 | **97,1%** (`0.9711`) | **100%** (`1`) | 96,3% / 100% |
| Application | 184 | **94,7%** (`0.9467`) | **99,0%** (`0.99`) | 94,2% / 98,9% |

Meta (Domain e Application acima de 80%): **atendida**. Infrastructure e Api não foram medidas: a spec
não pede.

## Mutation testing do Domain

`dotnet tool restore` (dotnet-stryker 4.16.0) e, em `tests/FixedIncome.Domain.Tests`, `dotnet stryker`
(config `stryker-config.json`, `break: 80`). Saída: `The final mutation score is 96.96 %`, código de
saída 0, 2 min 46 s, 395 mutantes testados. O relatório ficou no lugar habitual,
`tests/FixedIncome.Domain.Tests/StrykerOutput/2026-11-27.03-46-38/` (ignorado pelo Git, como as pastas
anteriores). Nenhum arquivo de `StrykerOutput` fora dessa pasta.

Pontuação por arquivo, lida de `reports/mutation-report.json` (mortos + timeout sobre válidos):

| Arquivo | Sobreviventes | Score do arquivo |
|---|---|---|
| `ValueObjects/IndexAccumulation.cs` (novo) | 7 | 87,0% |
| `ValueObjects/MarketData.cs` | 1 | 97,1% |
| `Entities/Position.cs` | 2 | 97,8% |
| `ValueObjects/PortfolioSummary.cs` | 0 | 100% |
| `Common/DecimalMath.cs` | 2 (linhas 71 e 77, preexistente) | 96,8% |

Sobreviventes dos arquivos da fase, com a leitura de cada um:

| Arquivo e linha | Mutação | Leitura |
|---|---|---|
| `IndexAccumulation.cs` L23, L60 e L95 | remoção de `ArgumentNullException.ThrowIfNull(market)` | Guarda de argumento nulo. Sem consequência de regra |
| `IndexAccumulation.cs` L29 | `to < cutoff` vira `to <= cutoff` | Equivalente: com `to == cutoff` os dois lados dão o mesmo `effectiveTo` |
| `IndexAccumulation.cs` L33 (duas mutações) | `from < effectiveTo` vira `<=`, e a condição vira `true` | `<=` é equivalente (intervalo `[from, from)` vazio). Com `true`, o caso `from` depois do fim efetivo cairia no erro do calendário em vez da mensagem de "período sem cotação": os testes de `from` depois da última cotação só afirmam `DomainException`, sem a mensagem. Lacuna de teste pequena, sem efeito no status HTTP (400 nos dois) |
| `IndexAccumulation.cs` L73 | `toMonth < last.Month` vira `<=` | Equivalente: com meses iguais, o resultado é o mesmo |
| `MarketData.cs` L125 | `firstMissing ??= month` vira `firstMissing = month` | Não equivalente: com dois meses ausentes na janela, a mensagem da D-4 citaria o último e não o primeiro. Os testes da D-4 têm uma lacuna só. Lacuna de teste, sem efeito nos casos de referência |
| `Position.cs` L156 e L173 | `<` vira `<=` em `day < last.Value.Date` e em `day < market.Today` | Equivalentes: um dia igual à última cotação tem valor publicado, e `NextBusinessDay(day) < today` já implica `day < today` |

Veredito do critério: 96,96% é maior que 80%. **ATENDIDO.** Nenhum sobrevivente muda um valor dos
casos de referência.

## Smoke contra a API em execução

Comando: `docker compose up -d --build` na raiz da API (porta 8080). `GET /health` respondeu 200.
Os contêineres `fixedincome-api` e `fixedincome-db` existiam parados (`Exited`, há 4 dias); o `up`
recriou os dois. O banco do compose (volume `renda-fixa-api_fixedincome-db-data`, preservado) tinha
dados reais importados: CDI de 2020-01-02 a 2026-11-25 (1.731 cotações) e IPCA de 2020-01 a 2026-10
(82), além de 323 posições e 81 títulos de QA anteriores. "Hoje" no momento do smoke: 27/11/2026
(sexta). O dia útil anterior a hoje é 26/11, a última cotação de CDI é 25/11.

Para cada caso, o dado foi criado pela API (título `Verif F12`, removido ao final) e o esperado foi
recalculado por fora, com a série 433 do SGS baixada por `curl` e script `node` (decimais em BigInt
para o IPCA acumulado, `Math.pow` em ponto flutuante para o IPCA+, só para conferir os centavos).

### Acumulado (RN-13)

| Chamada | Resposta | Esperado |
|---|---|---|
| `GET /api/indexes/Cdi/accumulated?from=2026-09-01&to=2026-10-01` | 200, `effectiveTo 2026-10-01`, `accumulatedPercentage 1.081673` | Calculadora: 1,01081673 (ver abaixo) |
| `...Cdi/accumulated?from=2026-11-16&to=2026-12-31` (`to` futuro) | 200, `"to":"2026-12-31","effectiveTo":"2026-11-26","accumulatedPercentage":0.356058` | corte no dia útil seguinte à última cotação (25/11 mais 1) |
| `...Cdi/accumulated?from=2026-11-16&to=2027-01-31` | 200, `effectiveTo 2026-11-26`, `0.356058` | idem |
| `...Cdi/accumulated?from=2026-11-16&to=2051-01-02` (além do calendário) | 200, `effectiveTo 2026-11-26`, `0.356058` | corta sem erro (D-9) |
| `...Cdi/accumulated?from=2026-11-16&to=2026-11-26` (`to == effectiveTo`) | 200, `0.356058` | mesmo resultado do corte |
| `...Cdi/accumulated?from=2026-11-16&to=2026-11-25` | 200, `0.305115` | Calculadora: 1,00305115 |
| `...Cdi/accumulated?from=2026-11-01&to=2026-11-25` | 200, `0.764534` | Calculadora: 1,00764534 |
| `...Cdi/accumulated?from=2026-11-26&to=2026-12-10` | 400, "Não há cotação de CDI publicada no período de 2026-11-26 a 2026-12-10." | período sem cotação |
| `...Cdi/accumulated?from=2026-12-10&to=2026-11-10` | 400, "A data inicial não pode ser posterior à data final." | |
| `.../PreFixed/accumulated?...` e `.../1/accumulated?...` | 400, "Só há cotação para CDI e IPCA." e "O valor do campo 'indexType' é inválido." | |
| `...Cdi/accumulated?to=2026-11-26` | 400, "O campo 'from' é obrigatório." | |
| `...Ipca/accumulated?from=2026-01-15&to=2026-09-20` | 200, `3.951320` | Calculadora 01/2026 a 09/2026: 3,951320 % |
| `...Ipca/accumulated?from=2025-01-10&to=2025-12-20` | 200, `4.264380` | Calculadora 01/2025 a 12/2025: 4,264380 % |
| `...Ipca/accumulated?from=2026-01-15&to=2026-12-31` | 200, `effectiveTo 2026-10-31`, `4.096850` | Calculadora 01/2026 a 10/2026: 4,096850 % |
| `...Ipca/accumulated?from=2026-10-10&to=2027-03-01` | 200, `effectiveTo 2026-10-31`, `0.140000` | variação de 10/2026 |
| `...Ipca/accumulated?from=2026-08-15&to=2026-08-15` | 200, `-0.320000` | caso 18 da spec |
| `...Ipca/accumulated?from=2026-11-05&to=2026-11-26` | 400, "Não há cotação de IPCA publicada no período..." | mês de `from` depois do último publicado |

### Projeção com data futura e lacuna (RN-12)

| Caso | Resposta | Conferência |
|---|---|---|
| CDB 100% CDI, R$ 1.000, aporte 16/11, `referenceDate=2026-11-30` | 200, `grossAmount 1004.58`, `taxAmount 0.48`, `netAmount 1001.67`, `iofRate 53`, `iofAmount 2.43`, `businessDays 9`, `elapsedDays 14`, **`isProjected true`**, `isEstimated false` | 7 dias publicados (16 a 25/11, menos o feriado de 20/11) e 26 e 27/11 projetados com 0,050788: fator `1,00050788^9` = 1,0045802, bruto 1.004,58 |
| o mesmo aporte, sem `referenceDate` | 200, `referenceDate 2026-11-26`, `grossAmount 1003.56`, `netAmount 1000.94`, `isProjected false` | igual ao caso 3a da spec (data padrão não projeta) |
| o mesmo aporte, `referenceDate=2026-11-26` | 200, idêntico, `isProjected false` | |
| IPCA+ 6%, R$ 1.000, aporte 03/11, `referenceDate=2026-12-15` | 200, `grossAmount 1012.30`, `taxAmount 2.77`, `netAmount 1009.53`, `businessDays 29`, `isProjected true`, `isEstimated false` | janela 11/2025 a 10/2026 (0,18; 0,33; 0,33; 0,70; 0,88; 0,67; 0,58; 0,16; 0,07; -0,32; 0,82; 0,14), produto 1,0462836, média 0,3777% arredondada em 0,38; `0,38^(19/19) x 0,38^(10/22) x 1,06^(29/252)` dá 1,0122973, bruto 1.012,30 |
| o mesmo IPCA+, sem data | 200, `referenceDate 2026-11-26`, `grossAmount 1006.92`, `isProjected true`, `isEstimated false` | mudança de comportamento 1 (novembro projetado). Recálculo: fator 1,0069174, bruto 1.006,92 |
| lacuna em 18/11 (linha apagada do banco por um instante, depois restaurada), projeção com `referenceDate=2026-11-30` | 400, "Cotação de CDI ausente para 2026-11-18. A importação de cotações pode estar desligada (IndexRatesImport:Enabled) ou ainda não ter rodado." | lacuna antes da última cotação continua 400 |
| a mesma lacuna, projeção sem data | 400, mesma mensagem | |
| a mesma lacuna, `GET .../Cdi/accumulated?from=2026-11-16&to=2026-11-25` | 400, "Cotação de CDI ausente para 2026-11-18." | |
| importação parada: apaguei por um instante as cotações de 24 e 25/11 (última 23/11, hoje 27/11), `referenceDate=2026-11-30` | 400, "Cotação de CDI ausente para 2026-11-24. A importação de cotações pode estar desligada..." | mudança de comportamento 2: o primeiro dia sem cotação antes do dia útil anterior a hoje continua 400 com o aviso do R-10 |
| título CDI com vencimento 2051, `GET /api/assets/{id}/projection?referenceDate=2051-03-01` e `GET /api/positions/{id}/projection?referenceDate=2051-03-01` | 400, "Data 2051-03-01 fora do período coberto pelo calendário (2020-01-01 a 2050-12-31)." | data além da cobertura do calendário continua 400 |

As linhas apagadas do banco foram reinseridas com o mesmo `id` e o mesmo valor, e o acumulado voltou a
0,305115 e o máximo de CDI a 2026-11-25 com 1.731 linhas.

### Projeção por título (RN-14)

| Caso | Resposta |
|---|---|
| CDB PreFixed 10%, dois aportes de R$ 1.000 em 01/04/2026, `referenceDate=2026-04-22` | 200: `totalInvestedAmount 2000.00`, `totalGrossAmount 2009.86`, `totalGrossYield 9.86`, `totalTaxAmount 1.56`, `totalIofAmount 2.96`, `totalNetAmount 2005.34`, `positionCount 2`, dois itens com `isProjected false`. Os valores são os do caso 29 da spec. Script sobre a resposta real: a soma de cada um dos seis campos monetários dos itens é igual ao total (todos `OK`), e `positionCount` é igual ao tamanho de `positions` |
| título sem aportes, sem data | 200: seis totais 0, `positionCount 0`, `referenceDate 2026-11-26`, `positions []` |
| título sem aportes, `referenceDate=2026-06-01` | 200: zeros, `referenceDate 2026-06-01`, `positions []` |
| id inexistente | 404, "Título com id 00000000-... não encontrado.", `data` nulo |
| id inválido (`abc`) | 400, "O valor do campo 'id' é inválido." |

### Resumo da carteira

`GET /api/portfolio/summary` sem data: 200, raiz com `isProjected true` (último campo), 328 itens, 91
com `isProjected` (IPCA+ com dia útil em novembro), nenhum com `isEstimated`, e a soma dos itens igual
ao total nos seis campos. Com `?referenceDate=2026-11-30`: 200, `isProjected true`, 323 itens (os
aportes posteriores saem), 187 projetados, soma igual ao total nos seis campos.

### Swagger

`GET /swagger/v1/swagger.json`: caminhos `/api/Assets/{id}/projection` e
`/api/Indexes/{indexType}/accumulated` presentes; `IndexAccumulationResponse` (`indexType`, `from`,
`to`, `effectiveTo` do tipo `string/date`, `accumulatedPercentage` `number/double`),
`AssetProjectionResponse` (seis totais, `positionCount`, `referenceDate`, `positions`; **sem**
`isProjected` na raiz), `PositionProjectionResponse` e `PortfolioSummaryResponse` com `isProjected`
(`boolean`) como último campo. Parâmetros: `indexType`, `from` e `to` obrigatórios no acumulado;
`id` obrigatório e `referenceDate` opcional na projeção por título.

### Estado ao final

Os cinco títulos e cinco aportes `Verif F12` foram removidos do banco (`delete` por `issuer`): voltaram
323 posições e 81 títulos. `docker compose down` removeu os contêineres e a rede; o volume
`renda-fixa-api_fixedincome-db-data` foi preservado. A imagem `renda-fixa-api-api:latest` foi
regerada com o código da fase. Os contêineres que estavam parados no início agora não existem mais
(`docker compose up` recria).

## Calculadora do Cidadão (critério da fase)

CDI, aba 5, POST descrito em `docs/VALIDACAO-F7.md` (critério 5):

```
curl -X POST ".../corrigirPeloCDI.do?method=corrigirPeloCDI" --data-urlencode aba=5 \
  --data-urlencode dataInicial=01/09/2026 --data-urlencode dataFinal=01/10/2026 \
  --data-urlencode valorCorrecao=1000,00 --data-urlencode percentualCorrecao=100
-> "Índice de correção no período 1,01081673 ... Valor percentual correspondente ..." e
   "Valor corrigido na data final R$ 1.010,82"
```

A API devolveu `accumulatedPercentage 1.081673` para o mesmo período, ou seja, fator 1,01081673 e, sobre R$
1.000, R$ 1.010,82. **Diferença: R$ 0,00** (limite R$ 0,01). Outros dois períodos conferidos, com a
cotação do último dia já publicada: 16/11/2026 a 25/11/2026, calculadora 1,00305115, API 0,305115; 01/11/2026 a
25/11/2026, calculadora 1,00764534, API 0,764534. A calculadora recusa a data final sem cotação ("Índice não
disponível para 26/11/2026"), como a spec previa.

### Regra de 7 casas do IPCA (D-10, "a confirmar")

IPCA, aba 1 (`corrigirPorIndice.do`, `idIndice=00433IPCA`, formulário em
`exibirFormCorrecaoValores.do?aba=1`). Consultei 17 períodos e comparei com o produtório exato da série
433 baixada do SGS, arredondado de quatro formas (script `node` com BigInt, sem ponto flutuante):

- Os 5 períodos da spec (01/2026 a 09/2026, 01/2025 a 12/2025, 10/2025 a 09/2026, 01/2026 a 10/2026, 08/2026 a
  08/2026) deram na calculadora 3,951320 %, 4,264380 %, 4,576120 %, 4,096850 % e -0,320000 %, iguais aos
  valores da spec.
- 12 períodos novos, escolhidos por mim: 03/2023 a 02/2024, 01/2024 a 12/2024, 06/2024 a 05/2025, 02/2023 a 11/2025,
  04/2024 a 09/2026, 07/2025 a 03/2026, 11/2024 a 04/2026, 05/2023 a 08/2023, 09/2025 a 10/2026, 01/2023 a
  12/2023, 03/2025 a 09/2025, 01/2023 a 10/2026. Percentuais da calculadora: 4,496270; 4,831300; 5,319640;
  13,375650; 12,034720; 3,180400; 7,955390; 0,500620; 5,225190; 4,621110; 2,137880; 19,037520.

Resultado: o fator arredondado em **7 casas** reproduz a calculadora nos **17 de 17** períodos (todos os
percentuais terminam em zero na 7a casa, sinal do arredondamento em 7). Com 8 casas, 14 de 17 divergem na 6a casa
do percentual (exemplos: 03/2023 a 02/2024 daria 4,496274 contra 4,496270; 01/2023 a 10/2026 daria 19,037517
contra 19,037520). Com 9 e 12 casas também diverge. A API real devolveu os mesmos valores da calculadora nos
três períodos consultados por HTTP (3,951320; 4,264380; 4,096850). Conclusão: a regra da D-10 está
**confirmada empiricamente** (17 de 17). Continua sendo inferência das saídas da calculadora, não metodologia
publicada pelo BC; vale deixar a ressalva no comentário do código, onde já está.

## Critérios do ROADMAP F12, um a um

| # | Critério | Evidência | Resultado |
|---|---|---|---|
| 1 | Projeção de CDB 100% do CDI com data futura bate com o caso de referência, com a última taxa diária | Domain: `PositionCdiFutureProjectionTests.Cdi_com_data_futura_usa_a_ultima_cotacao_nos_dias_uteis_sem_cotacao_e_marca_como_projetado` (caso 1: bruto 1.003,56, IOF 2,35, IR 0,27, líquido 1.000,94, `IsProjected` verdadeiro), `Cdi_projetado_usa_o_valor_da_ultima_cotacao_e_nao_o_de_dias_anteriores` (caso 1-bis: 1.003,60 e líquido 1.000,95, prova que usa a última taxa e não a de dias anteriores), `Cdi_projetado_com_110_por_cento_aplica_o_percentual_ao_valor_da_ultima_cotacao` (caso 2), `Cdi_com_referencia_no_dia_util_seguinte_a_ultima_cotacao_nao_projeta_nada` e `Cdi_com_referencia_um_dia_util_depois_projeta_so_o_dia_da_data_padrao` (casos 3a e 3b), `Cdi_com_dia_sem_cotacao_antes_do_dia_util_anterior_a_hoje_lanca_excecao_com_a_primeira_data`, `Cdi_com_todos_os_dias_sem_cotacao_a_partir_do_dia_util_anterior_a_hoje_projeta`, `Cdi_com_hoje_em_dia_nao_util_parte_do_dia_util_anterior_e_projeta` e `Cdi_com_hoje_em_sabado_e_ultima_cotacao_um_dia_util_antes_lanca_excecao_no_dia_sem_cotacao` (3-bis e 3-ter). API: `FutureDateProjectionFlowTests.Projecao_de_cdi_com_data_futura_devolve_is_projected_verdadeiro_com_os_valores_do_caso_de_referencia` (PostgreSQL real). Smoke: 1.004,58 com `isProjected true`, recalculado por fora. 861 testes verdes | **ATENDIDO** |
| 2 | IPCA+ com data futura usa a média geométrica de 12 meses e bate com o caso de referência; mês encerrado e não divulgado continua com `isEstimated` | Domain: `PositionIpcaFutureProjectionTests.Ipca_com_data_futura_estima_o_mes_encerrado_e_projeta_o_mes_de_hoje_e_os_seguintes` (caso 6: bruto 1.025,42, IR 5,72, líquido 1.019,70, 75/50 dias, `IsEstimated` e `IsProjected` verdadeiros), `Ipca_com_hoje_no_primeiro_dia_do_mes_seguinte_passa_o_mes_anterior_de_projetado_a_estimado` (caso 6-bis: 1.030,01, 6,75, 1.023,26), `Ipca_com_hoje_no_ultimo_dia_do_mes_ainda_projeta_o_mes_de_hoje`, `Ipca_em_31_de_dezembro_projeta_dezembro` e `Ipca_em_1_de_janeiro_estima_dezembro_em_vez_de_projetar` (D-3). A média: `MarketDataTests.Variacao_projetada_do_ipca_e_a_media_geometrica_dos_12_meses_em_duas_casas` (casos 7a, 7b, 7c: 0,37; -0,12; -0,10), `Variacao_projetada_do_ipca_usa_so_os_12_meses_da_janela_quando_a_serie_tem_13` (7d). Janela incompleta: `Ipca_projetado_com_11_meses_na_serie_lanca_excecao_com_a_contagem_e_o_primeiro_mes_ausente`, `Ipca_projetado_com_12_meses_e_uma_lacuna_na_janela_lanca_excecao_com_o_mes_ausente`, `Ipca_sem_mes_projetado_no_intervalo_nao_exige_a_janela_de_12_meses`. Smoke: IPCA+ com janela real e média 0,38 deu 1.012,30 e, sem data, 1.006,92, os dois recalculados por fora; `isEstimated` só aparece quando há mês encerrado e não divulgado (testes do caso 6) | **ATENDIDO** |
| 3 | Lacuna anterior à última cotação continua 400, e data posterior à cobertura do calendário também | Domain: `Cdi_com_lacuna_antes_da_ultima_cotacao_lanca_excecao_mesmo_com_o_periodo_terminando_logo_depois` (cenário da D-1), `Data_posterior_a_cobertura_do_calendario_continua_lancando_excecao_do_calendario`. Application: `MarketDataLoaderTests.A_serie_inclui_a_ultima_cotacao_de_cdi_e_os_12_meses_de_ipca_mesmo_com_o_periodo_longe_deles`, `Ultima_cotacao_de_cdi_tres_dias_uteis_antes_de_hoje_retorna_400_com_a_posicao_o_titulo_e_o_aviso_da_importacao` (resumo) e `..._retorna_400_citando_o_primeiro_dia_sem_cotacao` (projeção). Api: `AssetProjectionFlowTests.Projecao_com_data_posterior_a_cobertura_do_calendario_retorna_400_com_data_nulo`. Smoke: 400 "Cotação de CDI ausente para 2026-11-18" (lacuna), 400 "...2026-11-24" (importação parada) e 400 "Data 2051-03-01 fora do período coberto pelo calendário" | **ATENDIDO** |
| 4 | CDI acumulado bate com a Calculadora do Cidadão, diferença de no máximo R$ 0,01 sobre R$ 1.000 | Seção "Calculadora do Cidadão": calculadora 1,01081673 e R$ 1.010,82 contra API 1,081673 %, diferença R$ 0,00; mais dois períodos com diferença zero. Domain: `IndexAccumulationCdiTests.Periodo_de_setembro_devolve_o_percentual_da_calculadora_do_cidadao` e `Acumulado_de_um_milhar_e_igual_ao_bruto_de_um_cdb_100_por_cento_do_cdi_nas_mesmas_datas` (prova da D-7) | **ATENDIDO** |
| 5 | Acumulado com `to` posterior à última cotação devolve `effectiveTo` em vez de erro | Domain: `IndexAccumulationCdiTests.Cdi_com_to_depois_da_ultima_cotacao_termina_no_dia_util_seguinte_a_ela_sem_projetar`, `Chamar_de_novo_com_to_igual_ao_effective_to_devolve_o_mesmo_resultado`; `IndexAccumulationIpcaTests.Ipca_com_to_depois_do_ultimo_mes_publicado_termina_no_ultimo_dia_dele`. Application: `GetIndexAccumulationUseCaseTests.Cdi_com_to_depois_da_ultima_cotacao_retorna_200_com_from_to_e_effective_to` e a do IPCA. Api: `IndexAccumulatedEndpointTests.Acumulado_do_cdi_com_to_depois_da_ultima_cotacao_devolve_effective_to_em_vez_de_erro`. Smoke: `to=2026-12-31`, `2027-01-31` e `2051-01-02` devolveram 200 com `effectiveTo 2026-11-26`; IPCA com `to` futuro devolveu `effectiveTo 2026-10-31` | **ATENDIDO** |
| 6 | Na projeção por título, totais iguais à soma dos itens; sem aportes responde 200 com zeros; inexistente responde 404 | Application: `GetAssetProjectionUseCaseTests.Dois_aportes_do_mesmo_cdb_somam_os_itens_ja_arredondados_e_fecham_com_os_totais` (caso 29: 2.000,00 / 2.009,86 / 9,86 / 1,56 / 2,96 / 2.005,34), `Titulo_sem_aportes_com_data_informada_retorna_200_com_totais_zero_lista_vazia_e_a_data`, `Titulo_sem_aportes_sem_data_retorna_200_com_totais_zero_lista_vazia_e_a_data_padrao`, `Todos_os_aportes_posteriores_a_data_informada_retornam_200_com_totais_zero_e_lista_vazia`, `Titulo_inexistente_retorna_404_mesmo_com_o_calendario_vazio`. Api: `AssetProjectionFlowTests.Projecao_do_titulo_devolve_o_contrato_com_os_totais_do_caso_de_referencia`, `Projecao_de_titulo_inexistente_retorna_404_com_data_nulo`. Infrastructure: `PositionRepositoryTests.Listar_por_titulo_retorna_os_aportes_do_mais_recente_para_o_mais_antigo`. Smoke: totais do caso 29, soma dos itens igual ao total nos seis campos, 200 com zeros e 404 | **ATENDIDO** |
| 7 | Só aditivo: o `swagger.json` mostra os campos e endpoints novos e nenhum existente alterado | Api: `SwaggerContractTests` (lista fechada de campos da projeção e do resumo com `isProjected` acrescentado), `IndexAccumulatedSwaggerTests.Swagger_descreve_a_rota_do_acumulado_com_from_e_to_obrigatorios_e_o_schema_da_resposta`, `AssetProjectionSwaggerTests.Swagger_descreve_a_rota_da_projecao_por_titulo_e_o_schema_sem_is_projected_na_raiz`. `git diff -U0 -- src/FixedIncome.Application/DTOs`: os dois DTOs existentes só ganham `bool IsProjected` no fim do record. Nenhum `.csproj` alterado e nenhuma migration nova em `git status`. Swagger ao vivo conferido acima | **ATENDIDO** |
| 8 | `dotnet build` sem warning e `dotnet test` verde, com 712 testes ou mais | `0 Warning(s)  0 Error(s)` em Debug e Release; 861 testes, 0 falhas, 0 ignorados (seção "Baseline e execução") | **ATENDIDO** |
| 9 | Mutation score do Domain continua em 80% ou mais | `dotnet stryker`: `The final mutation score is 96.96 %`, saída 0 | **ATENDIDO** |

## Regras de negócio da fase e o teste que cobre cada uma

| Regra | Teste |
|---|---|
| RN-12, dia útil de CDI sem cotação depois da última e a partir do dia útil anterior a hoje: usa a última cotação, com o mesmo fator e o percentual do título, e marca `isProjected` | `PositionCdiFutureProjectionTests.Cdi_com_data_futura_usa_a_ultima_cotacao_...` (100%), `Cdi_projetado_usa_o_valor_da_ultima_cotacao_e_nao_o_de_dias_anteriores`, `Cdi_projetado_com_110_por_cento_...`, `Cdi_com_todos_os_dias_sem_cotacao_a_partir_do_dia_util_anterior_a_hoje_projeta` |
| RN-12, antes do dia útil anterior a hoje: falha de importação (D-2), inclusive com `today` em dia não útil | `Cdi_com_dia_sem_cotacao_antes_do_dia_util_anterior_a_hoje_lanca_excecao_com_a_primeira_data`, `Cdi_com_hoje_em_dia_nao_util_parte_do_dia_util_anterior_e_projeta`, `Cdi_com_hoje_em_sabado_e_ultima_cotacao_um_dia_util_antes_lanca_excecao_no_dia_sem_cotacao` |
| RN-12, lacuna antes da última cotação do banco (D-1) e sem nenhuma cotação | `Cdi_com_lacuna_antes_da_ultima_cotacao_lanca_excecao_mesmo_com_o_periodo_terminando_logo_depois`, `Cdi_sem_nenhuma_cotacao_lanca_excecao_mesmo_com_data_futura`; Application `MarketDataLoaderTests.A_serie_inclui_a_ultima_cotacao_de_cdi_e_os_12_meses_de_ipca_...` e `Periodo_sobreposto_a_janela_e_a_ultima_cotacao_nao_gera_cotacao_duplicada` |
| RN-02 antes da RN-12: vencimento antes do primeiro dia sem cotação não projeta | `Vencimento_antes_do_primeiro_dia_sem_cotacao_nao_projeta_mesmo_com_referencia_depois` |
| RN-12, PreFixed nunca é projetado | `Prefixado_com_data_futura_nunca_e_projetado`; Application `Titulo_prefixado_com_data_futura_nao_marca_is_projected` |
| RN-12, mês de IPCA estimado (encerrado) e projetado (mês de hoje e seguintes), D-3 e a virada de ano | `PositionIpcaFutureProjectionTests.Ipca_com_data_futura_estima_o_mes_encerrado_...`, `Ipca_com_hoje_no_primeiro_dia_do_mes_seguinte_...`, `Ipca_com_hoje_no_ultimo_dia_do_mes_...`, `Ipca_em_31_de_dezembro_...`, `Ipca_em_1_de_janeiro_...`, `Ipca_publicado_no_mes_de_hoje_prevalece_sobre_a_projecao` |
| RN-12, D-4 e D-5: média geométrica de 12 meses seguidos, 2 casas, janela incompleta falha | `MarketDataTests.Variacao_projetada_do_ipca_e_a_media_geometrica_dos_12_meses_em_duas_casas`, `..._usa_so_os_12_meses_da_janela_quando_a_serie_tem_13`, `..._com_11_meses_informa_a_contagem_e_o_primeiro_mes_ausente`, `..._com_lacuna_dentro_da_janela_cita_o_primeiro_mes_ausente`, `..._sem_nenhuma_cotacao_publicada_falha_dizendo_que_nao_ha_cotacao`; `Ipca_projetado_com_11_meses_...`, `Ipca_sem_mes_projetado_no_intervalo_nao_exige_a_janela_de_12_meses` |
| RN-12, mudança de comportamento 1 (IPCA+ sem data e com dia útil no mês de hoje) | Application `Sem_data_o_ipca_mais_com_dia_util_no_mes_de_hoje_e_projetado_e_nao_estimado` (projeção) e `..._marca_o_item_e_a_raiz_como_projetados` (resumo); `Referencia_no_primeiro_dia_util_do_mes_de_hoje_nao_projeta_...` nos dois; `Sem_data_com_menos_de_12_meses_de_ipca_e_dia_util_no_mes_de_hoje_retorna_400_com_a_mensagem_da_janela` nos dois |
| RN-12, mudança de comportamento 2 (CDI com a importação em dia projeta; parada, 400 com R-10) | Application `Ultima_cotacao_de_cdi_dois_dias_uteis_antes_de_hoje_projeta_a_data_informada` e `..._projeta_o_resumo_na_data_informada`; `Ultima_cotacao_de_cdi_tres_dias_uteis_antes_de_hoje_retorna_400_...` nos dois; `Data_futura_de_cdi_com_importacao_em_dia_projeta_com_a_ultima_cotacao_e_marca_is_projected`; `Sem_data_a_projecao_de_cdi_usa_so_cotacoes_publicadas_e_nao_marca_is_projected` |
| RN-12, `isProjected` no consolidado (D-6): raiz verdadeira se algum item for | `PortfolioSummaryTests.Raiz_e_projetada_quando_um_item_entre_outros_e_projetado`, `Raiz_nao_e_projetada_quando_nenhum_item_e_projetado`, `Raiz_nao_e_projetada_com_a_lista_vazia`; Application `Data_futura_com_cdi_projetado_marca_o_item_e_a_raiz_e_deixa_o_prefixado_sem_marca`, `Data_futura_com_ipca_marca_o_item_como_estimado_e_projetado_e_a_raiz_como_projetada`, `Sem_data_a_raiz_do_resumo_com_cdi_so_publicado_nao_marca_is_projected` |
| RN-13, CDI acumulado: só publicadas, fator da RN-09, 6 casas, corte (D-9), mensagens de erro | `IndexAccumulationCdiTests` (12 testes, incluindo `Periodo_de_setembro_...`, `Acumulado_de_um_milhar_e_igual_ao_bruto_...`, `Cdi_com_to_depois_...`, `Chamar_de_novo_com_to_igual_ao_effective_to_...`, `Lacuna_de_cdi_antes_da_ultima_cotacao_e_falha_de_importacao`, `Cdi_com_from_igual_a_to_...`, `Cdi_em_fim_de_semana_...`, `Cdi_com_from_fora_da_cobertura_do_calendario_e_rejeitado`); `IndexAccumulationTests` (fator acumulado, truncamento em 16 e fator final em 8 casas, D-7) |
| RN-13, IPCA acumulado: meses de competência, 7 casas (D-10), corte no último dia, ignora estimativa, não consulta o calendário | `IndexAccumulationIpcaTests` (14 testes: `Ipca_de_2025_...`, `Ipca_de_janeiro_a_setembro_de_2026_com_deflacao_em_agosto_...`, `Ipca_com_to_depois_do_ultimo_mes_publicado_termina_no_ultimo_dia_dele`, `Ipca_com_from_igual_a_to_no_meio_de_um_mes_publicado_...`, `Mes_encerrado_e_nao_divulgado_nao_entra_no_acumulado_do_ipca`, `Ipca_nao_consulta_o_calendario_e_aceita_periodo_fora_da_cobertura_dele`). Confirmação externa da regra de 7 casas: seção "Regra de 7 casas" acima |
| RN-13, contrato do endpoint | `GetIndexAccumulationUseCaseTests` (9 testes: `Data_inicial_posterior_a_data_final_retorna_400`, `Indexador_pre_fixado_retorna_400_porque_nao_tem_cotacao`, `Ipca_de_periodo_maior_que_a_janela_de_12_meses_usa_todos_os_meses_do_periodo`, entre outros); `IndexAccumulatedEndpointTests` (7 testes: 200 com `1.081673`, `effectiveTo`, sem `from`, sem `to`, `indexType` numérico, período sem cotação, PreFixed); `IndexAccumulatedSwaggerTests` |
| RN-14, projeção por título (D-11 e D-12) | `GetAssetProjectionUseCaseTests` (9 testes, ver critério 6, mais `Data_informada_exclui_o_aporte_posterior_a_ela`, `Sem_data_o_aporte_posterior_a_data_padrao_e_projetado_na_propria_data_do_aporte`, `So_traz_os_aportes_do_titulo_consultado_na_ordem_do_repositorio`, `Cotacao_de_cdi_ausente_retorna_400_citando_a_posicao_o_titulo_e_o_aviso_da_importacao`); `PositionProjectorTests` (12 testes da classe extraída); `AssetProjectionFlowTests` (4 testes, incluindo a ordem do mais recente para o mais antigo) |
| Carregamento de mercado (D-1 e D-2) | `MarketDataLoaderTests.A_serie_inclui_a_ultima_cotacao_de_cdi_e_os_12_meses_de_ipca_mesmo_com_o_periodo_longe_deles`, `Periodo_sobreposto_a_janela_e_a_ultima_cotacao_nao_gera_cotacao_duplicada`, `O_dia_de_hoje_vem_do_time_provider_no_fuso_de_brasilia`, `Carrega_o_cdi_do_periodo_com_as_duas_pontas_inclusivas_e_alem_delas_so_a_ultima_cotacao` |

## Divergências e observações

- **D-1 (menor, documentação, não bloqueia):** `README.md` seção "Testes" (linha 976) ainda diz "712
  testes", e a tabela de camadas (linhas 994 a 998) traz Domain 332, Application 132, Infrastructure 172, Api
  68, com cobertura de 96,3% / 94,2% / 99,7% / 96,4%. Os números medidos agora são 861, Domain 410,
  Application 184, Infrastructure 173, Api 86, e cobertura de linha de 97,1% (Domain) e 94,7% (Application),
  com branch de 100% e 99,0%. A spec não pede a atualização desses números na P5; fica para o orquestrador.
  O mesmo vale para a marcação do ROADMAP (linhas 265 e 315), que é do orquestrador.
- **O-1 (menor, teste):** sobreviventes do Stryker em `IndexAccumulation.cs` L33 e `MarketData.cs` L125
  indicam duas lacunas de teste sem efeito no contrato: a mensagem de "período sem cotação" para `from`
  depois do fim efetivo não é afirmada (só o tipo da exceção), e a mensagem da D-4 com dois meses ausentes
  não é testada (só com um).
- **O-2:** o `ci.yml` não pôde ser conferido para a F12, porque a fase não foi commitada nem tem PR (o
  critério de pipeline vale a partir da F8 e só quando a fase depende dele; aqui não depende).
- **O-3:** o smoke foi rodado em "hoje" real (27/11/2026, sexta) contra o CDI e o IPCA importados pelo próprio
  job da API, sem dado sintético de cotação. As lacunas foram simuladas apagando e reinserindo linhas de
  `index_rate`, com restauração conferida.

## Veredito: APROVADA

Os 9 critérios do ROADMAP têm evidência de teste e, onde cabe, de resposta HTTP real. Build sem
warning, 861 testes verdes (712 antes, +149, nenhum projeto caiu), Domain 97,1% e Application 94,7% de
cobertura, mutation score do Domain em 96,96%. O CDI acumulado bate com a Calculadora do Cidadão com
diferença de R$ 0,00 sobre R$ 1.000, e a regra de 7 casas do IPCA (D-10) foi confirmada em 17 de 17
períodos consultados na calculadora. Pendências não bloqueantes: a contagem de testes e a tabela de cobertura
do `README.md` (D-1) e as duas lacunas de teste pequenas (O-1).

## Adendo: correções de riscos do QA antes do commit (27/11/2026)

A pedido do Matheus, os riscos 4, 5 e 7 do `docs/QA-F12.md` foram corrigidos depois desta
validação, com o registro na seção "Correções antes do commit" daquele arquivo. Só o risco 4 muda
código: o acumulado de CDI com `from == to` passa a responder 400 com a mensagem de período vazio.
Depois da correção, conferido pelo coordenador: `dotnet build --no-incremental` com 0 warnings e
`dotnet test` com **863** testes (Domain 411, Application 184, Architecture 8, Infrastructure 173,
Api 87), 0 falhas, 0 ignorados. Os números acima (861) são os desta validação, antes do adendo.
