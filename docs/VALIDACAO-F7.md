# Validação da F7 — Indexadores reais e dias úteis

Verificação independente da F7, feita em 30/10/2026. Cada afirmação abaixo vem de comando
rodado ou arquivo lido nesta verificação; nenhum número de relatório de outro agente foi
reaproveitado sem conferência. Nenhum código foi alterado.

- Data: 2026-10-30
- Commit-base: `8d9c389` (HEAD). A árvore tinha alterações não commitadas: 81 entradas em
  `git status --short` (modificadas, removidas e não rastreadas). Isso é esperado: a fase ainda
  não foi commitada.
- `docs/SPEC-F7-EXECUCAO.md` está não rastreada (`??`): ver critério 1.

## Ambiente da verificação

- Docker indisponível: `docker version` falha com "failed to connect to the docker API at
  npipe:////./pipe/dockerDesktopLinuxEngine". `docker compose up` não foi executado.
- O serviço nativo `postgresql-x64-17` está parado e `Start-Service` exige elevação (negado).
  Como o PostgreSQL 17 está instalado, criei um cluster privado com `initdb` na pasta temporária da
  sessão, na porta 55432 (sem tocar na 5432), apliquei as migrations com `dotnet ef database update`
  e rodei a API compilada (`FixedIncome.Api.dll`) contra ele. É PostgreSQL real, não InMemory.
  O cluster foi desligado ao final.
- A rede estava liberada: o SGS e a Calculadora do Cidadão responderam.

## Baseline e execução

| Verificação | Comando | Resultado |
|---|---|---|
| Build | `dotnet build --no-incremental` | `Compilação com êxito. 0 Aviso(s) 0 Erro(s)` |
| Testes | `dotnet test --no-build` | Domain 251, Application 74, Infrastructure 75, Api 33 = **433**, 0 falhas, 0 ignorados |
| Skip | `grep -rn "Skip" tests --include=*.cs` | nenhuma ocorrência |
| Linha de base | `git archive HEAD` em pasta separada + `dotnet test` | Domain 71, Application 28, Infrastructure 9, Api 16 = **124** |

Contagem de testes: **124 antes, 433 depois** (+309). Nenhuma camada caiu: Domain 71 → 251,
Application 28 → 74, Infrastructure 9 → 75, Api 16 → 33.

## Cobertura

Método do CI (`.github/workflows/ci.yml`): `dotnet test <projeto> --settings coverlet.runsettings
--collect:"XPlat Code Coverage"`, um projeto por vez. Cada linha usa o assembly da própria camada
no relatório do projeto de teste correspondente (`line-rate` e `branch-rate` do `<package>` no
`coverage.cobertura.xml`).

| Camada | Testes | Linha | Branch | README hoje (linha / branch) | Diverge? |
|---|---|---|---|---|---|
| Domain | 251 | **95,1%** (862/906) | **97,9%** | 93,6% / 97,2% | sim |
| Application | 74 | **93,8%** (882/940) | **100%** | 93,2% / 100% | sim (linha) |
| Infrastructure | 75 | **87,4%** (624/714) | **86,7%** | 87,4% / 86,7% | não |
| Api | 33 | **93,5%** (348/372) | **88,2%** | 93,6% / 86,1% | sim |

Meta da fase: Domain e Application acima de 80%. Atendida (95,1% e 93,8%).

Para o orquestrador atualizar a tabela do README (seção "Testes"): Domain 95,1% / 97,9%;
Application 93,8% / 100%; Infrastructure 87,4% / 86,7% (já correto); Api 93,5% / 88,2%.

## Critérios do ROADMAP (seção F7)

