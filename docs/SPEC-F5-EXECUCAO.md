# Spec de execução — F5: Consolidação da carteira

Documento de execução para agente. Objetivo: agregar as projeções de todas as posições em
um resumo da carteira, exposto por um endpoint.

## Contexto

A especificação funcional está em `docs/ESPECIFICACAO.md`. As convenções de código estão
em `CLAUDE.md`. Ambas prevalecem sobre qualquer suposição.

Esta fase toca:

- `src/FixedIncome.Domain` — objeto de agregação e sua lógica
- `src/FixedIncome.Application` — DTO, caso de uso e registro em DI
- `src/FixedIncome.Api` — controller
- Os projetos de teste correspondentes

## Fora de escopo

Não altere entidades, regras ou cálculos existentes. Não altere repositórios nem
configurações de EF Core. Não adicione pacotes NuGet.

## Regra de negócio nova

**RN-07 — Coerência do consolidado.** Os totais do resumo são obtidos somando os valores
já arredondados de cada projeção individual, não arredondando a soma dos valores brutos.

A diferença é de centavos, mas o consolidado precisa bater exatamente com a lista de
posições que o usuário vê. Um total que não fecha com as parcelas exibidas é lido como
erro do sistema, ainda que a soma sem arredondamento intermediário seja matematicamente
mais precisa.

Consequência verificável: `TotalNetAmount` é sempre igual a `TotalGrossAmount` menos
`TotalTaxAmount`, e igual à soma dos `NetAmount` individuais.

Acrescente esta regra à seção "Regras de negócio" de `docs/ESPECIFICACAO.md`, seguindo a
numeração e o formato das existentes. É a única alteração autorizada naquele documento.

## Implementação

### P1 — Objeto de agregação no domínio

`PortfolioSummary` em `FixedIncome.Domain/ValueObjects`, com um método estático de fábrica
recebendo as projeções já calculadas e os tipos de título correspondentes.

A assinatura precisa permitir agrupar por tipo sem que o domínio consulte nada. Receba uma
coleção de pares projeção e `AssetType` — um record simples serve, ou uma tupla nomeada.

Campos do resumo:

| Campo | Descrição |
|---|---|
| `TotalInvestedAmount` | soma dos valores aportados |
| `TotalGrossAmount` | soma dos valores brutos projetados |
| `TotalGrossYield` | soma dos rendimentos brutos |
| `TotalTaxAmount` | soma dos impostos devidos |
| `TotalNetAmount` | soma dos valores líquidos |
| `PositionCount` | quantidade de posições consideradas |
| `ByAssetType` | quebra por tipo de título |

Cada item de `ByAssetType` traz: `AssetType`, `PositionCount`, `TotalInvestedAmount`,
`TotalNetAmount` e a participação percentual sobre o `TotalNetAmount` geral, com duas casas
decimais.

Comportamento em casos limite:

- Coleção vazia produz um resumo com todos os totais em zero, `PositionCount` zero e
  `ByAssetType` vazio. Não lance exceção: carteira sem posições é estado válido, não erro.
- Se `TotalNetAmount` for zero, a participação percentual de cada tipo é zero, sem divisão
  por zero.
- Tipos de título sem nenhuma posição não aparecem em `ByAssetType`.

A ordenação de `ByAssetType` é por `TotalNetAmount` decrescente.

Registre em comentário a decisão da RN-07: os totais somam valores já arredondados para
que o consolidado feche com as parcelas exibidas.

### P2 — DTO

`PortfolioSummaryResponse` em `FixedIncome.Application/DTOs`, como `record`, espelhando os
campos do resumo, com um record aninhado para os itens de `ByAssetType`.

Mapeamento manual em método estático junto ao DTO, como nos demais.

### P3 — Caso de uso

`GetPortfolioSummary`, com interface, retornando `ApiResponse<PortfolioSummaryResponse>`.

Recebe uma data de referência opcional. Ausente, usa a data atual.

Fluxo:

1. Carrega todas as posições
2. Carrega os títulos necessários
3. Calcula a projeção de cada posição na data de referência
4. Monta o resumo pelo objeto de domínio
5. Retorna 200

Carteira vazia retorna 200 com o resumo zerado, não 404.

Se a projeção de alguma posição lançar `DomainException`, capture e retorne `BadRequest`
com a mensagem, como nos demais casos de uso.

Evite consultar o repositório de títulos uma vez por posição. Carregue os títulos uma vez
e resolva em memória — várias posições costumam apontar para o mesmo título.

Registre o caso de uso em `AddApplication`.

### P4 — Endpoint

`GET /api/portfolio/summary` em um `PortfolioController`, seguindo o padrão dos demais:
herda `ControllerBase`, sem regra de negócio, retornando
`StatusCode(response.StatusCode, response)`.

`referenceDate` por query string, opcional. Documente com `[ProducesResponseType]`.

Acrescente as requisições correspondentes ao `FixedIncome.Api.http`.

### P5 — Testes

**Domínio** — resumo de coleção vazia; resumo de uma posição; resumo de várias posições do
mesmo tipo; resumo de tipos mistos com a quebra correta; participação percentual somando
100 quando há mais de um tipo; participação zero quando o total líquido é zero; ordenação
de `ByAssetType` por valor líquido decrescente; verificação explícita da RN-07, com valores
onde somar arredondados difere de arredondar a soma.

**Aplicação** — caminho de sucesso com posições de tipos diferentes; carteira vazia
retornando 200 zerado; data de referência ausente usando a data atual; dublê de repositório
confirmando que os títulos são carregados uma vez, e não uma vez por posição.

**Integração** — criar dois títulos de tipos diferentes, um aporte em cada, e obter o
resumo, conferindo que os totais fecham com as projeções individuais.

Siga a skill `testes-xunit`.

## Verificação

```powershell
dotnet build
dotnet test
```

Com a API em execução, obtenha o resumo e confira manualmente que `TotalNetAmount` bate com
a soma dos `NetAmount` das projeções individuais das mesmas posições.

## Restrições

- Não altere entidades, regras ou cálculos existentes
- Não altere `CLAUDE.md` nem specs de fases anteriores
- A única alteração permitida em `docs/ESPECIFICACAO.md` é acrescentar a RN-07
- Não altere o README
- Não adicione pacotes NuGet
- Não execute `git add` nem `git commit`

## Critérios de aceite

1. `dotnet build` sem erro nem warning
2. Todos os testes passam, incluindo os 109 existentes
3. `FixedIncome.Domain` continua sem referência de projeto e sem pacote
4. Carteira vazia retorna 200 com resumo zerado
5. `TotalNetAmount` é igual à soma dos `NetAmount` individuais, verificado por teste
6. Nenhuma consulta ao repositório de títulos dentro de laço sobre posições
7. A RN-07 está na especificação funcional e coberta por teste
8. Nenhum commit foi criado

## Relatório final

Reporte:

- Resultado de `dotnet build` e `dotnet test`, com a contagem de testes
- Arquivos criados
- O método de fábrica de `PortfolioSummary` na íntegra
- O corpo da resposta do endpoint com uma carteira de tipos mistos
- Decisões tomadas que não estavam especificadas aqui
- Qualquer ponto que se mostrou ambíguo durante a implementação
