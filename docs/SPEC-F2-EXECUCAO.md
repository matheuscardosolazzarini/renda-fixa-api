# Spec de execução — F2: Domínio

Documento de execução para agente. Objetivo: implementar as entidades, invariantes e
regras de cálculo do domínio, com cobertura de testes.

## Contexto

A especificação funcional está em `docs/ESPECIFICACAO.md`. As convenções de código
estão em `CLAUDE.md`. Ambas prevalecem sobre qualquer suposição.

Esta fase toca apenas dois projetos: `src/FixedIncome.Domain` e
`tests/FixedIncome.Domain.Tests`. Nenhum outro projeto deve ser modificado.

## Fora de escopo

Não implemente nesta fase: DbContext, configurações de entidade, migrations,
repositórios, DTOs, casos de uso, controllers, injeção de dependência.

Não adicione pacotes NuGet a nenhum projeto.

## Estrutura alvo

```
src/FixedIncome.Domain/
├── Common/
│   ├── BaseEntity.cs
│   └── DomainException.cs
├── Enums/
│   ├── AssetType.cs
│   └── IndexType.cs
├── Entities/
│   ├── FixedIncomeAsset.cs
│   └── Position.cs
└── ValueObjects/
    ├── IndexRates.cs
    ├── IncomeTaxTable.cs
    └── PositionProjection.cs

tests/FixedIncome.Domain.Tests/
├── Entities/
│   ├── FixedIncomeAssetTests.cs
│   └── PositionTests.cs
└── ValueObjects/
    └── IncomeTaxTableTests.cs
```

## Implementação

### D1 — Common

`BaseEntity`: classe abstrata com `Id` do tipo `Guid`, atribuído na construção.

`DomainException`: herda `Exception`, com construtor recebendo mensagem. Usada para
sinalizar violação de invariante de domínio.

### D2 — Enums

`AssetType`: `Cdb`, `Lci`, `Lca`, `TreasuryBond`.

`IndexType`: `PreFixed`, `Cdi`, `Ipca`.

### D3 — FixedIncomeAsset

Herda `BaseEntity`. Propriedades com setter privado: `Name`, `Issuer`, `AssetType`,
`IndexType`, `Rate`, `IssueDate`, `MaturityDate`.

Construtor público recebendo todos os valores e validando, na ordem, lançando
`DomainException` com mensagem específica:

- `Name` não vazio e com até 120 caracteres
- `Issuer` não vazio e com até 120 caracteres
- `Rate` maior que zero
- `MaturityDate` posterior a `IssueDate`

Construtor privado sem parâmetros, para materialização futura pelo EF Core.
Marque-o com comentário indicando esse propósito.

Método `ResolveAnnualRate(IndexRates rates)` retornando `decimal`, com a taxa anual
efetiva conforme o indexador:

| IndexType | Cálculo |
|---|---|
| PreFixed | `Rate` |
| Cdi | `rates.Cdi * (Rate / 100)` |
| Ipca | `rates.Ipca + Rate` |

Método `IsTaxExempt()` retornando `true` para `Lci` e `Lca`.

### D4 — IndexRates

Record com `Cdi` e `Ipca`, ambos `decimal`, representando taxas anuais em pontos
percentuais. Sem comportamento além da construção.

O domínio não conhece a origem desses valores.

### D5 — IncomeTaxTable

Classe estática, sem estado e sem dependências.

Método `RateFor(AssetType assetType, int elapsedDays)` retornando `decimal` com a
alíquota em pontos percentuais:

- `Lci` e `Lca` retornam `0` antes de qualquer avaliação de prazo (RN-04)
- até 180 dias: `22.5`
- de 181 a 360 dias: `20.0`
- de 361 a 720 dias: `17.5`
- acima de 720 dias: `15.0`

`elapsedDays` negativo lança `DomainException`.

### D6 — Position

Herda `BaseEntity`. Propriedades com setter privado: `AssetId`, `InvestedAmount`,
`ApplicationDate`.

Construtor público recebendo `FixedIncomeAsset asset`, `decimal investedAmount` e
`DateOnly applicationDate`, validando:

- `asset` não nulo
- `investedAmount` maior que zero
- `applicationDate` entre `IssueDate` e `MaturityDate` do título, inclusive (RN-05)

Armazena `asset.Id` em `AssetId`. Não mantém referência ao objeto do título.