| # | Critério | Evidência | Resultado |
|---|---|---|---|
| 1 | `SPEC-F7-EXECUCAO.md` commitada antes do primeiro commit de código | O arquivo existe e a linha 3 diz `Status: aprovada (30/10/2026)`. `git status --short` mostra `?? docs/SPEC-F7-EXECUCAO.md`: não está commitada. | **PENDENTE DE AÇÃO DO MATHEUS**: commitar a spec num commit anterior ao do código |
| 2 | `CountBusinessDays` confere em três casos à mão (feriado, virada de ano, Carnaval) | `tests/FixedIncome.Domain.Tests/ValueObjects/BusinessDayCalendarTests.cs:13-26`, `Contagem_de_dias_uteis_segue_os_casos_de_referencia`: 2.2 (20/04 a 24/04, Tiradentes, resultado 3) e 2.3 (04/09 a 08/09, feriado na segunda 07/09, resultado 1); 2.4 (31/12/2025 a 05/01/2026, resultado 2) e 2.5 (31/12/2020 a 04/01/2021, resultado 1); 2.6 (13/02 a 18/02/2026, Carnaval, resultado 1), 2.7 (Quarta de Cinzas, resultado 2) e 2.8 (Carnaval 2020, resultado 1). Erros 2.13/2.14 nas linhas 38 e 52. Os valores esperados coincidem com a tabela da seção 2 da spec (linhas 400-420). `dotnet test` verde. | **ATENDIDO** |
| 3 | O job grava CDI e IPCA; rodar duas vezes no mesmo dia não duplica | Unitário: `ImportIndexRatesUseCaseTests.Executar_duas_vezes_com_os_mesmos_dados_nao_duplica_as_cotacoes` (linha 91) e `IndexRateRepositoryTests.Upsert_com_os_mesmos_dados_duas_vezes_nao_duplica_as_cotacoes` (linha 11); índice único `IX_index_rate_index_type_reference_date` na tabela. Real: API com `IndexRatesImport__Enabled=true` sobre PostgreSQL 17. 1ª execução: `Importação de cotações concluída: 1714 de CDI, 0 de CDI descartadas e 81 de IPCA.` Banco: `Cdi 1714 2020-01-02 a 2026-10-29` e `Ipca 81 2020-01-01 a 2026-09-01`; a série 12 completa do SGS por curl tem 1714 itens. Reinício da API (2ª execução): contagens iguais (1714 e 81), `count(*) - count(distinct(index_type, reference_date))` = 0. Ressalva no achado A abaixo: a 2ª execução do mesmo dia termina com falha registrada, sem duplicar nem corromper. | **ATENDIDO** (com o achado A) |
| 4 | API do BC cai: aplicação de pé, retry com backoff, erro no log | Configuração: `src/FixedIncome.Infrastructure/DependencyInjection.cs:26-28`, `AddHttpClient<IIndexRateProvider, SgsIndexRateProvider>(...).AddStandardResilienceHandler()` (pacote `Microsoft.Extensions.Http.Resilience` 8.10.0, padrões: 3 retries, backoff exponencial de 2 s com jitter, timeout por tentativa de 10 s, circuit breaker). Testes: `SgsIndexRateProviderTests.Erro_5xx_propaga_a_excecao_de_http` (linha 197), `Erro_4xx_diferente_de_404...` (181), `ImportIndexRatesUseCaseTests.Falha_do_provedor_no_cdi_vira_erro_com_log_sem_lancar_excecao_e_nao_impede_o_ipca` (156) e `..._no_ipca_...` (177), `IndexRatesImportJobTests.Excecao_do_caso_de_uso_nao_derruba_o_job_e_e_registrada_em_log` (49) e `Resposta_de_falha_...` (68). Real: o SGS está embutido em `BaseAddress`, sem configuração, então apontei a saída HTTPS para um proxy morto (`HTTPS_PROXY=http://127.0.0.1:9`). O log mostra `Attempt: '0'`, `'1'`, `'2'`, `'3'` com `OnRetry` (3 retries, 4 tentativas) e depois `fail: ...ImportIndexRatesUseCase: Falha ao importar as cotações de Cdi.` com a `HttpRequestException`. O mesmo ciclo roda para o IPCA. Com o BC inalcançável, `GET /api/assets` respondeu 200 e `GET /api/indexes/Cdi?...` respondeu 200 com as 22 cotações já gravadas. Na carga real da 1ª execução, a tentativa 0 estourou o timeout de 10 s (`OnTimeout`) e a tentativa 1 trouxe 200 em 160 ms: retry provado também sobre falha real. | **ATENDIDO**. Ressalva: a configuração de retry/backoff usa os padrões do pacote e não tem teste automatizado; a prova é a execução real acima |
| 5 | CDB 100% do CDI num período real bate com a Calculadora do Cidadão (≤ R$ 0,01 sobre R$ 1.000) | Ver a seção "Critério 5" abaixo. API real: `grossAmount: 1010.82`. Calculadora do BC (POST automatizado): `Valor corrigido na data final R$ 1.010,82`, índice 1,01081673. | **ATENDIDO** (diferença R$ 0,00) |
| 6 | Testes da v1 do PreFixed em base 252, valores à mão, nenhum teste antigo removido sem substituto | Ver a seção "Critério 6" abaixo. | **ATENDIDO** |
| 7 | IOF nos dias 1, 15, 29 e 30; LCI paga IOF mesmo isenta de IR (LCA IOF zero) | `PositionIofProjectionTests.Cdb_prefixado_aplica_iof_regressivo_e_depois_ir_sobre_o_rendimento_menos_o_iof` (linhas 15-35): dias 0, 1, 15, 29, 30 e 31 (casos 5.1, 5.2, 5.4, 5.6, 5.8, 5.10); `Lci_prefixada_paga_iof_e_nao_paga_ir` (linha 61): dias 1, 15, 29, 30, 31 (5.3, 5.5, 5.7, 5.9, 5.11) com `TaxAmount` 0 e `IofAmount` > 0 nos dias 1, 15 e 29; `Lca_tem_iof_zero_mesmo_dentro_dos_trinta_primeiros_dias` (82); `Tesouro_direto_paga_iof_nos_primeiros_trinta_dias` (101). Tabela: `IofTableTests.RateFor_segue_a_tabela_do_decreto_para_os_dias_1_a_30` (30 InlineData, 96% no dia 1, 50% no 15, 3% no 29, 0% no 30), mais dia 0 e dia 31 em diante. O texto do ROADMAP já cita o art. 32, § 2º, V. | **ATENDIDO** |
| 8 | Cada RN nova tem testes; cliente HTTP com `HttpMessageHandler` stub `private sealed`, sem biblioteca de mock | Ver "Regras de negócio" e "Dublês" abaixo. `grep -rniE "Moq|NSubstitute|FakeItEasy|Substitute\.For|new Mock<" tests src` (`.cs` e `.csproj`): zero ocorrências. `SgsIndexRateProviderTests.cs:401`: `private sealed class HttpMessageHandlerStub : HttpMessageHandler`. As 33 classes de dublê encontradas em `tests/` (stubs de repositório, provedor, relógio, logger e caso de uso) são todas `private sealed`; as demais classes de `tests/` são classes de teste ou de dados (`TestMarket`, `AnbimaHolidays`). | **ATENDIDO** |
| 9 | README explica as regras novas e traz exemplo de chamada | `README.md`: seção "Indexadores e cotações" (linha 67) com subseções "Dias úteis", "Pós-fixado em CDI", "IPCA+", "IOF e IR", "Arredondamento" e "Exemplo de chamada" (`GET /api/indexes/Cdi?from=2026-09-14&to=2026-09-15`). A resposta documentada tem o formato que a API devolveu na verificação (`value: 0.051660`). O README diz "433 testes" e a tabela por camada (251/74/75/33) bate com a medição. Os percentuais de cobertura divergem em 3 linhas (ver "Cobertura"). | **ATENDIDO** quanto ao conteúdo. Pendência editorial: atualizar a tabela de cobertura |

