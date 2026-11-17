# Validação da tarefa "Contrato para o front (L-01 e L-03)"

Verificação independente da tarefa fora das fases descrita em `docs/SPEC-CONTRATO-FRONT.md`
(aprovada em 16/11/2026), feita em 16/11/2026. Cada afirmação vem de comando rodado ou arquivo
lido nesta verificação. Nenhum código foi alterado, nada foi commitado nem adicionado ao índice. O
único arquivo criado é este.

- Data: 2026-11-16
- Commit-base: `621d0bf` (HEAD da branch `contrato-para-o-front`). A árvore tinha alterações não
  commitadas, esperado porque a tarefa não foi commitada: **14 entradas** em `git status --short
  --untracked-files=all` antes deste arquivo (12 modificadas: `README.md`, `docs/ESPECIFICACAO.md`,
  `docs/ROADMAP.md`, `FixedIncome.Api.http`, 2 DTOs, 2 casos de uso e 4 arquivos de teste; 2 não
  rastreadas: `docs/SPEC-CONTRATO-FRONT.md` e `tests/FixedIncome.Api.Tests/SwaggerContractTests.cs`).
  `git diff --cached` vazio. Com este arquivo são 15.
- Fontes: `docs/SPEC-CONTRATO-FRONT.md` (seção "Critérios", 11 itens), `docs/ROADMAP.md`
  (item em "Manutenção"), `CLAUDE.md`.
- Ambiente: Windows 11, .NET SDK 8.0.x, Docker 29.7.2.

## Baseline e execução

| Verificação | Comando | Resultado |
|---|---|---|
| Build | `dotnet build --no-incremental`; e `dotnet build --configuration Release` | `0 Warning(s)  0 Error(s)` nos dois |
| Testes | `dotnet test --no-build` (solution, Docker em execução) | Domain 332, Application 132, Architecture 8, Infrastructure 172, Api 68 = **712**, 0 falhas, 0 ignorados |
| Skip | `grep -rnE "Skip\s*=" tests --include=*.cs` (fora de `obj`) | zero ocorrências |
| Os mesmos números em Release com cobertura | `dotnet test tests/FixedIncome.<X>.Tests --configuration Release --no-build --settings coverlet.runsettings --collect:"XPlat Code Coverage"` | Domain 332, Application 132, Infrastructure 172, Api 68, todos `Failed: 0, Skipped: 0` |

Contagem de testes: **683 antes (linha de base), 712 depois** (+29). Nenhum projeto caiu:

| Projeto | Antes | Depois | Diferença |
|---|---|---|---|
| Domain | 332 | 332 | 0 |
| Application | 112 | 132 | +20 |
| Infrastructure | 172 | 172 | 0 |
| Api | 59 | 68 | +9 (8 de fluxo e 1 do swagger) |
| Architecture | 8 | 8 | 0 |
| **Total** | **683** | **712** | **+29** |

A contagem relatada pelo implementador (712; Domain 332, Application 132, Infrastructure 172, Api 68,
Architecture 8) foi confirmada. Os 29 testes novos foram listados por `git diff -U0 -- tests` mais
`SwaggerContractTests.cs`: 28 `public async Task` nos quatro arquivos modificados e 1 no arquivo novo.

## Cobertura

Método do `ci.yml` (Release, um projeto por vez, `coverlet.runsettings`), lendo `line-rate` e
`branch-rate` do `<package>` da própria camada no `coverage.cobertura.xml`.

| Camada | Testes | Linha | Branch | Antes (VALIDACAO-F9) |
|---|---|---|---|---|
| Domain | 332 | **96,3%** (`0.9632`) | **100%** (`1`) | 96,3% / 100% |
| Application | 132 | **94,2%** (`0.942`) | **98,9%** (`0.9888`) | 92,6% / 98,9% |
| Infrastructure | 172 | 99,7% (`0.9972`) | 98,4% (`0.9838`) | 99,7% / 98,4% |
| Api | 68 | 96,4% (`0.9644`) | 91,3% (`0.913`) | 96,4% / 91,3% |

Meta (Domain e Application acima de 80%): **atendida**. O Domain não mudou, e a cobertura dele é a
mesma da F9.

## Stryker

`git status --short -- src/FixedIncome.Domain tests/FixedIncome.Domain.Tests` não devolve nada, e
`git diff --stat` não lista nenhum arquivo dessas pastas. O Domain não mudou, então o Stryker **não se
aplica** e não foi rodado, como a spec prevê.

## Critérios da spec, um a um

