using FixedIncome.Domain.Common;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Domain.Tests.ValueObjects;

public class IofTableTests
{
    // Valores da RN-11 (anexo do Decreto 6.306/2007), transcritos da especificação.
    [Theory]
    [InlineData(1, 96)]
    [InlineData(2, 93)]
    [InlineData(3, 90)]
    [InlineData(4, 86)]
    [InlineData(5, 83)]
    [InlineData(6, 80)]
    [InlineData(7, 76)]
    [InlineData(8, 73)]
    [InlineData(9, 70)]
    [InlineData(10, 66)]
    [InlineData(11, 63)]
    [InlineData(12, 60)]
    [InlineData(13, 56)]
    [InlineData(14, 53)]
    [InlineData(15, 50)]
    [InlineData(16, 46)]
    [InlineData(17, 43)]
    [InlineData(18, 40)]
    [InlineData(19, 36)]
    [InlineData(20, 33)]
    [InlineData(21, 30)]
    [InlineData(22, 26)]
    [InlineData(23, 23)]
    [InlineData(24, 20)]
    [InlineData(25, 16)]
    [InlineData(26, 13)]
    [InlineData(27, 10)]
    [InlineData(28, 6)]
    [InlineData(29, 3)]
    [InlineData(30, 0)]
    public void RateFor_segue_a_tabela_do_decreto_para_os_dias_1_a_30(int elapsedDays, decimal expectedRate)
    {
        // Arrange

        // Act
        var rate = IofTable.RateFor(elapsedDays);

        // Assert
        Assert.Equal(expectedRate, rate);
    }

    [Fact]
    public void RateFor_no_dia_zero_retorna_aliquota_zero()
    {
        // Arrange
        var elapsedDays = 0;

        // Act
        var rate = IofTable.RateFor(elapsedDays);

        // Assert
        Assert.Equal(0m, rate);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(180)]
    [InlineData(1000)]
    public void RateFor_a_partir_do_dia_31_retorna_aliquota_zero(int elapsedDays)
    {
        // Arrange

        // Act
        var rate = IofTable.RateFor(elapsedDays);

        // Assert
        Assert.Equal(0m, rate);
    }

    [Fact]
    public void RateFor_com_prazo_negativo_lanca_excecao_de_dominio()
    {
        // Arrange
        var elapsedDays = -1;

        // Act
        Action act = () => IofTable.RateFor(elapsedDays);

        // Assert
        Assert.Throws<DomainException>(act);
    }
}
