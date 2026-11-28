using System.Globalization;
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

    [Fact]
    public void RateFor_com_prazo_negativo_informa_que_o_prazo_nao_pode_ser_negativo()
    {
        // Arrange
        var assetType = AssetType.Cdb;
        var elapsedDays = -1;

        // Act
        Action act = () => IncomeTaxTable.RateFor(assetType, elapsedDays);

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Contains("prazo decorrido não pode ser negativo", exception.Message);
    }

    [Theory]
    [InlineData(0, 181, 20.0)]
    [InlineData(180, 181, 20.0)]
    [InlineData(181, 361, 17.5)]
    [InlineData(360, 361, 17.5)]
    [InlineData(361, 721, 15.0)]
    [InlineData(720, 721, 15.0)]
    public void NextBracketFor_devolve_o_primeiro_dia_e_a_aliquota_da_faixa_seguinte_nas_fronteiras(
        int elapsedDays, int expectedFromDay, decimal expectedRate)
    {
        // Arrange
        var assetType = AssetType.Cdb;

        // Act
        var bracket = IncomeTaxTable.NextBracketFor(assetType, elapsedDays);

        // Assert
        Assert.NotNull(bracket);
        Assert.Equal(expectedFromDay, bracket.Value.FromDay);
        Assert.Equal(expectedRate, bracket.Value.Rate);
    }

    [Theory]
    [InlineData(721)]
    [InlineData(1000)]
    public void NextBracketFor_na_ultima_faixa_nao_devolve_proxima(int elapsedDays)
    {
        // Arrange
        var assetType = AssetType.TreasuryBond;

        // Act
        var bracket = IncomeTaxTable.NextBracketFor(assetType, elapsedDays);

        // Assert
        Assert.Null(bracket);
    }

    [Theory]
    [InlineData(AssetType.Lci, 0)]
    [InlineData(AssetType.Lci, 181)]
    [InlineData(AssetType.Lci, 721)]
    [InlineData(AssetType.Lca, 0)]
    [InlineData(AssetType.Lca, 181)]
    [InlineData(AssetType.Lca, 721)]
    public void NextBracketFor_de_lci_e_lca_nao_devolve_proxima_faixa(AssetType assetType, int elapsedDays)
    {
        // Arrange: RN-04, isenção independente do prazo.

        // Act
        var bracket = IncomeTaxTable.NextBracketFor(assetType, elapsedDays);

        // Assert
        Assert.Null(bracket);
    }

    [Fact]
    public void NextBracketFor_devolve_a_aliquota_com_a_mesma_escala_da_tabela()
    {
        // Arrange: a escala vem do literal da tabela e sai no JSON como 20.0, 17.5 e 15.0.
        var assetType = AssetType.Cdb;

        // Act
        var first = IncomeTaxTable.NextBracketFor(assetType, 0);
        var second = IncomeTaxTable.NextBracketFor(assetType, 181);
        var third = IncomeTaxTable.NextBracketFor(assetType, 361);

        // Assert
        Assert.Equal("20.0", first!.Value.Rate.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("17.5", second!.Value.Rate.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("15.0", third!.Value.Rate.ToString(CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(AssetType.Cdb)]
    [InlineData(AssetType.Lci)]
    [InlineData(AssetType.Lca)]
    [InlineData(AssetType.TreasuryBond)]
    public void NextBracketFor_com_prazo_negativo_lanca_a_mesma_excecao_de_dominio_do_rate_for(AssetType assetType)
    {
        // Arrange
        var elapsedDays = -1;

        // Act
        Action rateFor = () => IncomeTaxTable.RateFor(assetType, elapsedDays);
        Action nextBracketFor = () => IncomeTaxTable.NextBracketFor(assetType, elapsedDays);

        // Assert
        var expected = Assert.Throws<DomainException>(rateFor);
        var actual = Assert.Throws<DomainException>(nextBracketFor);
        Assert.Equal(expected.Message, actual.Message);
    }
}
