using FixedIncome.Domain.Common;
using FixedIncome.Domain.Enums;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Domain.Tests.ValueObjects;

public class IncomeTaxTableTests
{
    [Theory]
    [InlineData(180, 22.5)]
    [InlineData(181, 20.0)]
    [InlineData(360, 20.0)]
    [InlineData(361, 17.5)]
    [InlineData(720, 17.5)]
    [InlineData(721, 15.0)]
    public void RateFor_segue_a_tabela_regressiva_nas_fronteiras_exatas(int elapsedDays, decimal expectedRate)
    {
        // Arrange
        var assetType = AssetType.Cdb;

        // Act
        var rate = IncomeTaxTable.RateFor(assetType, elapsedDays);

        // Assert
        Assert.Equal(expectedRate, rate);
    }

    [Theory]
    [InlineData(180, 22.5)]
    [InlineData(181, 20.0)]
    [InlineData(360, 20.0)]
    [InlineData(361, 17.5)]
    [InlineData(720, 17.5)]
    [InlineData(721, 15.0)]
    public void RateFor_de_treasury_bond_segue_a_tabela_regressiva_nas_fronteiras_exatas(int elapsedDays, decimal expectedRate)
    {
        // Arrange
        var assetType = AssetType.TreasuryBond;

        // Act
        var rate = IncomeTaxTable.RateFor(assetType, elapsedDays);

        // Assert
        Assert.Equal(expectedRate, rate);
    }

    [Fact]
    public void RateFor_de_lci_em_prazo_curto_retorna_aliquota_zero()
    {
        // Arrange
        var assetType = AssetType.Lci;
        var elapsedDays = 30;

        // Act
        var rate = IncomeTaxTable.RateFor(assetType, elapsedDays);

        // Assert
        Assert.Equal(0m, rate);
    }

    [Fact]
    public void RateFor_de_lci_em_prazo_longo_retorna_aliquota_zero()
    {
        // Arrange
        var assetType = AssetType.Lci;
        var elapsedDays = 1000;

        // Act
        var rate = IncomeTaxTable.RateFor(assetType, elapsedDays);

        // Assert
        Assert.Equal(0m, rate);
    }

    [Fact]
    public void RateFor_de_lca_em_prazo_curto_retorna_aliquota_zero()
    {
        // Arrange
        var assetType = AssetType.Lca;
        var elapsedDays = 30;

        // Act
        var rate = IncomeTaxTable.RateFor(assetType, elapsedDays);

        // Assert
        Assert.Equal(0m, rate);
    }

    [Fact]
    public void RateFor_de_lca_em_prazo_longo_retorna_aliquota_zero()
    {
        // Arrange
        var assetType = AssetType.Lca;
        var elapsedDays = 1000;

        // Act
        var rate = IncomeTaxTable.RateFor(assetType, elapsedDays);

        // Assert
        Assert.Equal(0m, rate);
    }

    [Fact]
    public void RateFor_com_prazo_negativo_lanca_excecao_de_dominio()
    {
        // Arrange
        var assetType = AssetType.Cdb;
        var elapsedDays = -1;

        // Act
        Action act = () => IncomeTaxTable.RateFor(assetType, elapsedDays);

        // Assert
        Assert.Throws<DomainException>(act);
    }
}
