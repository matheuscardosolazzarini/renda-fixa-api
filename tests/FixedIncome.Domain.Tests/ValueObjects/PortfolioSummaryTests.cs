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
        Assert.Equal(0m, summary.TotalIofAmount);
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
        // Arrange: fixture artificial que fecha com a identidade bruto - IR - IOF = líquido
        // (1.000 - 300 - 700 = 0 e 500 - 200 - 300 = 0), como exige a RN-07 estendida.
        var positions = new[]
        {
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_000m, taxAmount: 300m, netAmount: 0m, iofAmount: 700m), AssetType.Cdb),
            (CriarProjecao(investedAmount: 500m, grossAmount: 500m, taxAmount: 200m, netAmount: 0m, iofAmount: 300m), AssetType.Lci)
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
        Assert.Equal(summary.TotalGrossAmount - summary.TotalTaxAmount - summary.TotalIofAmount, summary.TotalNetAmount);
        Assert.Equal(positions.Sum(p => p.Item1.NetAmount), summary.TotalNetAmount);
    }

    [Fact]
    public void RN07_total_liquido_fecha_com_bruto_menos_ir_menos_iof_no_caso_de_referencia_7_1()
    {
        // Arrange: casos 5.4 (CDB) e 5.5 (LCI), PreFixed 12%, R$ 1.000 cada, dia 15.
        // O LCI é isento de IR mas paga IOF.
        var positions = new[]
        {
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_004.96m, taxAmount: 0.56m, netAmount: 1_001.92m, iofAmount: 2.48m), AssetType.Cdb),
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_004.96m, taxAmount: 0m, netAmount: 1_002.48m, iofAmount: 2.48m), AssetType.Lci)
        };

        // Act
        var summary = PortfolioSummary.Create(positions);

        // Assert
        Assert.Equal(2_000.00m, summary.TotalInvestedAmount);
        Assert.Equal(2_009.92m, summary.TotalGrossAmount);
        Assert.Equal(9.92m, summary.TotalGrossYield);
        Assert.Equal(4.96m, summary.TotalIofAmount);
        Assert.Equal(0.56m, summary.TotalTaxAmount);
        Assert.Equal(2_004.40m, summary.TotalNetAmount);
        Assert.Equal(summary.TotalGrossAmount - summary.TotalTaxAmount - summary.TotalIofAmount, summary.TotalNetAmount);
    }

    [Fact]
    public void Participacao_percentual_do_caso_7_1_e_arredondada_a_duas_casas()
    {
        // Arrange: 1.001,92 / 2.004,40 = 49,9860% e 1.002,48 / 2.004,40 = 50,0140%.
        var positions = new[]
        {
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_004.96m, taxAmount: 0.56m, netAmount: 1_001.92m, iofAmount: 2.48m), AssetType.Cdb),
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_004.96m, taxAmount: 0m, netAmount: 1_002.48m, iofAmount: 2.48m), AssetType.Lci)
        };

        // Act
        var summary = PortfolioSummary.Create(positions);

        // Assert
        Assert.Equal(49.99m, summary.ByAssetType.Single(item => item.AssetType == AssetType.Cdb).Percentage);
        Assert.Equal(50.01m, summary.ByAssetType.Single(item => item.AssetType == AssetType.Lci).Percentage);
    }

    [Fact]
    public void RN07_regressao_B1_projecoes_do_caso_7_2_fecham_com_bruto_menos_ir_menos_iof()
    {
        // Arrange: casos 5.6 (CDB) e 5.7 (LCI), dia 29, com os líquidos da coluna C6b
        // (1.007,14 e 1.009,21). Na coluna C6a a soma dos líquidos dava 2.016,33 e a
        // identidade quebrava em R$ 0,02.
        var positions = new[]
        {
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_009.49m, taxAmount: 2.07m, netAmount: 1_007.14m, iofAmount: 0.28m), AssetType.Cdb),
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_009.49m, taxAmount: 0m, netAmount: 1_009.21m, iofAmount: 0.28m), AssetType.Lci)
        };

        // Act
        var summary = PortfolioSummary.Create(positions);

        // Assert
        Assert.Equal(2_018.98m, summary.TotalGrossAmount);
        Assert.Equal(0.56m, summary.TotalIofAmount);
        Assert.Equal(2.07m, summary.TotalTaxAmount);
        Assert.Equal(2_016.35m, summary.TotalNetAmount);
        Assert.Equal(summary.TotalGrossAmount - summary.TotalTaxAmount - summary.TotalIofAmount, summary.TotalNetAmount);
        Assert.NotEqual(2_016.33m, summary.TotalNetAmount);
    }

    private static PositionProjection CriarProjecao(
        decimal investedAmount, decimal grossAmount, decimal taxAmount, decimal netAmount, decimal iofAmount = 0m)
    {
        return new PositionProjection(
            InvestedAmount: investedAmount,
            GrossAmount: grossAmount,
            GrossYield: grossAmount - investedAmount,
            TaxRate: 0m,
            TaxAmount: taxAmount,
            NetAmount: netAmount,
            ElapsedDays: 365,
            BusinessDays: 252,
            IofRate: 0m,
            IofAmount: iofAmount,
            IsEstimated: false);
    }
}
