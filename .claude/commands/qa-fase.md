---
description: Roda o QA exploratório de uma fase já verificada, contra a API em execução, e registra os bugs em docs/QA-Fx.md.
argument-hint: F7
---

# QA da fase $ARGUMENTS

Você coordena o QA. Não corrige nada durante o QA.

## Etapa 1 — Roteiro

Se `docs/QA-$ARGUMENTS-ROTEIRO.md` não existir, chame o `qa-exploratorio` com o modelo Opus para
escrever o roteiro a partir da especificação, do ROADMAP, do README e da
`docs/VALIDACAO-$ARGUMENTS.md`, focando no que a validação não cobriu. **PARE** e peça a aprovação
do Matheus para o roteiro.

## Etapa 2 — Ambiente

Siga a seção "Ambiente" do roteiro. Se o Docker não estiver disponível, pergunte ao Matheus se ele
pode abrir o Docker Desktop antes de usar um caminho alternativo.

## Etapa 3 — Execução

Chame o `qa-exploratorio` (Sonnet) com o roteiro. Roteiros independentes podem ir para duas
instâncias em paralelo, mas cada uma escreve numa seção separada do relatório.

## Etapa 4 — Triagem

Para cada `BUG`, confirme que você consegue reproduzir com a requisição registrada. Bug que não se
reproduz vira `RISCO`. Classifique a severidade:

- `alta`: valor financeiro errado, 500, perda ou duplicação de dados
- `média`: comportamento divergente da especificação sem efeito em valores
- `baixa`: mensagem, documentação, contrato

## Etapa 5 — Fechamento

**PARE** e entregue ao Matheus: o veredito, a lista de bugs por severidade e a proposta de
correção. Bugs de severidade alta ou média viram uma rodada de correção pelo `implementador`, com
o teste automatizado escrito antes da correção, seguida de nova execução do roteiro afetado.
Nada é commitado sem a aprovação dele.
