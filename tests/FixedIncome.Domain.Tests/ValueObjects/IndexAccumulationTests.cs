using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Domain.Tests.ValueObjects;

// Produtório do CDI da RN-09 (D-7). Valores esperados dos casos 1, 1-bis e 2 de
// docs/SPEC-F12-EXECUCAO.md, calculados à mão com truncamento em 16 casas a cada dia.
public class IndexAccumulationTests
{
    [Fact]
    public void Sem_nenhuma_taxa_diaria_o_fator_e_um()
    {
        // Arrange
        var rates = Array.Empty<decimal>();

        // Act
        var factor = IndexAccumulation.CdiFactor(rates, 100m);

        // Assert
        Assert.Equal(1m, factor);
    }

    [Theory]
    [InlineData(4, 100, "1.00203307")] // (1 + a)^4 truncado = 1,0020330681766474
    [InlineData(5, 100, "1.00254198")] // 1,0025419807313130
    [InlineData(7, 100, "1.00356058")] // 1,0035605813714389
    [InlineData(7, 110, "1.00391724")] // (1 + b)^7 = 1,0039172364148334, com b = 0,000558668
    public void Fator_acumulado_trunca_em_16_casas_a_cada_dia_e_arredonda_em_8_no_final(
        int days, int percentOfCdi, string expected)
    {
        // Arrange: a = 0,050788% ao dia.
        var rates = Enumerable.Repeat(0.050788m, days).ToArray();

        // Act
        var factor = IndexAccumulation.CdiFactor(rates, percentOfCdi);

        // Assert
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), factor);
    }

    [Fact]
    public void Fator_acumulado_usa_a_taxa_de_cada_dia_na_ordem_recebida()
    {
        // Arrange: caso 1-bis. Três dias a 0,050788 e quatro a 0,051660; A3 = 1,0015244139572868 e
        // (1,0005166)^4 acumulado dá 1,00359557.
        var rates = new[] { 0.050788m, 0.050788m, 0.050788m, 0.051660m, 0.051660m, 0.051660m, 0.051660m };

        // Act
        var factor = IndexAccumulation.CdiFactor(rates, 100m);

        // Assert
        Assert.Equal(1.00359557m, factor);
    }
}
