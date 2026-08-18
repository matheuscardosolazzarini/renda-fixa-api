---
name: testes-xunit
description: Escreve ou revisa testes de unidade em xUnit neste projeto. Use quando a tarefa envolver criar arquivo de teste, adicionar caso de teste, cobrir uma regra de negócio com teste, ou revisar testes existentes. Cobre o padrão AAA, nomenclatura, dublês sem biblioteca de mock e comparação de valores decimais.
---

# Testes de unidade

## Padrão obrigatório

Estrutura AAA com as três seções visualmente separadas por linha em branco e
comentário identificando cada uma:

```csharp
[Fact]
public void Titulo_com_vencimento_anterior_a_emissao_e_rejeitado()
{
    // Arrange
    var issueDate = new DateOnly(2025, 1, 10);
    var maturityDate = new DateOnly(2025, 1, 5);

    // Act
    var act = () => new FixedIncomeAsset("CDB", "Banco", AssetType.Cdb,
        IndexType.PreFixed, 12m, issueDate, maturityDate);

    // Assert
    Assert.Throws<DomainException>(act);
}
```

## Nomenclatura

Nomeie descrevendo o comportamento esperado, não o método exercitado.

Bom: `Aporte_na_data_de_vencimento_e_aceito`
Ruim: `TestPositionConstructor`, `Project_ShouldWork`

Nome em português com identificadores separados por underline. É o único lugar do
projeto onde o identificador não é em inglês, porque aqui ele funciona como
documentação da regra.

## Dublês

Não use Moq nem qualquer biblioteca de mock.

Quando um dublê for necessário, declare uma classe `private sealed` no próprio arquivo
de teste, implementando a interface e devolvendo valores fixos. Nomeie com o sufixo
`Stub`.

Antes de criar um dublê, verifique se ele é mesmo necessário. Teste de domínio quase
nunca precisa: as entidades não têm dependência externa.

## Casos de fronteira

Toda regra com faixa ou limite exige teste nos dois lados da fronteira, não apenas no
meio do intervalo.

Para a tabela regressiva de IR, isso significa 180 e 181, 360 e 361, 720 e 721.
Testar apenas 100, 300 e 500 dias deixa passar erro de operador de comparação, que é
justamente o defeito mais provável.

Use `[Theory]` com `[InlineData]` para faixas. Use `[Fact]` para comportamento único.

## Valores decimais

Nunca compare `decimal` resultante de cálculo com `Assert.Equal` sem tolerância.
Cálculo que passa por exponenciação em `double` produz diferença na última casa.

```csharp
Assert.Equal(1_120.00m, projection.GrossAmount, precision: 2);
```

Escolha valores de entrada redondos para que o resultado esperado seja verificável à
mão. Um teste cujo valor esperado você não consegue conferir manualmente não prova
nada — ele apenas registra o que o código faz hoje.

## Cobertura

Cada regra de negócio numerada na especificação precisa de teste correspondente.

Para cada entidade: um teste de construção válida e um teste por invariante violada,
individualmente. Violar duas invariantes no mesmo teste esconde qual delas disparou.

## Antes de concluir

Rode `dotnet test` e confirme que todos passam. Reporte a contagem.

Se um teste falhar, corrija o código ou o teste conforme o caso — mas nunca ajuste o
valor esperado para casar com a saída atual sem antes verificar à mão qual dos dois
está errado.
