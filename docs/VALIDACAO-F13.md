# Validação da F13 — Contrato v3 para o front

Verificação independente da F13, feita em 27/11/2026. Cada afirmação vem de comando rodado ou arquivo
lido nesta verificação. Nenhum código foi alterado, nada foi commitado nem adicionado ao índice. O
único arquivo criado é este.

- Data: 2026-11-27
- Commit-base: `9174345` (igual a `origin/main`, branch `f13-contrato-front-v3`). A árvore tinha
  alterações não commitadas, esperado porque a fase não foi commitada: **29 entradas** em `git status
  --short` antes deste arquivo (26 modificadas e 3 não rastreadas: `docs/SPEC-F13-EXECUCAO.md`,
  `PositionNetYieldTests.cs` e `PositionNextBracketTests.cs`). Com este arquivo são 30. `git diff
  --cached` vazio. Nenhum `.csproj`, migration, endpoint ou pacote alterado (`git diff --name-only --
  '*.csproj' '*Migrations*'` vazio).
- Fontes: seção F13 de `docs/ROADMAP.md` (7 critérios), seção "Critérios" e "Casos de referência" de
  `docs/SPEC-F13-EXECUCAO.md`, `docs/ESPECIFICACAO.md` (RN-15 a RN-18), `CLAUDE.md`.
- Ambiente: Windows 11, .NET SDK 8.0.x, Docker em execução.

## Baseline e execução

| Verificação | Comando | Resultado |
|---|---|---|
| Build | `dotnet build --no-incremental` | `0 Warning(s)  0 Error(s)` |
| Testes | `dotnet test --no-build` (solution, Docker em execução) | Domain 505, Application 196, Architecture 8, Infrastructure 173, Api 93 = **975**, 0 falhas, 0 ignorados |
| Skip | `grep -rnE "Skip\s*=" tests --include=*.cs` (fora de `obj`) | zero ocorrências |
| Cobertura | `dotnet test tests/FixedIncome.<X>.Tests --configuration Release --settings coverlet.runsettings --collect:"XPlat Code Coverage"` (Domain e Application) | mesmas contagens, 0 falhas |

Contagem contra a linha de base de 863 informada pelo orquestrador (e pela spec): **863 antes, 975
depois** (+112). A contagem informada pelo implementador (975) foi confirmada. Nenhum projeto caiu:

| Projeto | Antes | Depois | Diferença |
|---|---|---|---|
| Domain | 411 | 505 | +94 |
| Application | 184 | 196 | +12 |
| Infrastructure | 173 | 173 | 0 |
| Api | 87 | 93 | +6 |
| Architecture | 8 | 8 | 0 |
| **Total** | **863** | **975** | **+112** |

Testes existentes alterados (`git diff -U0 -- tests | grep "^-[^-]"`): só as remoções da tabela
"Testes existentes que mudam" da spec (listas fechadas de propriedades em `AssetProjectionFlowTests`,
`AssetProjectionSwaggerTests`, `PortfolioSummaryFlowTests`, `PositionProjectionFlowTests` e
`SwaggerContractTests`; o teste de projeção renomeado; e as chamadas e o helper de
`PortfolioSummaryTests`, que ganharam o `AssetId` e os três parâmetros novos nulos). Nenhum valor
esperado foi alterado e nenhuma asserção foi removida sem substituta. Os desvios fora da tabela estão em
"Desvios aceitos".

## Cobertura

Método do `ci.yml`: Release, um projeto por vez, `coverlet.runsettings`, lendo `line-rate` e
`branch-rate` do `<package>` da própria camada no `coverage.cobertura.xml`.

| Camada | Testes | Linha | Branch | Antes (VALIDACAO-F12) |
|---|---|---|---|---|
| Domain | 505 | **97,5%** (`0.9751`) | **100%** (`1`) | 97,1% / 100% |
| Application | 196 | **95,0%** (`0.9499`) | **99,0%** (`0.9901`) | 94,7% / 99,0% |

Meta (Domain e Application acima de 80%): **atendida**.

## Mutation testing do Domain

`dotnet tool restore` e, em `tests/FixedIncome.Domain.Tests`, `dotnet stryker` (config
`stryker-config.json`, `break: 80`, não alterada na fase). Saída: `The final mutation score is 97.15 %`,
código de saída 0, 3 min 1 s, 421 mutantes testados. Relatório em
`tests/FixedIncome.Domain.Tests/StrykerOutput/2026-11-27.17-43-30/` (ignorado pelo Git).

