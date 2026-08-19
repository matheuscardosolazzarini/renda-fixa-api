using FixedIncome.Domain.Enums;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Domain.Tests.ValueObjects;

public class PortfolioSummaryTests
{
    [Fact]
    public void Colecao_vazia_produz_resumo_zerado_sem_lancar_excecao()
    {
        // Arrange
        var positions = Array.Empty<(PositionProjection Projection, AssetType AssetType)>();

        // Act
        var summary = PortfolioSummary.Create(positions);

        // Assert
        Assert.Equal(0m, summary.TotalInvestedAmount);
        Assert.Equal(0m, summary.TotalGrossAmount);
        Assert.Equal(0m, summary.TotalGrossYield);
        Assert.Equal(0m, summary.TotalTaxAmount);
        Assert.Equal(0m, summary.TotalNetAmount);
        Assert.Equal(0, summary.PositionCount);
        Assert.Empty(summary.ByAssetType);
    }

    [Fact]
    public void Resumo_de_uma_unica_posicao_reflete_seus_proprios_valores()
    {
        // Arrange
        var projection = CriarProjecao(investedAmount: 1_000m, grossAmount: 1_100m, taxAmount: 17.50m, netAmount: 1_082.50m);
        var positions = new[] { (projection, AssetType.Cdb) };

        // Act
        var summary = PortfolioSummary.Create(positions);

        // Assert
        Assert.Equal(1, summary.PositionCount);
        Assert.Equal(1_082.50m, summary.TotalNetAmount);
        var item = Assert.Single(summary.ByAssetType);
        Assert.Equal(AssetType.Cdb, item.AssetType);
        Assert.Equal(1, item.PositionCount);
        Assert.Equal(100.00m, item.Percentage);
    }