## Critério 5 — CDB 100% do CDI contra a Calculadora do Cidadão

Entrada: R$ 1.000, aplicação 2026-09-01, referência 2026-10-01, CDB com `indexType: Cdi`,
`rate: 100`, cotações reais da série 12 do SGS importadas pelo job.

1. **Cotações importadas = SGS.** `GET /api/indexes/Cdi?from=2026-09-01&to=2026-10-01` devolveu 22
   cotações: 11 de 0,051660 (01, 02, 03, 04, 08, 09, 10, 11, 14, 15 e 16/09) e 11 de 0,050788 (17,
   18, 21, 22, 23, 24, 25, 28, 29 e 30/09 mais 01/10). A série 12 pelo curl
   (`dataInicial=01/09/2026&dataFinal=01/10/2026`) tem os mesmos 22 itens. Comparação data a data
   e valor a valor: 0 divergências. `GET /api/indexes/Ipca?from=2026-01-01&to=2026-09-01`
   devolveu as 9 cotações de 2026 (0,33; 0,70; 0,88; 0,67; 0,58; 0,16; 0,07; -0,32; 0,82), iguais às
   da série 433 por curl. `GET /api/indexes/PreFixed?...` devolveu 400, "Só há cotação para CDI e
   IPCA."
2. **Projeção pela API.** `POST /api/assets` e `POST /api/positions` com os dados acima, depois
   `GET /api/positions/{id}/projection?referenceDate=2026-10-01`:
   `grossAmount 1010.82, grossYield 10.82, taxRate 22.5, taxAmount 2.43, netAmount 1008.39,
   elapsedDays 30, businessDays 21, iofRate 0, iofAmount 0.00, isEstimated false`. Bate com o caso 4
   da spec (C6b). O consolidado do mesmo dia devolveu `totalGrossAmount 1010.82`,
   `totalIofAmount 0.00`, `totalNetAmount 1008.39`.