Pontuação por arquivo, lida de `reports/mutation-report.json` (mortos + timeout sobre válidos):

| Arquivo | Sobreviventes | Score do arquivo |
|---|---|---|
| `ValueObjects/IncomeTaxTable.cs` (F13) | 0 | 100% |
| `ValueObjects/IofTable.cs` (F13, `FreeFromDay`) | 0 | 100% |
| `ValueObjects/PositionProjection.cs` (F13, `NetYield`) | 0 | 100% |
| `ValueObjects/PortfolioSummary.cs` (F13, `TotalNetYield`, `ByAsset`, `SumTotals`) | 0 | 100% |
| `Entities/Position.cs` | 2 (L184 e L201, código da F12) | 98,1% |
| `ValueObjects/IndexAccumulation.cs` | 7 (F12) | 87,9% |
| `ValueObjects/MarketData.cs` | 1 (F12) | 97,1% |
| `Common/DecimalMath.cs` | 2 (L71 e L77, preexistentes) | 96,8% |

**Sobreviventes no código novo da F13: nenhum.** Os dois de `Position.cs` são `day < last.Value.Date`
vira `<=` (L184) e `day < market.Today` vira `<=` (L201), trechos da projeção de CDI da F12 que a F13
não tocou (o `git diff` de `Position.cs` só acrescenta `isIofExempt`, `nextTaxRate`, `nextTaxRateDate` e
`iofFreeDate`), e já constavam como equivalentes na VALIDACAO-F12. Os demais sobreviventes também são
da F12 ou anteriores.

Veredito do critério: 97,15% é maior que 80%. **ATENDIDO.**

## Smoke contra a API em execução

Comando: `docker compose up -d --build` (porta 8080, `GET /health` 200). O banco do compose tem 355
aportes de QA anteriores, que poluiriam o consolidado de 22/04/2025. Para os casos de referência, criei o
banco `f13verif` (vazio) no PostgreSQL do compose, apliquei a migration por script SQL idempotente
(`dotnet ef migrations script --idempotent`) e subi um segundo contêiner `f13verif-api` (mesma imagem
recém-construída com o código da fase, porta 8081, importação de cotações desligada). Todas as chamadas
abaixo foram a esse contêiner; títulos e aportes criados pela própria API (`issuer` `Verif F13`). Scripts
`node` em Windows, sem alterar o repositório.

### Casos 1a e LCA

`GET /api/positions/{id}/projection?referenceDate=2025-04-22`, CDB PreFixed 10%, R$ 1.000, aporte
2025-04-01, vencimento 2027-01-01:

```
"grossAmount":1004.93,"taxRate":22.5,"taxAmount":0.78,"netAmount":1002.67,"elapsedDays":21,
"businessDays":13,"iofRate":30,"iofAmount":1.48,"isProjected":false,"netYield":2.67,
"nextTaxRate":20.0,"nextTaxRateDate":"2025-09-29","iofFreeDate":"2025-05-01"
```

Igual ao caso 1a (netYield 2,67; 20,0; 2025-09-29; 2025-05-01), com `20.0` no literal do JSON.

LCA PreFixed 10%, R$ 1.000, aporte 2025-04-01, vencimento 2027-04-01, mesma referência:
`taxRate 0`, `iofRate 0`, `netYield 4.93`, **`nextTaxRate null`, `nextTaxRateDate null`, `iofFreeDate
null`** (os três campos presentes). Igual ao caso 5 (LCA, `d` = 21).

### Caso 16 (consolidado com dois títulos)

A = CDB PreFixed 10% com dois aportes de R$ 1.000 em 2025-04-01; B = LCI PreFixed 10% com R$ 1.500 em
2025-04-02; `GET /api/portfolio/summary?referenceDate=2025-04-22`, banco só com esses três aportes:

| Campo | Resposta | Esperado |
|---|---|---|
| raiz | 3500.00 / 3516.68 / 16.68 / 1.56 / 5.21 / 3509.91, `positionCount 3` | caso 16 |
| `totalNetYield` da raiz | **9.91** | 9,91 |
| `byAsset[0]` (B, `positionCount 1`) | 1500.00 / 1506.82 / 6.82 / 0.00 / 2.25 / 1504.57 / **4.57** | B 1500/1506.82/6.82/0/2.25/1504.57/4.57 |
| `byAsset[1]` (A, `positionCount 2`) | 2000.00 / 2009.86 / 9.86 / 1.56 / 2.96 / 2005.34 / **5.34** | A 2000/2009.86/9.86/1.56/2.96/2005.34/5.34 |
| ordem de `positions` | B (LCI), A, A | B primeiro |
| item B de `positions` | `netYield 4.57`, `nextTaxRate null`, `nextTaxRateDate null`, `iofRate 33`, `iofFreeDate "2025-05-02"` | caso 16 |
| item A de `positions` | `netYield 2.67`, `nextTaxRate 20.0`, `nextTaxRateDate "2025-09-29"`, `iofRate 30`, `iofFreeDate "2025-05-01"` | caso 16 |

`GET /api/assets/{A}/projection?referenceDate=2025-04-22`: `positionCount 2`, 2000.00 / 2009.86 / 9.86 /
1.56 / 2.96 / 2005.34, `totalNetYield 5.34`. `GET /api/assets/{B}/projection?...`: `positionCount 1`,
1500.00 / 1506.82 / 6.82 / 0.00 / 2.25 / 1504.57, `totalNetYield 4.57`. Iguais ao item de `byAsset` do
mesmo título.

Depois de criar mais títulos (ver abaixo), reconsultei o consolidado em 2025-04-22: 10 posições, 9
títulos em `byAsset`. Script sobre a resposta real: para **cada** um dos 9 títulos, `byAsset[i]` é igual
(`positionCount` e os sete totais, por valor) ao de `GET /api/assets/{id}/projection` na mesma data
(9 de 9 `true`), e a soma de `netYield`, `netAmount` e `taxAmount` dos itens de `positions` do título é
igual ao total dele (9 de 9 `true`). `totalNetYield` da raiz 29 e `totalNetAmount − totalInvestedAmount`
29,00.

### Fronteiras de vencimento e das faixas

CDB PreFixed 10%, R$ 1.000, emissão igual ao aporte, via `GET /api/positions/{id}/projection`. A coluna
"Spec" é a linha correspondente em "Casos de referência"; todas bateram.

| Caso | Aporte / vencimento / referência | Resposta (d, TaxRate, NextTaxRate, NextTaxRateDate, IofRate, IofFreeDate) |
|---|---|---|
| 6a-1 | 04-02 / 09-30 / 2025-04-02 | 0, 22.5, 20, **"2025-09-30"** (= vencimento), 0, "2025-05-02" |
| 6a-2 | idem / 2025-09-29 | 180, 22.5, 20, "2025-09-30", 0, null |
| 6a-3 | idem / 2025-09-30 | 181, 20, **null**, null, 0, null |
| 6a-4 | idem / 2025-10-15 (depois do vencimento) | 181, 20, null, null, 0, null |
| 6b-1 | 04-02 / 09-29 (+180) / 2025-04-02 | 0, 22.5, **null**, **null**, 0, "2025-05-02" |
| 6b-3 | idem / 2025-10-15 | 180, 22.5, null, null, 0, null |
| 6c-1 | 06-02 / 07-02 (+30) / 2025-07-01 | 29, 22.5, null, null, **3**, **"2025-07-02"** (= vencimento) |
| 6c-2 | idem / 2025-07-02 | 30, 22.5, null, null, 0, null |
| 6c-3 | idem / 2025-07-15 | 30, 22.5, null, null, 0, null |
| 6d-1 | 06-02 / 07-01 (+29) / 2025-06-23 | 21, 22.5, null, null, 30, **null** |
| 6d-2 | idem / 2025-07-15 | 29, 22.5, null, null, 3, null |

Fronteiras do caso 4 (aporte 2027-09-01, vencimento 2030-07-01; datas por `AddDays`, com 29/02/2028):