| # | Critério | Evidência | Resultado |
|---|---|---|---|
| 1 | Projeção devolve `referenceDate` com e sem o parâmetro, e `applicationDate` | Testes `Projecao_com_data_informada_devolve_a_data_de_referencia_e_a_data_do_aporte_no_formato_iso` e `Projecao_sem_data_com_cdi_gravado_devolve_a_data_padrao_como_data_de_referencia` (Api); unitários `Data_informada_e_devolvida_como_data_de_referencia_junto_com_a_data_do_aporte`, `Data_ausente_com_cdi_importado_devolve_o_dia_util_seguinte_a_ultima_cotacao_como_data_de_referencia`, `Data_ausente_sem_cdi_devolve_a_data_de_hoje_em_brasilia_como_data_de_referencia`. Smoke: `GET /api/positions/9cc0adf2-.../projection` sem data devolveu `"referenceDate":"2026-11-16","applicationDate":"2026-11-05"`; com `?referenceDate=2026-11-16` devolveu o mesmo objeto | Atendido |
| 2 | `swagger.json` descreve os campos novos | Teste `Swagger_descreve_os_campos_novos_da_projecao_e_do_resumo_com_o_item_de_positions_apontando_para_projecao_e_titulo`. Smoke em `GET /swagger/v1/swagger.json`: `PositionProjectionResponse.referenceDate` e `applicationDate` = `{"type":"string","format":"date"}`; `PortfolioSummaryResponse.referenceDate` = `date`, `positions` = `array` de `$ref PortfolioPositionResponse`; `PortfolioPositionResponse` = `projection` (`$ref PositionProjectionResponse`) e `asset` (`$ref AssetResponse`) | Atendido |
| 3 | Resumo devolve `referenceDate` com e sem o parâmetro, inclusive com carteira vazia | Application: `Data_informada_e_a_data_de_referencia_da_raiz_do_resumo`, `Data_omitida_com_cdi_importado_devolve_a_data_padrao_como_data_de_referencia_da_raiz`, `Carteira_vazia_sem_data_devolve_a_data_padrao_como_data_de_referencia`, `Carteira_vazia_sem_data_e_sem_cdi_devolve_hoje_em_brasilia_como_data_de_referencia`, `Data_informada_anterior_a_todos_os_aportes_devolve_a_informada_como_data_de_referencia`, `Carteira_vazia_sem_data_com_cdi_e_calendario_vazio_retorna_400_com_a_mensagem_do_calendario` (consequência aceita D-4). Api: `Resumo_com_data_informada_devolve_a_data_de_referencia_no_formato_iso`, `Resumo_de_carteira_vazia_devolve_a_data_de_referencia_em_formato_iso_e_positions_como_array_vazio_com_e_sem_data_informada`. Smoke: sem data `"referenceDate":"2026-11-16"`; com `?referenceDate=2026-10-01` `"referenceDate":"2026-10-01"`. A carteira vazia não foi reproduzida no smoke (o banco do compose tem 316 posições de QA anterior); está provada pelos testes, contra o PostgreSQL | Atendido |
| 4 | Resumo devolve `positions`, cada item com projeção e título | `Cada_item_traz_a_projecao_da_posicao_e_o_titulo_dela_com_duas_posicoes_de_titulos_diferentes`, `Duas_posicoes_do_mesmo_titulo_geram_dois_itens_com_o_mesmo_titulo_e_posicoes_diferentes`, `Itens_saem_na_ordem_em_que_o_repositorio_devolveu_as_posicoes`, `Item_reflete_o_is_estimated_da_projecao_com_o_caso_ipca_de_mes_sem_divulgacao`, `Resumo_com_data_devolve_positions_com_projection_e_asset_...`. Smoke: cada item de `data.positions` com `projection{...,referenceDate,applicationDate}` e `asset{id,name,issuer,assetType,indexType,rate,issueDate,maturityDate}` | Atendido |
| 5 | Soma de cada campo monetário dos itens = total; itens = `positionCount`, com e sem data | Testes `Soma_dos_itens_do_mesmo_cdb_fecha_com_os_totais_e_nao_com_um_consolidado_recalculado` (IR 1,56, líquido 2.005,34), `Soma_dos_itens_de_cdb_lci_e_lca_fecha_com_os_totais`, `Data_informada_deixa_a_posicao_aportada_depois_dela_fora_de_positions_e_o_fechamento_vale_com_as_que_ficaram`, e o helper `AssertFechamento` (seis campos mais a contagem). Smoke (script `node` sobre a resposta real): sem data, 316 itens = `positionCount` 316, seis campos `OK` (ex.: investido 1.032.041,99; líquido 1.147.076,91); com `?referenceDate=2026-10-01`, 279 itens = 279, seis campos `OK` (líquido 1.088.179,82) | Atendido |
| 6 | `projection` do item idêntica à do endpoint de projeção chamado com `projection.referenceDate` do item | Teste `Resumo_com_data_devolve_positions_com_projection_e_asset_e_cada_projection_e_igual_a_do_endpoint_de_projecao`. Smoke (comparação de JSON): item normal == projeção com data (`true`) e sem data (`true`); item com aporte posterior à data padrão (`b3624de0-...`, `referenceDate` e `applicationDate` 2030-12-31) == projeção chamada com `?referenceDate=2030-12-31` (`true`). A mesma posição sem data responde 400: "A data de referência padrão (2026-11-16, ...) é anterior à data de aporte (2030-12-31). Informe referenceDate." (D-8, comportamento preexistente, agora documentado) | Atendido |
| 7 | Nenhum campo existente mudou de nome, tipo ou significado; nenhum valor esperado de teste existente alterado | `git diff -U0 -- tests \| grep -E "^-[^-]"` devolve **3 linhas removidas**, todas do helper privado `CriarAsset` de `GetPortfolioSummaryUseCaseTests.cs` (assinatura e `return`, que ganharam `string name = "Título Teste", string issuer = "Emissor Teste"` com os mesmos padrões): nenhuma asserção nem valor esperado removido ou alterado. `git diff -- src`: os dois DTOs só acrescentam parâmetros no fim do record (`ReferenceDate`, `ApplicationDate` na projeção; `ReferenceDate`, `Positions` no resumo). Testes `Projecao_mantem_os_campos_antigos_com_os_mesmos_nomes_e_acrescenta_so_as_duas_datas` e `Resumo_devolve_exatamente_as_propriedades_do_contrato_no_resumo_no_byAssetType_e_no_item_de_positions` enumeram as propriedades | Atendido |
| 8 | Nenhum arquivo de Domain, migration ou pacote | `git status --short -- src/FixedIncome.Domain tests/FixedIncome.Domain.Tests` vazio; nenhuma entrada de migration, `.csproj`, `.config` ou `Directory.Packages` em `git status --short`; `git diff -- '*.csproj'` com 0 linhas | Atendido |
| 9 | Build sem warning; testes verdes com contagem maior; cobertura acima de 80% | Seções "Baseline e execução" e "Cobertura": 0 warnings e 0 erros; 712 > 683; Domain 96,3% e Application 94,2% | Atendido |
| 10 | `ESPECIFICACAO.md`, `README.md`, `FixedIncome.Api.http` e `ROADMAP.md` atualizados | Ver "Documentação" abaixo: os quatro descrevem o contrato implementado, com a ressalva da contagem de testes no README | Atendido, com divergência (D-1 abaixo) |
| 11 | Nenhuma alteração em `../renda-fixa-web` | `git -C ../renda-fixa-web status --short`: `?? .claude/`, `?? CLAUDE.md`, `?? docs/` (só os que já existiam, não rastreados); nenhum arquivo rastreado modificado | Atendido |