3. **Recálculo independente.** Script em PowerShell com a série 12 real baixada por curl, `decimal`,
   fator diário exato `1 + CDI/100 × pct/100`, acumulado truncado em 16 casas a cada dia, fator
   final em 8 casas: 100% e fim em 01/10: acumulado 1,0108167257286023, fator 1,01081673, bruto
   **R$ 1.010,82**. 110%: fator 1,01190451, bruto R$ 1.011,90 (igual ao caso 4 da spec).
4. **Calculadora do Cidadão do BC.** O formulário (aba CDI) é um POST simples para
   `https://www3.bcb.gov.br/CALCIDADAO/publico/corrigirPeloCDI.do?method=corrigirPeloCDI`, com os
   campos `aba=5`, `dataInicial`, `dataFinal`, `valorCorrecao` e `percentualCorrecao`. Automatizei
   com `curl`:
   - 01/09/2026 a 01/10/2026, R$ 1.000,00, 100%: **índice de correção 1,01081673; valor corrigido
     R$ 1.010,82**.
   - 01/09/2026 a 30/09/2026, mesmos valores: valor corrigido R$ 1.010,30. A API com
     `referenceDate=2026-09-30` devolveu `grossAmount 1010.30` (e meu script, fator 1,01030361).
   - 01/09/2026 a 01/10/2026 com 110%: valor corrigido R$ 1.011,90 (igual ao caso 4 da spec).

   A calculadora exclui a data final (mesma convenção da RN-08), então o resultado esperado da spec
   (R$ 1.010,82) é o que ela mostra. Diferença entre API e calculadora: R$ 0,00, dentro do limite
   de R$ 0,01.

## Critério 6 — Testes da v1 do PreFixed

Método: nomes de métodos de teste de cada arquivo `.cs` em `tests/` no HEAD (`git show HEAD:<arq>`)
contra os atuais. O HEAD tem 132 declarações de teste (Theories contam uma vez). Nenhum arquivo de
teste do HEAD foi removido (todos existem hoje). Cinco nomes do HEAD não existem mais:

| Teste no HEAD | Situação | Substituto |
|---|---|---|
| `PositionTests.Projecao_com_capitalizacao_composta_calcula_bruto_rendimento_imposto_e_liquido` | renomeado | `PositionTests.Projecao_com_capitalizacao_composta_em_base_252_calcula_bruto_rendimento_imposto_e_liquido` (linha 210): 1.099,17 / 99,17 / IR 17,35 / líquido 1.081,82 (caso 1.2, C6b) |
| `GetPortfolioSummaryUseCaseTests.Data_de_referencia_ausente_usa_a_data_atual` | renomeado e dividido | `Data_de_referencia_ausente_usa_o_dia_util_seguinte_a_ultima_cotacao_de_cdi` (linha 53) e `Data_de_referencia_ausente_e_sem_nenhuma_cotacao_de_cdi_usa_a_data_de_hoje_do_relogio` (79). Muda por decisão da RN-07/RN-01 v2 (data padrão = dia útil seguinte à última cotação de CDI) |
| `FixedIncomeAssetTests.ResolveAnnualRate_prefixado_retorna_a_propria_taxa` | removido com o método | A spec manda remover `ResolveAnnualRate` (`SPEC-F7-EXECUCAO.md:173`; critério de aceite item 6). O PreFixed passa a ser coberto por `PositionTests` (casos 1.1 a 1.3, `Rate` como taxa anual em base 252) |
| `FixedIncomeAssetTests.ResolveAnnualRate_cdi_aplica_o_percentual_sobre_o_cdi` | removido com o método | `PositionCdiProjectionTests` (percentual do CDI pelo produtório, casos 3 e 4 a 100% e 110%) |
| `FixedIncomeAssetTests.ResolveAnnualRate_ipca_soma_a_taxa_a_variacao_do_ipca` | removido com o método | `PositionIpcaProjectionTests` (caso 6.1 a 6.4: IPCA composto com taxa real em DU/252) |

Valores novos conferidos à mão no `PositionTests` atual: caso 1.1 (`GrossAmount 100.992,02`,
`GrossYield 992,02`, `TaxAmount 173,60`), caso 1.2 (1.099,17 / 17,35 / 1.081,82), caso 1.3
(`GetPositionProjectionUseCaseTests`: 252 dias úteis, 1.100,00 / 1.082,50) e
`Projecao_e_limitada_ao_vencimento...` com `ElapsedDays 3653` e `BusinessDays 2504`
(`PositionTests.cs:143-144`). Os demais testes da v1 (construção, aporte antes da emissão e
depois do vencimento, isenção, faixas de IR 180/360/720/900) permanecem com o mesmo nome.

`Iof_incide_antes_do_ir_...`: não existe nenhum teste com esse nome em `.cs` nem em `.md` do
repositório (`grep -rn Iof_incide_antes_do_ir`), coerente com a informação do orquestrador de que foi
removido de propósito na revisão por ser tautológico. A ordem IOF antes do IR está coberta pelo caso
5.4 em `PositionIofProjectionTests` (dia 15: rendimento 4,96, IOF 2,48, IR 0,56 = 22,5% de
4,96 − 2,48) e pelas verificações `bruto − IOF − IR = líquido`.

## Regras de negócio da fase e o teste que cobre cada uma