| Referência | d | TaxRate | NextTaxRate | NextTaxRateDate |
|---|---|---|---|---|
| 2027-09-01 | 0 | 22.5 | 20 | 2028-02-29 |
| 2028-02-28 | 180 | 22.5 | 20 | 2028-02-29 |
| 2028-02-29 | 181 | 20 | 17.5 | 2028-08-27 |
| 2028-08-26 | 360 | 20 | 17.5 | 2028-08-27 |
| 2028-08-27 | 361 | 17.5 | 15 | 2029-08-22 |
| 2029-08-21 | 720 | 17.5 | 15 | 2029-08-22 |
| 2029-08-22 | 721 | 15 | null | null |

Fronteiras do caso 10 no mesmo título: `d` 1 (2027-09-02) IofRate 96 e `iofFreeDate "2027-10-01"`; `d` 29
(2027-09-30) IofRate 3 e `"2027-10-01"`; `d` 30 (2027-10-01) IofRate 0 e `iofFreeDate null`. Caso 6a-1
com `netYield 0` e os demais com ganho coerente (6a-3: 38.73; 6c-1: 5.7).

### Swagger

`GET /swagger/v1/swagger.json` do contêiner:

- `PositionProjectionResponse`: os 16 campos existentes na mesma ordem e tipo, mais `netYield number/double`
  e, com `nullable: true`, `nextTaxRate number/double`, `nextTaxRateDate string/date`, `iofFreeDate
  string/date`.
- `PortfolioSummaryResponse`: os 11 existentes iguais, mais `totalNetYield number/double` e `byAsset array`
  (`nullable`, como `byAssetType` e `positions` já eram).
- `AssetSummaryResponse` (novo): `assetId string/uuid`, `positionCount integer/int32` e sete campos
  `number/double` (seis totais e `totalNetYield`), nenhum anulável.
- `AssetProjectionResponse`: os 10 existentes iguais, mais `totalNetYield number/double` no fim.
- Comparação com o contrato anterior: `git diff -U0` de `SwaggerContractTests.cs` e
  `AssetProjectionSwaggerTests.cs` só acrescenta itens no fim das listas; nenhuma linha existente
  mudou de nome, tipo ou formato.

### Estado ao final

Contêiner `f13verif-api` removido, banco `f13verif` apagado (`DROP DATABASE`), `docker compose down`
removeu os contêineres e a rede. O banco do compose (volume preservado) não recebeu nenhum dado desta
verificação. `docker ps -a` não lista nenhum contêiner do projeto.

## Critérios do ROADMAP F13, um a um