## Documentação

| Arquivo | O que foi conferido contra o código | Resultado |
|---|---|---|
| `docs/ESPECIFICACAO.md` | RN-07 ganha a frase "os totais são a soma dos itens de `positions`..."; seção "Endpoints" descreve `referenceDate`/`applicationDate` da projeção, a posição vencida (D-2), `referenceDate` na raiz do resumo inclusive com carteira vazia e o 400 por calendário (D-4), `positions` como posições incluídas (D-8), as duas datas (D-3), a ordem do repositório (D-6), os totais com estimados (RN-10) e a observação sobre `byAssetType` não somar 100. Bate com `GetPortfolioSummaryUseCase` e com as respostas reais | ok |
| `README.md` | Nova seção "Projeção e resumo da carteira" com exemplos de projeção e resumo. Os campos e a ordem batem com a resposta real do smoke (`referenceDate` e `applicationDate` no fim da projeção; `referenceDate` e `positions` no fim do resumo; `asset` com os oito campos de `AssetResponse`) | ok; ver D-1 |
| `FixedIncome.Api.http` | Comentários nas chamadas de projeção e resumo e nova chamada `GET /api/portfolio/summary` sem `referenceDate` (`git diff` lido) | ok |
| `docs/ROADMAP.md` | Item em "Manutenção" marcado, "Atualizado em 16/11/2026", "Total: 712 testes verdes, antes 683": bate com a contagem medida | ok |

## Regras de negócio e decisões da spec, com o teste que cobre cada uma