| Regra | Teste |
|---|---|
| RN-01 revisada (PreFixed em base 252, dias úteis) | `PositionTests` (casos 1.1, 1.2, `ElapsedDays` 3653 / `BusinessDays` 2504), `GetPositionProjectionUseCaseTests` (caso 1.3), `PositionProjectionFlowTests` (365 dias, 252 úteis, 1.100,00 / 1.082,50) |
| RN-08 dias úteis (início incluído, fim excluído, calendário ANBIMA, erro fora da cobertura) | `BusinessDayCalendarTests` (casos 2.1 a 2.14, `NextBusinessDay`, `IsBusinessDay`, `BusinessDaysBetween`, cobertura 2020-2029 e dias úteis por ano), `HolidayTests` (invariantes), `AnbimaHolidaySeedBusinessDaysTests` (seed de produção: dias úteis por ano e 2504 em 2020-2029). Banco real: 139 feriados semeados pela migration |
| RN-09 CDI em % do CDI (fator exato, acumulado truncado em 16 casas, fator final em 8, sem cotação = erro) | `PositionCdiProjectionTests` (casos 3 e 4, arredondamento do fator, falta de cotação cita a primeira data, fim de semana/feriado sem cotação não exige), `DecimalMathTests`, `MarketDataTests` |
| RN-10 IPCA+ (meses cheios, mês parcial pro rata em DU, mês estimado, deflação, mês faltante) | `PositionIpcaProjectionTests` (9 testes, casos 6.1 a 6.4 e erros) |
| RN-11 IOF regressivo (tabela 1 a 30 dias, antes do IR, LCA zero, LCI paga) | `IofTableTests`, `PositionIofProjectionTests` |
| RN-07 estendida (`TotalNetAmount = Gross − Tax − Iof`) | `PortfolioSummaryTests.Liquido_fecha_com_bruto_menos_ir_menos_iof_no_caso_de_referencia_7_1` e `Projecoes_do_caso_7_2_fecham_com_bruto_menos_ir_menos_iof`, `GetPortfolioSummaryUseCaseTests`, `PortfolioSummaryFlowTests` |
| Importação (job, upsert idempotente, CDI em dia não útil descartado, falha de provedor) | `ImportIndexRatesUseCaseTests`, `IndexRatesImportJobTests`, `IndexRateRepositoryTests`, `IndexRatesImportJobCompositionTests`, `IndexRatesImportJobDisabledInTestsTests` |
| Cliente SGS (URL, parse, janelas de 10 anos, 404 = vazio, demais erros propagam) | `SgsIndexRateProviderTests` (23 testes, `HttpMessageHandlerStub`) |
| Endpoint `GET /api/indexes/{indexType}?from=&to=` | `IndexesControllerTests` (Api), `GetIndexRatesUseCaseTests` (Application) |
| Invariantes de `IndexRate` e `Holiday` | `IndexRateTests`, `HolidayTests` |

## Definição de pronto

| Item | Evidência | Resultado |
|---|---|---|
| Build sem warnings | `dotnet build --no-incremental`: `0 Aviso(s) 0 Erro(s)` | atendido |
| `dotnet test` verde | 433 aprovados, 0 falhas, 0 ignorados | atendido |
| Pacotes NuGet só os autorizados | `git diff -- '*.csproj'`: Infrastructure ganhou `Microsoft.Extensions.Hosting.Abstractions` 8.0.1 e `Microsoft.Extensions.Http.Resilience` 8.10.0; Application ganhou `Microsoft.Extensions.Logging.Abstractions` 8.0.2 e `DependencyInjection.Abstractions` subiu de 8.0.1 para 8.0.2. Os três estão na tabela "Pacotes autorizados" da spec (linhas 37-39), inclusive a nota sobre a subida de versão. `tests/*.csproj` sem pacote novo | atendido |
| Domain sem referência a outro projeto nem a pacote | `src/FixedIncome.Domain/FixedIncome.Domain.csproj` só tem as propriedades do projeto, sem `ProjectReference` nem `PackageReference` | atendido |
| Sem `DateTime.UtcNow`/`DateTime.Now` em `src` | `grep -rnE "DateTime\.(UtcNow|Now|Today)|DateTimeOffset\.(UtcNow|Now)" src`: a única ocorrência é um comentário em `Application/DependencyInjection.cs:12`; relógio injetado via `TimeProvider` | atendido |
| Nenhuma ponte temporária | `IndexRates` (value object), `IndexRatesOptions` e `ResolveAnnualRate` removidos (`git status`: `D`). Busca por `IndexRates\b`, `IndexRatesOptions` e `"IndexRates"` em src, tests, docker-compose.yml, Dockerfile, README e `.github` só acha o `DbSet<IndexRate> IndexRates`, repositório, migration e um teste. Sem `TODO`/`HACK`/`FIXME` | atendido |
| `appsettings.json` sem a seção `IndexRates` | O arquivo só tem `Logging`, `AllowedHosts` e `IndexRatesImport:Enabled` | atendido |
| `docker-compose.yml` e Dockerfile coerentes | Nenhum dos dois referencia `IndexRates`. A API sobe com `IndexRatesImport:Enabled=true` do `appsettings.json` e `ConnectionStrings__DefaultConnection` do compose. Verificação estática: Docker indisponível | atendido estaticamente; não executado |
| Cobertura Domain e Application acima de 80% | 95,1% e 93,8% | atendido |
| CI no GitHub, PR, commit | Fora do alcance do verificador | **PENDENTE DE AÇÃO DO MATHEUS** |