    [Fact]
    public void Varias_posicoes_do_mesmo_tipo_sao_agrupadas_em_um_unico_item()
    {
        // Arrange
        var positions = new[]
        {
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_100m, taxAmount: 17.50m, netAmount: 1_082.50m), AssetType.Cdb),
            (CriarProjecao(investedAmount: 2_000m, grossAmount: 2_200m, taxAmount: 35.00m, netAmount: 2_165.00m), AssetType.Cdb)
        };

        // Act
        var summary = PortfolioSummary.Create(positions);

        // Assert
        Assert.Equal(2, summary.PositionCount);
        var item = Assert.Single(summary.ByAssetType);
        Assert.Equal(2, item.PositionCount);
        Assert.Equal(3_000m, item.TotalInvestedAmount);
        Assert.Equal(3_247.50m, item.TotalNetAmount);
    }

    [Fact]
    public void Tipos_mistos_produzem_uma_quebra_por_tipo_com_os_totais_corretos()
    {
        // Arrange
        var positions = new[]
        {
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_100m, taxAmount: 17.50m, netAmount: 1_082.50m), AssetType.Cdb),
            (CriarProjecao(investedAmount: 500m, grossAmount: 550m, taxAmount: 0m, netAmount: 550m), AssetType.Lci)
        };

        // Act
        var summary = PortfolioSummary.Create(positions);

        // Assert
        Assert.Equal(2, summary.ByAssetType.Count);
        var cdb = summary.ByAssetType.Single(item => item.AssetType == AssetType.Cdb);
        var lci = summary.ByAssetType.Single(item => item.AssetType == AssetType.Lci);
        Assert.Equal(1_082.50m, cdb.TotalNetAmount);
        Assert.Equal(550m, lci.TotalNetAmount);
    }

    [Fact]
    public void Participacao_percentual_soma_100_quando_ha_mais_de_um_tipo()
    {
        // Arrange: divisão exata para que a soma das participações feche em 100,00 sem
        // sobra de arredondamento entre os dois tipos.
        var positions = new[]
        {
            (CriarProjecao(investedAmount: 500m, grossAmount: 500m, taxAmount: 0m, netAmount: 500m), AssetType.Cdb),
            (CriarProjecao(investedAmount: 500m, grossAmount: 500m, taxAmount: 0m, netAmount: 500m), AssetType.Lci)
        };

        // Act
        var summary = PortfolioSummary.Create(positions);

        // Assert
        Assert.Equal(100.00m, summary.ByAssetType.Sum(item => item.Percentage));
    }

    [Fact]
    public void Participacao_percentual_e_zero_para_todos_quando_total_liquido_e_zero()
    {
        // Arrange
        var positions = new[]
        {
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_000m, taxAmount: 0m, netAmount: 0m), AssetType.Cdb),
            (CriarProjecao(investedAmount: 500m, grossAmount: 500m, taxAmount: 0m, netAmount: 0m), AssetType.Lci)
        };

        // Act
        var summary = PortfolioSummary.Create(positions);

        // Assert
        Assert.Equal(0m, summary.TotalNetAmount);
        Assert.All(summary.ByAssetType, item => Assert.Equal(0m, item.Percentage));
    }

    [Fact]
    public void Tipo_sem_nenhuma_posicao_nao_aparece_na_quebra()
    {
        // Arrange
        var positions = new[] { (CriarProjecao(1_000m, 1_100m, 17.50m, 1_082.50m), AssetType.Cdb) };

        // Act
        var summary = PortfolioSummary.Create(positions);

        // Assert
        Assert.DoesNotContain(summary.ByAssetType, item => item.AssetType == AssetType.TreasuryBond);
        Assert.DoesNotContain(summary.ByAssetType, item => item.AssetType == AssetType.Lca);
    }

    [Fact]
    public void Quebra_por_tipo_e_ordenada_por_valor_liquido_decrescente()
    {
        // Arrange
        var positions = new[]
        {
            (CriarProjecao(investedAmount: 100m, grossAmount: 100m, taxAmount: 0m, netAmount: 100m), AssetType.Cdb),
            (CriarProjecao(investedAmount: 100m, grossAmount: 100m, taxAmount: 0m, netAmount: 300m), AssetType.Lci),
            (CriarProjecao(investedAmount: 100m, grossAmount: 100m, taxAmount: 0m, netAmount: 200m), AssetType.Lca)
        };

        // Act
        var summary = PortfolioSummary.Create(positions);

        // Assert
        Assert.Equal(
            new[] { AssetType.Lci, AssetType.Lca, AssetType.Cdb },
            summary.ByAssetType.Select(item => item.AssetType));
    }

    [Fact]
    public void RN07_totais_somam_valores_ja_arredondados_em_vez_de_arredondar_a_soma_bruta()
    {
        // Arrange: cada projeção representa um NetAmount bruto de 1000,004, que
        // Position.Project já arredonda para 1000,00 (RN-07 parte da premissa de que o
        // valor recebido aqui já vem arredondado). Somando as duas parcelas já
        // arredondadas o total é 2000,00. Se em vez disso a soma bruta (1000,004 +
        // 1000,004 = 2000,008) fosse arredondada só ao final, o total seria 2000,01 — um
        // centavo a mais do que a soma das parcelas exibidas ao usuário.
        var positions = new[]
        {
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_000m, taxAmount: 0m, netAmount: 1_000.00m), AssetType.Cdb),
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_000m, taxAmount: 0m, netAmount: 1_000.00m), AssetType.Cdb)
        };

        // Act
        var summary = PortfolioSummary.Create(positions);

        // Assert
        Assert.Equal(2_000.00m, summary.TotalNetAmount);
        Assert.NotEqual(2_000.01m, summary.TotalNetAmount);
        Assert.Equal(summary.TotalGrossAmount - summary.TotalTaxAmount, summary.TotalNetAmount);
        Assert.Equal(positions.Sum(p => p.Item1.NetAmount), summary.TotalNetAmount);
    }

    private static PositionProjection CriarProjecao(
        decimal investedAmount, decimal grossAmount, decimal taxAmount, decimal netAmount)
    {
        return new PositionProjection(
            InvestedAmount: investedAmount,
            GrossAmount: grossAmount,
            GrossYield: grossAmount - investedAmount,
            TaxRate: 0m,
            TaxAmount: taxAmount,
            NetAmount: netAmount,
            ElapsedDays: 365);
    }
}