| # | Critério | Evidência | Resultado |
|---|---|---|---|
| 1 | `netYield` e `totalNetYield` fecham com `líquido − investido` em todos os casos de referência, inclusive com IOF | Domain `PositionNetYieldTests`: `Net_yield_de_cdb_prefixado_com_iof_e_ir_e_o_liquido_menos_o_investido` (caso 1a, 2,67), `Net_yield_de_cdi_projetado_fecha_com_o_liquido_dos_casos_1_e_2_da_f12` (0,94 e 1,03), `Net_yield_e_negativo_no_ipca_mais_com_taxa_real_baixa_num_mes_de_deflacao` (caso 2a, −1,48), `Net_yield_e_zero_no_dia_do_aporte` (2b), `Net_yield_de_lci_paga_iof_e_o_de_lca_nao_paga_e_nenhum_dos_dois_paga_ir` (3,45 e 4,93); `PortfolioSummaryTests.Total_net_yield_e_a_soma_dos_net_yield_dos_itens_e_o_liquido_menos_o_investido` e `Total_net_yield_e_zero_com_a_lista_vazia`. Application: `Projecao_devolve_o_net_yield_como_liquido_menos_investido`, `Resumo_devolve_net_yield_nos_itens_e_total_net_yield_igual_ao_liquido_menos_o_investido`, `Titulo_devolve_total_net_yield_igual_ao_liquido_menos_o_investido_e_a_soma_dos_itens`. Api: `Projecao_devolve_net_yield_como_liquido_menos_investido_no_caso_de_referencia`, `Resumo_devolve_total_net_yield_igual_ao_liquido_menos_o_investido_no_caso_de_referencia`, `Titulo_devolve_total_net_yield...` em `AssetProjectionFlowTests` (literal `"5.34"`). Smoke: 2,67; raiz 9,91 = 3509,91 − 3500; B 4,57; A 5,34; 29 = 29,00. 975 testes verdes | **ATENDIDO** |
| 2 | Fronteiras da RN-16 batem com os casos de referência: dias 180, 181, 360, 361, 720 e 721, título isento e faixa depois do vencimento | Domain `PositionNextBracketTests`: `Proxima_faixa_de_ir_nas_fronteiras_da_tabela_regressiva` (caso 4, inclusive 29/02/2028), `Na_ultima_faixa_de_ir_nao_ha_proxima_faixa`, `Lci_e_lca_nunca_tem_proxima_faixa_de_ir_...` (caso 5), `Proxima_faixa_de_ir_e_devolvida_ate_o_vencimento_e_nula_depois_dele` (caso 6a e 6b), `Tesouro_direto_recebe_...`, `Datas_devolvidas_sao_o_primeiro_dia_da_faixa_seguinte_e_o_primeiro_dia_sem_iof` (invariante D-4); `IncomeTaxTableTests` com `NextBracketFor_devolve_o_primeiro_dia_e_a_aliquota_da_faixa_seguinte_nas_fronteiras`, `NextBracketFor_na_ultima_faixa_nao_devolve_proxima`, `NextBracketFor_de_lci_e_lca_nao_devolve_proxima_faixa`, `NextBracketFor_devolve_a_aliquota_com_a_mesma_escala_da_tabela`, `NextBracketFor_com_prazo_negativo_lanca_a_mesma_excecao_de_dominio_do_rate_for`. Api: `Projecao_devolve_a_proxima_faixa_de_ir_e_o_fim_do_iof_em_literais_e_datas_iso` (`20.0`, ISO), `Projecao_de_lca_devolve_os_tres_campos_presentes_e_nulos`. Smoke: as sete linhas do caso 4, 6a-1 a 6a-4, 6b-1, 6b-3 e a LCA, todas iguais à spec | **ATENDIDO** |
| 3 | Fronteiras da RN-17 batem: dias 29 e 30, LCA, LCI e data depois do vencimento | Domain `PositionNextBracketTests.Data_do_fim_do_iof_nas_fronteiras_dos_dias_0_1_29_e_30` (caso 10, CDB, LCI e LCA), `Data_do_fim_do_iof_e_devolvida_ate_o_vencimento_e_nula_depois_dele` (6c e 6d), `Data_do_fim_do_iof_e_nula_com_menos_de_30_dias_se_e_so_se_o_titulo_nao_paga_iof` (Theory por `AssetType`), `Lci_e_lca_nunca_tem_proxima_faixa_de_ir_e_so_a_lci_recebe_a_data_do_fim_do_iof`; `IofTableTests.FreeFromDay_e_o_dia_30_cuja_aliquota_e_zero_e_o_anterior_ainda_paga`. Smoke: `d` 29 e 30 (6c-1, 6c-2, caso 10), 6c-3 e 6d-1/6d-2 (depois do vencimento, IOF 3, data nula), LCA nula, LCI com `"2025-05-02"` no consolidado | **ATENDIDO** |
| 4 | Em `byAsset`, totais de cada título iguais à soma dos itens de `positions` dele e iguais aos da projeção por título na mesma data | Domain `PortfolioSummaryTests`: `Por_titulo_com_aportes_intercalados_agrupa_na_ordem_da_primeira_aparicao_com_a_soma_de_cada_total`, `Por_titulo_cada_total_somado_entre_os_titulos_e_igual_ao_total_da_raiz`, `Por_titulo_e_vazio_com_a_lista_vazia`, `Por_titulo_com_um_titulo_so_tem_os_mesmos_totais_da_raiz`. Application `GetPortfolioSummaryUseCaseTests`: `Por_titulo_com_data_informada_agrupa_na_ordem_dos_itens_com_os_totais_do_caso_16`, `Por_titulo_sem_data_informada_usa_a_data_padrao_e_tem_os_totais_do_caso_16`, `Por_titulo_tem_os_mesmos_totais_da_projecao_do_titulo_na_mesma_data` (Theory, com e sem data), `Por_titulo_nao_inclui_titulo_cujos_aportes_ficam_todos_depois_da_data_informada`, `Por_titulo_e_vazio_com_a_carteira_vazia`. Api: `Resumo_com_dois_titulos_devolve_by_asset_com_os_nomes_do_contrato_e_os_totais_da_projecao_de_cada_titulo` (PostgreSQL real). Smoke: caso 16 (B, A) e 9 de 9 títulos iguais à projeção por título e à soma dos itens | **ATENDIDO** |
| 5 | Só aditivo: o teste do `swagger.json` mostra os campos novos e nenhum campo existente alterado | Api: `SwaggerContractTests` (listas fechadas com os campos novos só no fim, `AssetSummaryResponse` com tipo e formato), `AssetProjectionSwaggerTests` (`totalNetYield` no fim), `Swagger_mantem_fechado_o_conjunto_de_propriedades_anulaveis_de_cada_schema` (`PositionProjectionResponse` = {iofFreeDate, nextTaxRate, nextTaxRateDate}; `PortfolioSummaryResponse` = {byAsset, byAssetType, positions}; `AssetSummaryResponse` vazio; `AssetProjectionResponse` = {positions}). `git diff -U0 -- src/FixedIncome.Application/DTOs`: só campos acrescentados no fim de cada record. Swagger ao vivo conferido acima | **ATENDIDO** |
| 6 | `dotnet build` sem warning e `dotnet test` verde, com 863 testes ou mais | `dotnet build --no-incremental`: `0 Warning(s)  0 Error(s)`; 975 testes, 0 falhas, 0 ignorados (maior que 863, nenhum projeto caiu) | **ATENDIDO** |
| 7 | Mutation score do Domain continua em 80% ou mais | `dotnet stryker`: `The final mutation score is 97.15 %`, saída 0; zero sobreviventes no código novo | **ATENDIDO** |

