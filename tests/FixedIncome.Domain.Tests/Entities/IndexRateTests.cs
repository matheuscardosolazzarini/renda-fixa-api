using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Domain.Tests.Entities;

public class IndexRateTests
{
    [Fact]
    public void Cdi_valido_e_criado_com_tipo_data_e_valor()
    {
        // Arrange
        var date = new DateOnly(2026, 9, 14);

        // Act
        var rate = new IndexRate(IndexType.Cdi, date, 0.055131m);

        // Assert
        Assert.Equal(IndexType.Cdi, rate.IndexType);
        Assert.Equal(date, rate.ReferenceDate);
        Assert.Equal(0.055131m, rate.Value);
    }

    [Fact]
    public void Indexador_prefixado_e_rejeitado()
    {
        // Arrange
        var date = new DateOnly(2026, 9, 14);

        // Act
        Action act = () => new IndexRate(IndexType.PreFixed, date, 1m);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Cdi_negativo_e_rejeitado()
    {
        // Arrange
        var date = new DateOnly(2026, 9, 14);

        // Act
        Action act = () => new IndexRate(IndexType.Cdi, date, -0.000001m);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Cdi_zero_e_aceito()
    {
        // Arrange
        var date = new DateOnly(2026, 9, 14);

        // Act
        var rate = new IndexRate(IndexType.Cdi, date, 0m);

        // Assert
        Assert.Equal(0m, rate.Value);
    }

    [Fact]
    public void Ipca_negativo_no_primeiro_dia_do_mes_e_aceito()
    {
        // Arrange
        var month = new DateOnly(2026, 8, 1);

        // Act
        var rate = new IndexRate(IndexType.Ipca, month, -0.20m);

        // Assert
        Assert.Equal(-0.20m, rate.Value);
        Assert.Equal(month, rate.ReferenceDate);
    }

    [Fact]
    public void Ipca_positivo_no_primeiro_dia_do_mes_e_aceito()
    {
        // Arrange
        var month = new DateOnly(2026, 7, 1);

        // Act
        var rate = new IndexRate(IndexType.Ipca, month, 0.26m);

        // Assert
        Assert.Equal(IndexType.Ipca, rate.IndexType);
        Assert.Equal(0.26m, rate.Value);
    }

    [Theory]
    [InlineData("2026-07-02")]
    [InlineData("2026-07-31")]
    [InlineData("2026-02-28")]
    public void Ipca_fora_do_primeiro_dia_do_mes_e_rejeitado(string date)
    {
        // Arrange
        var referenceDate = DateOnly.Parse(date);

        // Act
        Action act = () => new IndexRate(IndexType.Ipca, referenceDate, 0.26m);

        // Assert
        Assert.Throws<DomainException>(act);
    }
}
