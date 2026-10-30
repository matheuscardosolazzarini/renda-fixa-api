---
description: Conduz uma fase da v2 (F7 a F10) do início ao fim, com spec revisada por especialista, testes antes do código, revisões e verificação independente.
argument-hint: F7
---

# Executar a fase $ARGUMENTS

Você é o orquestrador. Você não implementa: delega aos agentes de `.claude/agents/`,
confere o que voltou e decide se a fase avança. Siga as etapas na ordem. Uma etapa com
`PARE` só continua depois da resposta do Matheus.

## Quem faz o quê

| Agente | Papel | Quando |
|---|---|---|
| `especialista-renda-fixa` | regra de mercado correta, casos de referência, auditoria de cálculo | specs e implementação com cálculo (F7, e qualquer fase que toque cálculo) |
| `revisor-arquitetura` | camadas, convenções, escopo, qualidade dos testes | toda fase, depois da implementação; em F8 e F9, também na spec |
| `implementador` | testes primeiro, depois código, um passo por vez | cada passo e cada correção |
| `verificador-de-fase` | build, testes, cobertura, smoke, critério a critério | fim da fase |

Ao chamar um agente que vai explorar código, inclua no prompt: "use `graphify query` antes
de buscar ou abrir arquivos, quando `graphify-out/graph.json` existir".

## Etapa 0 — Pré-condições (se alguma falhar, PARE e explique)

1. `$ARGUMENTS` é uma fase de `docs/ROADMAP.md`.
2. A fase anterior está com todos os critérios marcados no ROADMAP. Para a F7, a seção
   "Antes de começar" está toda marcada.
3. Nenhum item de "Decisões em aberto" em `docs/ESPECIFICACAO.md` está pendente para esta
   fase.
4. A árvore está limpa (`git status`) e atualizada com o remoto (`git fetch` e
   `git status -sb`).
5. Linha de base: `dotnet build` e `dotnet test` verdes. Anote a contagem de testes.
6. Se `graphify-out/graph.json` não existir ou estiver desatualizado, rode `graphify update .`.

## Etapa 1 — Spec de execução

Se `docs/SPEC-$ARGUMENTS-EXECUCAO.md` não existir, escreva-a no mesmo formato das
`SPEC-F1` a `SPEC-F6`: Contexto, Fora de escopo, Regras novas, Implementação em passos
P1..Pn, Testes e Critérios. A fonte é a especificação e o ROADMAP. Nada de requisito que não
esteja neles.

## Etapa 2 — Revisão da spec

- Fase com cálculo: `especialista-renda-fixa` em modo revisão.
- F8 e F9: `revisor-arquitetura` revisando a spec.

Incorpore os achados `bloqueante` e `importante`. Os que você não incorporar, liste com o
motivo.

## Etapa 3 — Casos de referência (só fases com cálculo)

`especialista-renda-fixa` em modo oráculo escreve a seção "Casos de referência" da spec.
Esses valores são os esperados dos testes.

**PARE.** Mostre ao Matheus o resumo da spec, os achados não incorporados e os casos de
referência. Só continue com a aprovação dele.

## Etapa 4 — Implementação

Para cada passo P1..Pn, nesta ordem:

1. `implementador` com o passo indicado.
2. Confira o relatório: build sem warning, testes verdes, contagem não caiu.
3. Se o mesmo passo falhar duas vezes, PARE e reporte o impasse em vez de tentar de novo.

## Etapa 5 — Revisões em paralelo

Chame juntos `revisor-arquitetura` e, em fase com cálculo, `especialista-renda-fixa` em
modo auditoria. Repasse os achados `bloqueante` e `importante` ao `implementador`. No máximo
duas rodadas de correção; se ainda houver bloqueante, PARE e reporte.

## Etapa 6 — Verificação independente

`verificador-de-fase`, informando a contagem de testes da linha de base. Se o veredito for
`REPROVADA`, volte à etapa 5 (uma rodada) e verifique de novo. Reprovada outra vez: PARE e
reporte.

## Etapa 7 — Revisão de fase

Rode a skill `revisao-de-fase` e anexe o relatório.

## Etapa 8 — Fechamento

1. Marque no `docs/ROADMAP.md` os critérios da fase que o `VALIDACAO-$ARGUMENTS.md` aprovou.
   Nenhum critério é marcado sem evidência no arquivo de validação.
2. Atualize a especificação se a fase fechou alguma decisão.
3. `graphify update .`.
4. **PARE.** Entregue ao Matheus: o que foi feito, a contagem de testes antes e depois, o
   veredito da validação, o relatório da revisão de fase e a mensagem de commit sugerida no
   formato `$ARGUMENTS: <descrição>`. Não faça `git add` nem `git commit`.
