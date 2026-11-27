# QA exploratório — F12: Contrato v2 para o front

Executado em 27/11/2026 (sexta), entre 04:28 e 04:37 de Brasília, pelo roteiro aprovado
`docs/QA-F12-ROTEIRO.md`, em duas partes paralelas e isoladas:

- **Parte A** (`docs/QA-F12-parteA.md`): API do Compose (porta 8080), cotações reais, só lidas.
- **Parte B** (`docs/QA-F12-parteB.md`): pilha isolada `qaf12-*` (porta 28112), importação
  desligada, cotações copiadas do Compose e manipuladas entre dois `restaura`.

Leituras, iguais no início e no fim das duas partes: T = 2026-11-27, P = 2026-11-26, L = 2026-11-25
(CDI 0,050788), D = 2026-11-26, M = 2026-10 (0,14), μ = 0,38.

## Veredito

**Sem bugs.** 46 cenários: 37 OK, 0 BUG, 11 RISCO (todos de severidade baixa), B12 e B13 não
executados (a janela de horário não coincidiu) e o sub-caso 6-bis do B06 não executado (exige hoje
em dezembro). Nenhum 500, nenhum valor diferente do recálculo independente, nenhuma marca
`isProjected` ou `isEstimated` fora do esperado.

| Parte | Cenários | OK | BUG | RISCO | Não executado |
|---|---|---|---|---|---|
| A | 31 | 24 | 0 | 7 | 0 (só a abertura do Swagger UI na A28, sem navegador) |
| B | 15 | 13 | 0 | 4 (anotados em cenários OK) | 2 (B12, B13) |

Destaques:

- Calculadora do Cidadão (A10): três períodos de CDI (02/01/2020–25/11/2026, 30/12/2025–05/01/2026,
  02/01/2024–02/01/2025), índice igual em 8 casas, diferença R$ 0,00.
- Coerência entre endpoints: 73 itens iguais campo a campo entre título, posição e resumo (A17); a
  soma dos 89 títulos fecha com o resumo em 5 datas (A18); 253 aportes num título com soma e ordem
  corretas (B10).
- Mudança de comportamento 2 com L = 26/11, 25/11, 24/11 e 19/11 nas três rotas (B01); D-4 com os
  meses certos, inclusive "há 10, falta 2025-11" (B07, lacuna O-1 do Stryker).
- Carga: 600 requisições com 20 clientes simultâneos, todas 200 (B11). Exatidão: bruto igual ao
  fator exato dígito a dígito com aporte de R$ 100 milhões (B14).
- O `index_rate` do Compose ficou intacto (md5 iguais antes e depois, A30 e Parte B).

## Triagem

Sem `BUG`, não houve rodada de correção. O coordenador reproduziu dois riscos na API do Compose
(A01 e A04), com a mesma resposta registrada. Pela decisão 6 do roteiro, os casos sem regra na spec
são `RISCO` de severidade baixa.

| # | Risco | Origem | Severidade | Situação |
|---|---|---|---|---|
| 1 | `01/09/2026` é lido como `MM/dd` (9 de janeiro) em `from`, `to` e `referenceDate`; `2026-9-1`, `2026/09/01` e data com hora também são aceitos | A01, A12 | baixa | aberto |
| 2 | Parâmetro repetido usa o primeiro valor sem aviso | A01, A12 | baixa | aberto |
| 3 | `indexType` com espaços (`%20Cdi%20`) é aceito nas duas rotas | A02 | baixa | aberto |
| 4 | `from == to` num dia com cotação responde "Não há cotação de CDI publicada no período" | A04 | baixa | corrigido |
| 5 | O README do acumulado não diz que o mês de `from` entra no IPCA, ao contrário da listagem de cotações | A07 | baixa | corrigido |
| 6 | A ordem dos aportes do mesmo dia difere entre a projeção por título e o resumo (cada uma estável; a D-12 permite) | A19 | baixa | aberto |
| 7 | O `.http` cria títulos sem prefixo de teste e o README não avisa que, apontado para o 8080, grava na carteira real | A29 | baixa | corrigido |
| 8 | As mensagens de IPCA do resumo e da projeção por título não identificam a posição nem o título, ao contrário do CDI (a spec só manda identificar no CDI) | B07a, B08 | baixa | aberto |
| 9 | O acumulado de CDI com lacuna não traz o aviso da importação | B03 | baixa | aberto |
| 10 | A mesma lacuna de IPCA gera a mensagem da janela para um aporte e a de cotação ausente para outro (as duas citam o mês) | B07c | baixa | aberto |
| 11 | Projeções longas com centenas de aportes passam de 1 s (resumo de 262 posições em 2050-12-30: 2,7 a 3,0 s); data padrão abaixo de 0,15 s | B10 | baixa | aberto |

### Correções antes do commit (27/11/2026, a pedido do Matheus)

- **Risco 4.** No acumulado de CDI, `from == to` responde 400 com "O período de … a … está vazio: no
  CDI a data final não entra no acumulado, então to precisa ser posterior a from.", conferido antes
  da série. RN-13 e D-9 atualizadas. Testes:
  `IndexAccumulationCdiTests.Cdi_com_from_igual_a_to_num_dia_com_cotacao_explica_que_o_periodo_esta_vazio`
  (ajustado), `Cdi_com_from_igual_a_to_sem_nenhuma_cotacao_tambem_explica_que_o_periodo_esta_vazio` e
  `IndexAccumulatedEndpointTests.Acumulado_do_cdi_com_from_igual_a_to_num_dia_com_cotacao_retorna_400_de_periodo_vazio`
  (novos). Fim de semana e `from` depois da última cotação mantêm "Não há cotação de CDI publicada".
- **Risco 5.** O README do acumulado diz que no IPCA entram os meses de competência do mês de `from`
  ao de `to`, inclusive, e que o mês de `from` entra inteiro.
- **Risco 7.** Os títulos do `.http` levam o prefixo `EXEMPLO-`; o cabeçalho do arquivo e o README
  avisam que as requisições gravam no banco da API apontada (8080 é a carteira do Compose).

Observações fora da F12 (não são da fase): o enum `IndexType` do Swagger lista `PreFixed`, que as
rotas de cotação recusam com 400; `PUT /api/assets/{id}` devolve `"rate":13` e o `GET` devolve
`12.000000`.

## Dados criados

- Compose: 20 títulos `QA-F12A` vivos (um deles, `QA-F12A VAZIO`,
  `e971a3ce-59e6-48e2-bb20-51311d2a7cea`, sem aportes) e 32 aportes, com os ids em
  `docs/QA-F12-parteA.md`, seção "Dados criados". Os títulos com aporte não saem pela API (RN-06).
- Parte B: removida com a pilha `qaf12-*` depois da triagem.

## Limites

A execução foi às 04:30 de Brasília, quando o dia de UTC e o de Brasília coincidem: o QA não prova
que a API usa o dia de Brasília, o que fica coberto pelos testes automatizados. A fronteira de dia
não útil para `today` (B13) também ficou só nos testes automatizados (caso 3-ter).
