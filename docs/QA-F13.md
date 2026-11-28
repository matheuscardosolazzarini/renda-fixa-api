# QA exploratório — F13: Contrato v3 para o front

Executado em 27/11/2026 (sexta), entre cerca de 20:30 e 20:45 de Brasília, pelo roteiro aprovado
`docs/QA-F13-ROTEIRO.md`. Foram duas partes paralelas e isoladas, e nenhuma escreveu no banco do
Compose (decisão 4).

- **Parte A** (`docs/QA-F13-parteA.md`): pilha `qaf13a` (porta 28115), com a imagem da fase. O
  banco era uma cópia completa do Compose: 101 títulos, 355 aportes, cotações reais e importação
  ligada.
- **Parte B** (`docs/QA-F13-parteB.md`): pilha `qaf13`. A API da fase (porta 28113) e a da `main`
  (`9174345`, porta 28114) leem o mesmo banco, com importação desligada. A carteira começou vazia e
  depois recebeu a cópia do Compose. As cotações foram manipuladas, cada cenário entre dois
  `restaura`.

As leituras foram as mesmas no início e no fim das duas partes: T = 2026-11-27, P = 2026-11-26,
L = 2026-11-26, D = 2026-11-27 e M = 2026-10. As âncoras que o roteiro escreveu para D = 2026-11-26
foram recalculadas pela regra.

## Veredito

**Sem bugs.** Dos 27 cenários, 27 deram OK e nenhum deu BUG. Houve 3 riscos, todos de severidade
baixa e anotados em cenários OK, e nenhum cenário ficou sem execução. Nenhuma requisição deu 500, e
nenhum log registrou exceção.

| Parte | Cenários | OK | BUG | RISCO | Não executado |
|---|---|---|---|---|---|
| A | 19 (A00–A18) | 19 | 0 | 2 | 0 |
| B | 8 (B00–B07) | 8 | 0 | 2 (um em comum com a A) | 0 |

Destaques:

- **Só aditivo, provado na resposta (B01).** A comparação cobriu 1793 rotas da `main` contra a
  fase, sobre a cópia da carteira e as 13 rotas de erro. Nenhuma divergeu: status, mensagens e o
  texto de cada campo antigo saíram iguais, e as chaves novas vieram sempre depois das antigas.
- **Escala do `taxRate`.** Saiu idêntica nas duas APIs, com as contagens 22.5=662, 20.0=210,
  17.5=447, 15.0=614 e 0=1059. A dúvida do roteiro sobre `20` → `20.0` fica descartada.
- **Swagger (B02).** Nenhuma rota, schema ou propriedade antiga mudou, e `AssetSummaryResponse` é o
  único schema novo.
- **Regras RN-15 a RN-18 na carteira inteira.** As regras foram recalculadas uma a uma, sem copiar
  a resposta, e deram zero divergências:
  - na A10, em 10 datas, de "sem data" até 2050-12-30, com 355 a 391 itens;
  - na B01 e na B03, sobre até 907 itens e 238 títulos.

  Apareceram os três motivos de `null` da próxima faixa.
- **Coerência entre os três endpoints (A11, B03).** O `byAsset` de cada título bateu com a
  projeção por título. Cada item bateu com a projeção da posição, campo a campo no texto. Nenhum
  título com aporte incluído ficou fora do `byAsset`. As datas testadas foram 6 na Parte A (inclusive
  sem data, com `--todos`) e 3 na Parte B.
- **Casos ao vivo que a validação não cobriu:**
  - CDI com dias projetados, IPCA+ projetado e estimado juntos (B05), Tesouro Direto, LCI e LCA com
    `null` presente;
  - vencimento curto, véspera e aporte no dia do vencimento, que foi aceito (201), conforme a
    decisão 5;
  - posições vencidas, aporte posterior à data padrão e `netYield` negativo com a deflação de
    07/2022 (`netYield = grossYield = -4.91`);
  - datas depois do calendário, `PUT` e `DELETE` de título, e importação parada (B06).
- **Caso 16 da spec (A01).** Os totais conferem também numa carteira suja.
- **Compose intacto.** Depois das duas partes, continuava com 101 títulos e 355 aportes, e a
  `index_rate` continuava idêntica à foto inicial.