| Regra | Teste |
|---|---|
| D-1/D-7, RN-07: totais e itens saem da mesma lista; a soma fecha com arredondamento por projeção (IR 1,56, não 1,55) | `Soma_dos_itens_do_mesmo_cdb_fecha_com_os_totais_e_nao_com_um_consolidado_recalculado`, `Soma_dos_itens_de_cdb_lci_e_lca_fecha_com_os_totais` |
| D-2: data posterior ao vencimento devolve a data pedida com os valores do vencimento (RN-02) | `Data_posterior_ao_vencimento_devolve_a_data_pedida_com_os_valores_do_vencimento` |
| D-3: item com aporte posterior à data padrão tem a data do aporte, a raiz tem a padrão | `Sem_data_o_item_da_posicao_com_aporte_posterior_a_data_padrao_usa_a_data_do_aporte`, `Carteira_mista_sem_data_mantem_a_data_padrao_na_raiz_mesmo_com_aporte_posterior`, `Resumo_sem_data_com_cdi_gravado_devolve_a_data_padrao_na_raiz_e_o_aporte_posterior_entra_com_rendimento_zero_na_data_do_proprio_aporte` |
| D-4: carteira vazia resolve a data padrão, vale hoje em Brasília sem CDI, e 400 com calendário vazio | `Carteira_vazia_sem_data_devolve_a_data_padrao_como_data_de_referencia`, `Carteira_vazia_sem_data_e_sem_cdi_devolve_hoje_em_brasilia_como_data_de_referencia`, `Carteira_vazia_sem_data_com_cdi_e_calendario_vazio_retorna_400_com_a_mensagem_do_calendario` |
| P2.1: resolver a data só depois do 404 | `Titulo_inexistente_sem_data_retorna_404_mesmo_com_cdi_e_calendario_vazio` |
| D-5: item `{ projection, asset }` com os mesmos DTOs | `Cada_item_traz_a_projecao_da_posicao_e_o_titulo_dela_...`, `Swagger_descreve_os_campos_novos_...` |
| D-6: ordem do repositório | `Itens_saem_na_ordem_em_que_o_repositorio_devolveu_as_posicoes` |
| D-8: posição aportada depois da data informada não aparece (R-02) | `Data_informada_deixa_a_posicao_aportada_depois_dela_fora_de_positions_e_o_fechamento_vale_com_as_que_ficaram`, `Data_informada_anterior_a_todos_os_aportes_devolve_a_informada_como_data_de_referencia` |
| RN-10: `isEstimated` do item reflete a projeção | `Item_reflete_o_is_estimated_da_projecao_com_o_caso_ipca_de_mes_sem_divulgacao` |
| Duas posições do mesmo título viram dois itens | `Duas_posicoes_do_mesmo_titulo_geram_dois_itens_com_o_mesmo_titulo_e_posicoes_diferentes` |
| Campos antigos preservados | `Projecao_mantem_os_campos_antigos_...`, `Resumo_devolve_exatamente_as_propriedades_do_contrato_...` |

## Smoke contra a API em execução

Comando: `docker compose up -d --build` na raiz da API (porta 8080), `GET /health` 200, depois
chamadas com `curl`. O banco do compose já tinha 316 posições de QA anteriores (volume
`fixedincome-db-data`). Resultados já citados nos critérios 1 a 6. Em resumo: `GET
/api/portfolio/summary` sem data trouxe `referenceDate` `2026-11-16` e 316 itens; 4 itens (aportes
de 2030 e 2032) com `projection.referenceDate` diferente da raiz, como a D-3 descreve; com
`?referenceDate=2026-10-01`, 279 itens, nenhum com data diferente da raiz.

Estado dos contêineres: ao início, `fixedincome-api` e `fixedincome-db` existiam parados (`Exited
(255)`, há 4 dias). Depois do smoke rodei `docker compose stop`: os dois voltaram a `Exited (0)`,
nenhum removido, o volume e os dados foram preservados. Efeito colateral: o `--build` regerou a
imagem `renda-fixa-api-api:latest` (de 2 semanas para `f290e7a145c8`, agora com o código da tarefa).

## Divergências e observações

- **D-1 (menor, documentação):** `README.md` seção "Testes" (linha 553) ainda diz "659 testes" e a
  tabela (linhas 572 a 576) traz Application 112, Infrastructure 150, Api 57. Os números reais são
  712, 132, 172 e 68. A defasagem já existia (a linha de base de 683 e a Infrastructure com 172 já
  não batiam com o README), e a spec não pede a atualização desses números; fica a recomendação de o
  orquestrador atualizar a tabela com as medidas acima (cobertura: Application 94,2% / 98,9%).
- **O-1:** o OpenAPI continua marcando todos os campos como opcionais (`nullable`), inclusive os
  novos (`positions` aparece com `"nullable":true`). A spec registra isso como pré-existente.
- **O-2:** `ROADMAP.md` e `ESPECIFICACAO.md` citam a carteira vazia com 400 por calendário como
  consequência aceita; coberta por teste unitário, não por teste de API.

## Veredito: APROVADA

Os 11 critérios da spec têm evidência de teste e, onde cabe, de resposta real. Build sem warnings,
712 testes verdes (683 antes), Domain 96,3% e Application 94,2%, Domain, migrations e pacotes
intocados, `../renda-fixa-web` sem alterações. Pendência não bloqueante: a contagem de testes e a
tabela de cobertura do `README.md` (D-1).