## Achados

**A. (média, recomendado corrigir antes do commit) A importação incremental falha quando o
período tem um só dia.** O SGS responde `400 {"erro":"DataFinal deve ser maior que dataInicial."}`
quando `dataInicial == dataFinal`, nas duas séries (confirmado por curl: série 12 em 29/10 a 29/10 e
30/10 a 30/10; série 433 em 01/09 a 01/09; já `28/10 a 29/10` devolve 200 com 2 itens).
`ImportIndexRatesUseCase` calcula `from = última data + 1` e `to = hoje`. Quando a última cotação
gravada é a de ontem (o caso normal de um job diário), `from == to` e a chamada leva 400.
Evidência: na 2ª execução da API no mesmo dia, o log mostrou
`GET .../bcdata.sgs.12/dados?formato=json&dataInicial=30/10/2026&dataFinal=30/10/2026`, resposta 400,
`fail: ImportIndexRatesUseCase: Falha ao importar as cotações de Cdi. ... 400 ...` e
`warn: IndexRatesImportJob: Importação de cotações terminou com falha`. Os dados não são corrompidos
nem duplicados e o dia seguinte recupera (a janela passa a ter dois dias), mas o job registra falha
na maior parte das execuções e a cotação do dia só entra com um dia de atraso. Os testes unitários
não pegam isso porque o `IndexRateProviderStub` aceita qualquer período; não há teste do caso
`from == to` no provedor (só `Periodo_invertido_devolve_lista_vazia_sem_chamar_o_sgs`). Sugestão:
no provedor, quando `from == to`, consultar a partir do dia anterior e filtrar o resultado, e cobrir
com teste.

**B. (baixa) Log com data ambígua.** `SgsIndexRateProvider` loga `{From}`/`{To}` como `DateOnly` com
a cultura corrente do processo: `SGS série 433 respondeu 404 para 10/01/2026 a 10/30/2026` (formato
mês/dia, enquanto a URL usa dia/mês). Só cosmético.

**C. (baixa) Retry sem teste automatizado.** A resiliência usa os padrões do pacote, sem opções
explícitas. Foi provada em execução real (tentativas 0 a 3 com `OnRetry`), mas nenhum teste trava os
valores de 3 retries e backoff exponencial.

**D. (baixa) README.** A árvore de pastas em "Arquitetura" não lista `FixedIncome.Api.Tests` (já
ficava de fora antes da fase).

## Veredito

**APROVADA COM PENDÊNCIAS.**

Critérios do ROADMAP que este arquivo aprova (o orquestrador pode marcar):

- [x] `CountBusinessDays` confere em três casos verificados à mão (critério 2)
- [x] O job grava CDI e IPCA, e rodar duas vezes no mesmo dia não duplica registros (critério 3; ver achado A)
- [x] Se a API do BC cair, a aplicação continua de pé: retry com backoff e erro registrado no log (critério 4)
- [x] Um CDB 100% do CDI num período real bate com a Calculadora do Cidadão (critério 5; calculadora 1.010,82, API 1.010,82)
- [x] Testes da v1 do PreFixed atualizados para base 252, sem teste antigo removido sem substituto (critério 6)
- [x] IOF testado nos dias 1, 15, 29 e 30, e LCI pagando IOF mesmo isenta de IR (critério 7)
- [x] Cada RN nova tem testes, com o cliente HTTP testado por `HttpMessageHandler` stub `private sealed`, sem biblioteca de mock (critério 8)
- [x] README explica as regras novas e traz um exemplo de chamada (critério 9; atualizar a tabela de cobertura)