## Triagem

Nenhum bug foi encontrado, então não houve rodada de correção. O coordenador reproduziu os riscos
assim:

- os riscos 1 e 2 na pilha `qaf13a`, com as mesmas respostas registradas;
- o risco 3 numa nova medição, já sem a Parte A rodando em paralelo.

Pela decisão 5 do roteiro, os casos sem regra explícita contam como `RISCO` de severidade baixa.

| # | Risco | Origem | Severidade | Situação |
|---|---|---|---|---|
| 1 | O par `nextTaxRate`/`nextTaxRateDate` nulo tem quatro significados: isento (LCI/LCA), já em 15%, faixa depois do vencimento e título vencido. O front só os separa pelo tipo, pelo vencimento e por `elapsedDays`. O README não cita o caso do título vencido. Exemplo: `GET /api/positions/{id vencido}/projection?referenceDate=2025-06-19` traz os três campos `null` | A10, B01 | baixa | corrigido |
| 2 | O zero sai em duas formas: `0.00` em `netYield` e nos totais dos itens, `0` em `totalNetYield` com carteira vazia ou título sem aporte (`GET /api/portfolio/summary?referenceDate=2000-01-01` → `"totalNetYield":0`). Na `main` já conviviam `taxAmount:0` e `iofAmount:0`. Não aparece zero negativo nem notação exponencial | A08, A14, B00 | baixa | aberto |
| 3 | O consolidado ficou mais lento que na `main`: de 8% a 12% sem carga paralela (medição do coordenador) e até 31% com a Parte A rodando. Com 907 itens em 2030-01-02, a resposta passa de 560.775 para 691.848 bytes (+23%, cerca de 145 bytes por item). Fica abaixo do limite de bug do roteiro, que é duas vezes. O risco 11 da F12 não piorou em termos absolutos | B04 | baixa | aberto |

### Correções antes do commit (27/11/2026, a pedido do Matheus)

- **Risco 1.** O README passou a dizer, na explicação de `nextTaxRate`, `nextTaxRateDate` e
  `iofFreeDate`, que uma posição de título vencido devolve os três campos nulos. Os outros três
  significados do `null` já estavam lá. O contrato não mudou.
- Os riscos 2 e 3 ficam em "Pendências herdadas da F13", no `docs/ROADMAP.md`.

Ficaram sem ocorrência dois casos previstos como risco: o empate de ordem em `byAsset` entre
aportes do mesmo dia (a ordem foi estável em 5 chamadas nas duas partes) e totais de mesmo valor
com texto diferente entre `byAsset` e a projeção por título.

### Observações sem severidade

- **O1.** O snapshot do front, `../renda-fixa-web/openapi/renda-fixa-api.json`, está desatualizado
  desde antes da F12 e não tem nenhum campo da F13. Nenhuma propriedade antiga do Swagger mudou, e a
  atualização do snapshot é trabalho do front.
- **O2.** Os exemplos do README mostram `1000` e `2000`, mas a resposta real traz `1000.00` e
  `2000.00`. O formato já era assim antes da F13.
- **O3.** `GET /api/portfolio/summary?referenceDate=2051-01-02` responde 200 nas duas APIs: é o
  comportamento da `main`, fora da F13. A projeção da posição nessa data dá 400 por causa do
  calendário (A09).

## Dados criados e limpeza

- **Parte A:** 28 títulos `QA-F13A` (27 restantes, porque o `DEL` foi removido na A13) e 36
  aportes, só em `qaf13a-db`.
- **Parte B:** 158 títulos e 561 aportes `QA-F13B`, só em `qaf13-db`.

Os ids estão nos relatórios das partes. Os contêineres `qaf13a-*` e `qaf13-*`, os volumes
anônimos dos dois bancos, a rede `qaf13-net` e as etiquetas `qaf13-main` e `qaf13-fase` foram
removidos. A lista de volumes ficou igual à de antes do QA, e o Compose foi parado com
`docker compose stop` (decisão 2). O banco do Compose não recebeu nenhuma escrita.