Construtor privado sem parâmetros, para o EF Core.

Método `Project(FixedIncomeAsset asset, DateOnly referenceDate, IndexRates rates)`
retornando `PositionProjection`:

1. Rejeita com `DomainException` se `asset.Id` diferir de `AssetId`
2. Rejeita com `DomainException` se `referenceDate` for anterior a `ApplicationDate`
3. Limita a data de cálculo ao vencimento do título (RN-02)
4. Calcula os dias corridos entre `ApplicationDate` e a data limitada
5. Obtém a taxa anual via `asset.ResolveAnnualRate(rates)`
6. Aplica capitalização composta com base 365 (RN-01)
7. Apura o rendimento, a alíquota via `IncomeTaxTable.RateFor` e o imposto
8. Devolve a projeção

### D7 — PositionProjection

Record com: `InvestedAmount`, `GrossAmount`, `GrossYield`, `TaxRate`, `TaxAmount`,
`NetAmount`, `ElapsedDays`.

## Regras de cálculo

**Capitalização (RN-01).** Montante bruto igual a valor investido multiplicado por
`(1 + taxaAnual/100)` elevado a `dias/365`.

**Exponenciação.** `Math.Pow` opera em `double`. Converta para `double` apenas na
exponenciação e retorne a `decimal` em seguida. Registre a decisão em comentário no
código: a perda de precisão é irrelevante na escala de valores tratada e implementar
potência em `decimal` acrescentaria complexidade sem ganho prático.

**Arredondamento.** Todo o cálculo intermediário permanece em `decimal` sem
arredondamento. Apenas os valores monetários do `PositionProjection` são arredondados
para duas casas, com `MidpointRounding.AwayFromZero`. `TaxRate` mantém uma casa
decimal.

**Imposto.** Incide exclusivamente sobre o rendimento, nunca sobre o principal (RN-03).

## Testes

Siga o padrão definido em `CLAUDE.md`: xUnit, AAA com seções separadas, sem Moq,
dublês como classes `private sealed` no próprio arquivo quando necessários.

Nomeie descrevendo o comportamento, não o método.

### Cobertura obrigatória

**IncomeTaxTableTests** — `[Theory]` cobrindo as fronteiras exatas de cada faixa:
180, 181, 360, 361, 720 e 721 dias. Casos separados para `Lci` e `Lca` retornando
zero em prazo curto e em prazo longo. Caso de `elapsedDays` negativo lançando
`DomainException`.

**FixedIncomeAssetTests** — construção válida; cada uma das quatro invariantes
violada individualmente; `ResolveAnnualRate` nos três indexadores; `IsTaxExempt` nos
quatro tipos de título.

**PositionTests** — construção válida; valor investido zero e negativo; aporte antes
da emissão e depois do vencimento; aporte exatamente na data de emissão e na de
vencimento aceito; projeção com data anterior ao aporte rejeitada; projeção limitada
ao vencimento quando a data de referência o ultrapassa; projeção de título isento
resultando em imposto zero; projeção com prazo em cada faixa de IR.

Nos testes de cálculo, use valores redondos e verifique o resultado com tolerância
explícita, não igualdade exata de `decimal`.

## Verificação

```bash
dotnet build
dotnet test
```

Build sem erro nem warning. Todos os testes passando.

## Restrições

- Não altere `CLAUDE.md`, `docs/ESPECIFICACAO.md` nem qualquer arquivo fora dos dois
  projetos desta fase
- Não execute `git add` nem `git commit`
- Não crie interface para nada nesta fase — não há inversão de dependência envolvida
- Não crie construtores, métodos ou propriedades além dos especificados

## Critérios de aceite

1. `dotnet build` conclui sem erro nem warning
2. Todos os testes passam
3. `FixedIncome.Domain.csproj` permanece sem `ProjectReference` e sem `PackageReference`
4. Nenhuma entidade pode ser construída em estado que viole suas invariantes
5. As seis fronteiras da tabela de IR têm teste dedicado
6. Nenhum commit foi criado

## Relatório final

Reporte:

- Resultado de `dotnet build` e `dotnet test`, com a contagem de testes
- Arquivos criados
- Decisões tomadas que não estavam especificadas aqui
- Qualquer ponto da especificação que se mostrou ambíguo durante a implementação
