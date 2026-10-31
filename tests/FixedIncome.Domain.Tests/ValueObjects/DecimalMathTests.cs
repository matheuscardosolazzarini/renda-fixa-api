using System.Globalization;
using FixedIncome.Domain.Common;

namespace FixedIncome.Domain.Tests.ValueObjects;

public class DecimalMathTests
{
    // Tolerância de 1e-15: os valores de referência da spec vêm de Python com 50 dígitos e
    // aparecem com 16 casas.
    private const decimal Tolerance = 0.000_000_000_000_001m;

    // Tolerância de 1e-20 para os valores com 26 casas, calculados em ponto fixo de 60 dígitos
    // (BigInt; ln por série de atanh e exp por Taylor com redução de faixa), conferidos antes
    // contra os fatores de referência da spec.
    private const decimal FineTolerance = 0.000_000_000_000_000_000_01m;

    [Theory]
    [InlineData("0.9968", 9, 21, "0.99862731544494931771127679")]
    [InlineData("1.0016", 9, 21, "1.00068540107877381682226691")]
    [InlineData("1.15", 2500, 252, "4.00093148837348560458611338")]
    public void Potencia_fracionaria_bate_com_a_referencia_de_alta_precisao_em_1e_20(
        string baseValue, int numerator, int denominator, string expected)
    {
        // Arrange
        var x = decimal.Parse(baseValue, CultureInfo.InvariantCulture);
        var expectedValue = decimal.Parse(expected, CultureInfo.InvariantCulture);

        // Act
        var result = DecimalMath.Pow(x, (decimal)numerator / denominator);

        // Assert
        Assert.InRange(result, expectedValue - FineTolerance, expectedValue + FineTolerance);
    }

    [Theory]
    [InlineData("10", 100)]
    [InlineData("10", 30.5)]
    public void Potencia_acima_do_maximo_do_decimal_lanca_excecao_de_dominio(string baseValue, double exponent)
    {
        // Arrange
        var x = decimal.Parse(baseValue, CultureInfo.InvariantCulture);

        // Act
        Action act = () => DecimalMath.Pow(x, (decimal)exponent);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Theory]
    [InlineData("1.01", 250, 252, "1.0099202425607141")]
    [InlineData("1.10", 250, 252, "1.0991682416248615")]
    [InlineData("1.12", 1, 252, "1.0004498181430395")]
    [InlineData("1.12", 11, 252, "1.0049591431042557")]
    [InlineData("1.12", 21, 252, "1.0094887929345830")]
    [InlineData("1.12", 22, 252, "1.0099428793088400")]
    [InlineData("1.12", 23, 252, "1.0103971699393866")]
    public void Potencia_fracionaria_bate_com_os_fatores_de_referencia(
        string baseValue, int numerator, int denominator, string expected)
    {
        // Arrange
        var x = decimal.Parse(baseValue, CultureInfo.InvariantCulture);
        var expectedValue = decimal.Parse(expected, CultureInfo.InvariantCulture);

        // Act
        var result = DecimalMath.Pow(x, (decimal)numerator / denominator);

        // Assert
        Assert.InRange(result, expectedValue - Tolerance, expectedValue + Tolerance);
    }

    [Fact]
    public void Potencia_de_expoente_um_devolve_a_propria_base_sem_perda()
    {
        // Arrange
        var x = 1.10m;

        // Act
        var result = DecimalMath.Pow(x, 252m / 252);

        // Assert
        Assert.Equal(1.10m, result);
    }

    [Fact]
    public void Potencia_de_expoente_inteiro_e_exata()
    {
        // Arrange
        var x = 1.10m;

        // Act
        var result = DecimalMath.Pow(x, 2m);

        // Assert
        Assert.Equal(1.21m, result);
    }

    [Fact]
    public void Potencia_de_expoente_zero_e_um()
    {
        // Arrange
        var x = 1.0067m;

        // Act
        var result = DecimalMath.Pow(x, 0m);

        // Assert
        Assert.Equal(1m, result);
    }

    [Fact]
    public void Potencia_de_expoente_inteiro_negativo_e_o_inverso()
    {
        // Arrange
        var x = 2m;

        // Act
        var result = DecimalMath.Pow(x, -2m);

        // Assert
        Assert.Equal(0.25m, result);
    }

    [Fact]
    public void Potencia_de_base_fracionaria_com_expoente_meio_e_a_raiz_quadrada()
    {
        // Arrange: raiz quadrada de 2 = 1,4142135623730950488016887242
        var x = 2m;

        // Act
        var result = DecimalMath.Pow(x, 0.5m);

        // Assert
        Assert.InRange(result, 1.414213562373095048m, 1.414213562373095049m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Potencia_de_base_nao_positiva_e_rejeitada(int baseValue)
    {
        // Arrange
        var x = (decimal)baseValue;

        // Act
        Action act = () => DecimalMath.Pow(x, 0.5m);

        // Assert
        Assert.Throws<DomainException>(act);
    }
}
