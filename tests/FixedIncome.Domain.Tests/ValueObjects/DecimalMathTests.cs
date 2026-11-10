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

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void Potencia_de_base_nao_positiva_informa_que_a_base_deve_ser_positiva(string baseValue)
    {
        // Arrange
        var x = decimal.Parse(baseValue, CultureInfo.InvariantCulture);

        // Act
        Action act = () => DecimalMath.Pow(x, 0.5m);

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Contains("base da potência deve ser positiva", exception.Message);
    }

    [Fact]
    public void Potencia_acima_do_maximo_do_decimal_informa_que_o_fator_esta_fora_do_intervalo()
    {
        // Arrange: 10^100 não cabe em decimal (máximo de cerca de 7,9 x 10^28).
        var x = 10m;

        // Act
        Action act = () => DecimalMath.Pow(x, 100m);

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Contains("fora do intervalo representável", exception.Message);
    }

    [Theory]
    [InlineData("4", "0.5", "2")] // raiz quadrada de 4: base acima de 2 entra na redução de intervalo
    [InlineData("8", "0.3333333333333333333333333333", "2")] // raiz cúbica de 8
    [InlineData("0.25", "0.5", "0.5")] // raiz quadrada de 1/4: base abaixo de 0,5 entra na redução
    [InlineData("0.125", "0.3333333333333333333333333333", "0.5")] // raiz cúbica de 1/8: duas reduções
    public void Potencia_com_base_fora_do_intervalo_de_reducao_devolve_a_raiz_exata(
        string baseValue, string exponent, string expected)
    {
        // Arrange: os resultados são identidades (4^(1/2) = 2, 8^(1/3) = 2, (1/4)^(1/2) = 1/2,
        // (1/8)^(1/3) = 1/2), com a tolerância de 1e-20 dos demais testes de precisão.
        var x = decimal.Parse(baseValue, CultureInfo.InvariantCulture);
        var y = decimal.Parse(exponent, CultureInfo.InvariantCulture);
        var expectedValue = decimal.Parse(expected, CultureInfo.InvariantCulture);

        // Act
        var result = DecimalMath.Pow(x, y);

        // Assert
        Assert.InRange(result, expectedValue - FineTolerance, expectedValue + FineTolerance);
    }

    [Fact]
    public void Potencia_inteira_nao_estoura_ao_elevar_ao_quadrado_alem_do_necessario()
    {
        // Arrange: 10^8 elevado a 3 é 10^24, que cabe em decimal; o quadrado seguinte da base
        // (10^32) não caberia, e a potência por quadrados sucessivos não deve calculá-lo.
        var x = 100_000_000m;

        // Act
        var result = DecimalMath.Pow(x, 3m);

        // Assert
        Assert.Equal(1e24m, result);
    }

    [Fact]
    public void Potencia_com_expoente_inteiro_no_limite_do_int_usa_a_multiplicacao_exata()
    {
        // Arrange: (1 + a)^n = 1 + n x a + termos de ordem a^2, com a = 1e-28 e n = int.MaxValue
        // = 2147483647. O termo de segunda ordem é cerca de 2e-38, abaixo da 28ª casa, então o
        // resultado é 1 + 2147483647e-28 = 1,0000000000000000002147483647. Pela série de
        // exp/ln a base cairia em ln = 0 e o resultado seria 1.
        var x = 1.0000000000000000000000000001m;

        // Act
        var result = DecimalMath.Pow(x, int.MaxValue);

        // Assert
        Assert.InRange(
            result, 1.0000000000000000002147483647m - 1e-25m, 1.0000000000000000002147483647m + 1e-25m);
    }

    [Fact]
    public void Raiz_quadrada_de_2_bate_com_a_constante_dentro_da_precisao_do_calculo()
    {
        // Arrange: raiz de 2 = 1,41421356237309504880168872420969807... Este teste fixa a precisão
        // do cálculo (tolerância de 1e-20, a mesma dos demais testes de precisão; o cabeçalho de
        // DecimalMath declara cerca de 26 dígitos corretos, então não se exige igualdade na 28ª casa).
        // Por isso o mutante x > 2m para x >= 2m, que só difere no ruído da 28ª casa, sobrevive e é
        // aceito sem desabilitar, como x < 0.5m para x <= 0.5m na redução do logaritmo: não é equivalência estrita.
        var x = 2m;
        var expectedValue = 1.4142135623730950488016887242m;

        // Act
        var result = DecimalMath.Pow(x, 0.5m);

        // Assert
        Assert.InRange(result, expectedValue - FineTolerance, expectedValue + FineTolerance);
    }
}
