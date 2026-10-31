using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;

namespace FixedIncome.Domain.Tests.Entities;

public class HolidayTests
{
    [Fact]
    public void Feriado_valido_e_criado_com_data_e_nome()
    {
        // Arrange
        var date = new DateOnly(2026, 9, 7);

        // Act
        var holiday = new Holiday(date, "Independência do Brasil");

        // Assert
        Assert.Equal(date, holiday.Date);
        Assert.Equal("Independência do Brasil", holiday.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Nome_vazio_ou_em_branco_e_rejeitado(string name)
    {
        // Arrange
        var date = new DateOnly(2026, 9, 7);

        // Act
        Action act = () => new Holiday(date, name);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Nome_com_120_caracteres_e_aceito()
    {
        // Arrange
        var name = new string('a', 120);

        // Act
        var holiday = new Holiday(new DateOnly(2026, 9, 7), name);

        // Assert
        Assert.Equal(name, holiday.Name);
    }

    [Fact]
    public void Nome_com_121_caracteres_e_rejeitado()
    {
        // Arrange
        var name = new string('a', 121);

        // Act
        Action act = () => new Holiday(new DateOnly(2026, 9, 7), name);

        // Assert
        Assert.Throws<DomainException>(act);
    }
}
