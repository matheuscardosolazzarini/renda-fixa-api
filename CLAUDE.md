# Convenções do projeto

API de controle de investimentos em renda fixa. A especificação técnica está em
`docs/ESPECIFICACAO.md` e é a fonte de verdade — consulte-a antes de implementar
qualquer coisa e não invente requisitos que não estejam nela.

## Fluxo de trabalho

Implemente uma fase por vez, conforme a seção "Fases" da especificação. Ao concluir
uma fase, pare e aguarde revisão antes de avançar. Não antecipe código de fases
seguintes.

Ao final de cada alteração, rode `dotnet build` e `dotnet test` e corrija o que falhar.

A v2 (F7 em diante) segue a ordem e os critérios de `docs/ROADMAP.md` e é conduzida pelo
comando `/executar-fase F7` (ou F8, F9, F10), que coordena os agentes de
`.claude/agents/`. Nenhuma fase da v2 começa com item da seção "Decisões em aberto" da
especificação pendente para ela.

## Idioma

Comentários e mensagens de commit em português. Identificadores — classes, métodos,
variáveis, colunas — em inglês.

Comente apenas o que não é óbvio pelo código: decisão de negócio, motivo de uma
exceção, origem de uma regra tributária. Não comente o que o nome do método já diz.

## Arquitetura

Dependências apontam para dentro: `Api → Application → Domain`, com `Infrastructure`
implementando interfaces declaradas em `Application`. O projeto `Domain` não referencia
nenhum outro projeto nem pacote de infraestrutura.

Regras de cálculo — rentabilidade e apuração de IR — ficam no domínio, não nos casos
de uso.

## Padrões de código

**Controller** herda `ControllerBase` e retorna `StatusCode(response.StatusCode, response)`.
Não contém regra de negócio: valida a rota, chama o caso de uso, devolve a resposta.

**Resposta** encapsulada em `ApiResponse<T>`, com fábricas para Success, BadRequest,
NotFound e InternalError.

**Caso de uso** implementa uma interface, recebe as dependências por construtor e
retorna `ApiResponse<T>`.

**Entidade** herda `BaseEntity`, propriedades em PascalCase. Invariantes validadas no
próprio domínio, lançando exceção de domínio — nunca deixando a entidade em estado
inválido.

**Configuração de entidade** via `IEntityTypeConfiguration<T>`, com `ToTable`,
`HasKey` e `HasColumnName` em SNAKE_CASE. Propriedades calculadas marcadas com `Ignore`.

**Repositório** recebe o DbContext por injeção e expõe métodos assíncronos retornando
`Task<IEnumerable<T>>` para coleções.

## Testes

xUnit no padrão AAA, com as três seções visualmente separadas.

Não use Moq nem qualquer biblioteca de mock. Dublês são classes stub declaradas
`private sealed` dentro do próprio arquivo de teste.

Testes de repositório usam DbContext InMemory até a F8. A partir da F9, passam a rodar
contra PostgreSQL real com Testcontainers.

Nomeie os testes descrevendo o comportamento esperado, não o método chamado.
Cada regra de negócio da especificação precisa de teste, incluindo os casos de
fronteira — especialmente os limites das faixas da tabela regressiva de IR.

## Restrições

Não adicione pacotes NuGet além dos já presentes sem justificar a necessidade.

Não crie camadas, abstrações ou padrões que a especificação não pede. Interface com
uma única implementação só existe quando serve à inversão de dependência entre camadas.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- When graphify-out/graph.json exists, run `graphify query "<question>"` before your first grep, search or source-file read for a codebase question, and put this rule in every subagent prompt that explores code. Read raw files only for lines the graph or an earlier read already located. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
- A committed graphify-out/graph.json is rewritten whole on every build, so a large diff to it in a pull request is expected. Do not flag it as churn or suggest skipping the graph rebuild or its hooks.