## Regras de negócio da fase e o teste que cobre cada uma

| Regra | Teste |
|---|---|
| RN-15, `netYield = netAmount − investedAmount`, com IOF, IR, negativo e zero | `PositionNetYieldTests` (5 testes, ver critério 1) |
| RN-15, `totalNetYield` = soma dos itens = `totalNet − totalInvested`; zero com lista vazia | `PortfolioSummaryTests.Total_net_yield_...` (2 testes); Application (3 testes de `NetYield`/`TotalNetYield`); Api (3 testes) |
| RN-16, faixas `d` 0, 180, 181, 360, 361, 720, 721 e datas por `AddDays` (29/02/2028) | `PositionNextBracketTests.Proxima_faixa_de_ir_nas_fronteiras_da_tabela_regressiva`, `Na_ultima_faixa_de_ir_nao_ha_proxima_faixa`, `Datas_devolvidas_sao_o_primeiro_dia_da_faixa_seguinte_e_o_primeiro_dia_sem_iof`; `IncomeTaxTableTests` (`NextBracketFor_*`) |
| RN-16, nulo para LCI e LCA e com data posterior ao vencimento, igual devolvida | `PositionNextBracketTests.Lci_e_lca_nunca_tem_proxima_faixa_...`, `Proxima_faixa_de_ir_e_devolvida_ate_o_vencimento_e_nula_depois_dele`; Api `Projecao_de_lca_devolve_os_tres_campos_presentes_e_nulos` |
| RN-16, tabela única e escala (`20.0`, `17.5`, `15.0`) e `d` negativo | `IncomeTaxTableTests.NextBracketFor_devolve_a_aliquota_com_a_mesma_escala_da_tabela`, `NextBracketFor_com_prazo_negativo_...`; Api `Projecao_devolve_a_proxima_faixa_de_ir_e_o_fim_do_iof_em_literais_e_datas_iso`; `RateFor` existente inalterado (testes de `IncomeTaxTableTests` sem mudança) |
| RN-17, `IofFreeDate = aporte + 30` com `d` < 30; no dia do aporte também | `PositionNextBracketTests.Data_do_fim_do_iof_nas_fronteiras_dos_dias_0_1_29_e_30`; `IofTableTests.FreeFromDay_...` |
| RN-17, nulo com `d` ≥ 30, LCA e data posterior ao vencimento; LCI e Tesouro recebem | `Data_do_fim_do_iof_e_devolvida_ate_o_vencimento_e_nula_depois_dele`, `Data_do_fim_do_iof_e_nula_com_menos_de_30_dias_se_e_so_se_o_titulo_nao_paga_iof`, `Lci_e_lca_nunca_tem_proxima_faixa_...`, `Tesouro_direto_recebe_...` |
| RN-16 e RN-17 não dependem de cotação nem mudam `IsProjected`/`IsEstimated` | `Datas_do_cdi_projetado_sao_as_do_aporte_e_is_projected_continua_verdadeiro`, `Datas_do_ipca_estimado_e_projetado_sao_as_do_aporte_e_as_marcas_continuam_as_da_f12`, `Datas_do_ipca_com_rendimento_negativo_ou_nulo_saem_mesmo_sem_imposto`; Application `Sem_data_o_aporte_posterior_a_data_padrao_recebe_aporte_mais_181_e_aporte_mais_30`, `Itens_do_resumo_levam_a_proxima_faixa_de_ir_e_o_fim_do_iof_de_cada_titulo` |
| RN-18, `byAsset` por título, ordem da primeira aparição, soma igual à da raiz, vazio com carteira vazia | `PortfolioSummaryTests.Por_titulo_*` (4 testes); Application `Por_titulo_*` (5 testes); Api `Resumo_com_dois_titulos_...` |
| RN-18, igualdade com a projeção por título | Application `Por_titulo_tem_os_mesmos_totais_da_projecao_do_titulo_na_mesma_data` (com e sem data); smoke (9 de 9 títulos) |
| Contrato só aditivo (D-6) | `SwaggerContractTests`, `AssetProjectionSwaggerTests`, `Swagger_mantem_fechado_o_conjunto_de_propriedades_anulaveis_de_cada_schema`, `PositionProjectionFlowTests.Projecao_mantem_os_campos_antigos_...`, `PortfolioSummaryFlowTests` (conjunto fechado de nomes), `AssetProjectionFlowTests.Projecao_do_titulo_devolve_o_contrato_...` |