Critério que este arquivo não aprova:

- [ ] `SPEC-F7-EXECUCAO.md` commitada antes do primeiro commit de código (critério 1): pendente do Matheus.

Pendências:

1. Matheus: commitar `docs/SPEC-F7-EXECUCAO.md` (e `docs/REVISAO-ESPECIALISTA-F7.md`) antes do commit
   de código; depois o commit da fase, a conferência do CI no GitHub e o PR.
2. Orquestrador: atualizar a tabela de cobertura do README para Domain 95,1% / 97,9%, Application
   93,8% / 100%, Infrastructure 87,4% / 86,7%, Api 93,5% / 88,2%.
3. Decisão do orquestrador e do Matheus sobre o achado A (corrigir agora ou registrar como dívida da
   F9, quando o job ganha teste contra o provedor real). Os demais achados são opcionais.
4. Docker não estava disponível aqui: `docker compose up --build` não foi exercitado. O smoke real
   foi feito com a API compilada sobre um PostgreSQL 17 local.

Contagem de testes na verificação: **124 antes, 433 depois** (Domain 71 → 251, Application 28 → 74,
Infrastructure 9 → 75, Api 16 → 33).

## Correções posteriores à verificação (registro do orquestrador, 30/10/2026)

Depois deste relatório, o orquestrador mandou corrigir os achados A, B e D e atualizar a tabela
de cobertura do README. O verificador não repetiu o smoke completo depois dessas correções; a
evidência abaixo é a do `implementador` e a conferência do orquestrador (`dotnet build` e
`dotnet test`), e a repetição do smoke com a segunda execução do job fica como conferência
sugerida ao Matheus.

- **A (corrigido).** `SgsIndexRateProvider` consulta a partir da véspera quando `from == to` e
  filtra o resultado para `[from, to]`. Chamadas reais ao SGS em 30/10/2026: série 12 de
  29/10 a 29/10 responde 400; de 28/10 a 29/10 responde 200; de 24/10 a 25/10 (sábado e
  domingo) responde 404; série 433 de 01/09 a 01/09 responde 400 e de 31/08 a 01/09 responde 200
  com os meses 08 e 09, por isso o filtro é necessário. O `HttpMessageHandlerStub` agora rejeita
  com 400 qualquer requisição com `dataInicial == dataFinal`: com ele, 8 testes falharam antes da
  correção e passam depois. Testes novos: `Periodo_de_um_dia_*` (4) e
  `Ipca_de_um_dia_no_dia_primeiro_*`.
- **B (corrigido).** Datas do log e da mensagem de erro do provedor em `yyyy-MM-dd`,
  independente da cultura, com teste.
- **D (corrigido).** `FixedIncome.Api.Tests` entrou na árvore de pastas do README.
- **Cobertura do README.** Remedida pelo `implementador` com o método do CI, depois dos testes
  novos: Domain 95,5% / 97,9%; Application 93,1% / 100%; Infrastructure 88,0% / 88,2%; Api
  94,4% / 91,2%. Os números do verificador (acima) diferem em até 0,7 ponto em linha e 3 em
  branch, sem código novo nas camadas Domain, Application e Api que explique; a diferença não foi
  esclarecida. Domain e Application seguem acima de 80% nas duas medições.
- **Contagem final de testes: 439** (Domain 251, Application 74, Infrastructure 81, Api 33),
  `dotnet build` com 0 avisos, conferida pelo orquestrador.
- **C (mantido como dívida).** A configuração de retry usa os padrões do pacote, sem teste
  automatizado que trave 3 retries e backoff exponencial.