## Desvios aceitos (já avaliados pelo `revisor-arquitetura`)

Registrados como informados pelo orquestrador e conferidos no `git diff`:

- **(a)** O stub `PositionRepositoryStub.GetByAssetIdAsync` de `GetPortfolioSummaryUseCaseTests` passou de
  `Task.FromResult(_positions)` para `Task.FromResult(_positions.Where(p => p.AssetId == assetId).AsEnumerable())`,
  fiel ao repositório real, necessário ao teste de igualdade do `byAsset` com a projeção por título.
- **(b)** Asserções acrescentadas a testes existentes fora da tabela "Testes existentes que mudam": em
  `AssetProjectionFlowTests` (`totalNetYield` e `netYield`, literal `"5.34"`) e `GetAssetProjectionUseCaseTests`
  (`TotalNetYield` 0 num teste antigo). Nenhum valor esperado mudou.
- **(c)** Encontrado nesta verificação, de mesma natureza e sem efeito em valor: o helper privado
  `CriarTituloEAporteAsync` de `PositionProjectionFlowTests` ganhou o parâmetro opcional `assetType`
  (padrão `"Cdb"`, igual ao literal antigo) para o teste da LCA. Os testes que já o usavam seguem com o mesmo
  título.

## Divergências e observações

- **D-1 (menor, documentação, não bloqueia):** `README.md` linha 1043 ainda diz "863 testes" (agora 975), e
  a tabela de camadas e de cobertura da seção "Testes" segue com os números anteriores (Domain 410 e
  Application 184, por exemplo). A spec não pede essa atualização na P4; fica para o orquestrador, junto
  com a marcação do ROADMAP (critérios e "Próximo passo"), que também é dele.
- O smoke não reproduziu o consolidado **sem** data informada com o stub da spec (última cotação de CDI em
  17/04/2025): esse caso está coberto no Application por
  `Por_titulo_sem_data_informada_usa_a_data_padrao_e_tem_os_totais_do_caso_16` e, no smoke, só há PreFixed
  (sem importação de CDI), onde a data padrão não depende de cotação.

## Veredito

**APROVADA.** Os sete critérios da F13 têm evidência: build sem warning, 975 testes verdes (baseline 863,
nenhum projeto caiu), mutation score do Domain 97,15% sem sobrevivente no código novo, cobertura de
Domain (97,5%) e Application (95,0%) acima de 80%, e os casos de referência 1a, LCA, 16, 4, 5, 6 e 10
conferidos na API em execução, com o contrato do Swagger só aditivo. Nada falta. As pendências de
documentação da seção "Divergências e observações" são do orquestrador e não reprovam a fase.
