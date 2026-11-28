using FixedIncome.Domain.Enums;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Domain.Tests.ValueObjects;

public class PortfolioSummaryTests
{
    [Fact]
    public void Colecao_vazia_produz_resumo_zerado_sem_lancar_excecao()
    {
        // Arrange
        var positions = Array.Empty<(PositionProjection Projection, Guid AssetId, AssetType AssetType)>();

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
        var positions = new[] { (projection, Guid.NewGuid(), AssetType.Cdb) };

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
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_100m, taxAmount: 17.50m, netAmount: 1_082.50m), Guid.NewGuid(), AssetType.Cdb),
            (CriarProjecao(investedAmount: 2_000m, grossAmount: 2_200m, taxAmount: 35.00m, netAmount: 2_165.00m), Guid.NewGuid(), AssetType.Cdb)
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
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_100m, taxAmount: 17.50m, netAmount: 1_082.50m), Guid.NewGuid(), AssetType.Cdb),
            (CriarProjecao(investedAmount: 500m, grossAmount: 550m, taxAmount: 0m, netAmount: 550m), Guid.NewGuid(), AssetType.Lci)
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
            (CriarProjecao(investedAmount: 500m, grossAmount: 500m, taxAmount: 0m, netAmount: 500m), Guid.NewGuid(), AssetType.Cdb),
            (CriarProjecao(investedAmount: 500m, grossAmount: 500m, taxAmount: 0m, netAmount: 500m), Guid.NewGuid(), AssetType.Lci)
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
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_000m, taxAmount: 300m, netAmount: 0m, iofAmount: 700m), Guid.NewGuid(), AssetType.Cdb),
            (CriarProjecao(investedAmount: 500m, grossAmount: 500m, taxAmount: 200m, netAmount: 0m, iofAmount: 300m), Guid.NewGuid(), AssetType.Lci)
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
        var positions = new[] { (CriarProjecao(1_000m, 1_100m, 17.50m, 1_082.50m), Guid.NewGuid(), AssetType.Cdb) };

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
            (CriarProjecao(investedAmount: 100m, grossAmount: 100m, taxAmount: 0m, netAmount: 100m), Guid.NewGuid(), AssetType.Cdb),
            (CriarProjecao(investedAmount: 100m, grossAmount: 100m, taxAmount: 0m, netAmount: 300m), Guid.NewGuid(), AssetType.Lci),
            (CriarProjecao(investedAmount: 100m, grossAmount: 100m, taxAmount: 0m, netAmount: 200m), Guid.NewGuid(), AssetType.Lca)
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
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_000m, taxAmount: 0m, netAmount: 1_000.00m), Guid.NewGuid(), AssetType.Cdb),
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_000m, taxAmount: 0m, netAmount: 1_000.00m), Guid.NewGuid(), AssetType.Cdb)
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
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_004.96m, taxAmount: 0.56m, netAmount: 1_001.92m, iofAmount: 2.48m), Guid.NewGuid(), AssetType.Cdb),
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_004.96m, taxAmount: 0m, netAmount: 1_002.48m, iofAmount: 2.48m), Guid.NewGuid(), AssetType.Lci)
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
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_004.96m, taxAmount: 0.56m, netAmount: 1_001.92m, iofAmount: 2.48m), Guid.NewGuid(), AssetType.Cdb),
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_004.96m, taxAmount: 0m, netAmount: 1_002.48m, iofAmount: 2.48m), Guid.NewGuid(), AssetType.Lci)
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
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_009.49m, taxAmount: 2.07m, netAmount: 1_007.14m, iofAmount: 0.28m), Guid.NewGuid(), AssetType.Cdb),
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_009.49m, taxAmount: 0m, netAmount: 1_009.21m, iofAmount: 0.28m), Guid.NewGuid(), AssetType.Lci)
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

    [Fact]
    public void Raiz_e_projetada_quando_um_item_entre_outros_e_projetado()
    {
        // Arrange
        var positions = new[]
        {
            (CriarProjecao(1_000m, 1_100m, 17.50m, 1_082.50m), Guid.NewGuid(), AssetType.Cdb),
            (CriarProjecao(1_000m, 1_050m, 11.25m, 1_038.75m, isProjected: true), Guid.NewGuid(), AssetType.Cdb),
            (CriarProjecao(500m, 550m, 0m, 550m), Guid.NewGuid(), AssetType.Lci)
        };

        // Act
        var summary = PortfolioSummary.Create(positions);

        // Assert
        Assert.True(summary.IsProjected);
    }

    [Fact]
    public void Raiz_nao_e_projetada_quando_nenhum_item_e_projetado()
    {
        // Arrange
        var positions = new[]
        {
            (CriarProjecao(1_000m, 1_100m, 17.50m, 1_082.50m), Guid.NewGuid(), AssetType.Cdb),
            (CriarProjecao(500m, 550m, 0m, 550m), Guid.NewGuid(), AssetType.Lci)
        };

        // Act
        var summary = PortfolioSummary.Create(positions);

        // Assert
        Assert.False(summary.IsProjected);
    }

    [Fact]
    public void Raiz_nao_e_projetada_com_a_lista_vazia()
    {
        // Arrange
        var positions = Array.Empty<(PositionProjection Projection, Guid AssetId, AssetType AssetType)>();

        // Act
        var summary = PortfolioSummary.Create(positions);

        // Assert
        Assert.False(summary.IsProjected);
    }

    [Fact]
    public void Total_net_yield_e_a_soma_dos_net_yield_dos_itens_e_o_liquido_menos_o_investido()
    {
        // Arrange: itens do caso 16 da seção "Casos de referência" de docs/SPEC-F13-EXECUCAO.md,
        // com IOF e IR. Net yield dos itens: 4,57, 2,67 e 2,67.
        var itemB = CriarProjecao(investedAmount: 1_500m, grossAmount: 1_506.82m, taxAmount: 0m, netAmount: 1_504.57m, iofAmount: 2.25m);
        var itemA = CriarProjecao(investedAmount: 1_000m, grossAmount: 1_004.93m, taxAmount: 0.78m, netAmount: 1_002.67m, iofAmount: 1.48m);
        var positions = new[] { (itemB, Guid.NewGuid(), AssetType.Lci), (itemA, Guid.NewGuid(), AssetType.Cdb), (itemA, Guid.NewGuid(), AssetType.Cdb) };

        // Act
        var summary = PortfolioSummary.Create(positions);

        // Assert
        Assert.Equal(9.91m, summary.TotalNetYield);
        Assert.Equal(positions.Sum(item => item.Item1.NetYield), summary.TotalNetYield);
        Assert.Equal(summary.TotalNetAmount - summary.TotalInvestedAmount, summary.TotalNetYield);
    }

    [Fact]
    public void Total_net_yield_e_zero_com_a_lista_vazia()
    {
        // Arrange
        var positions = Array.Empty<(PositionProjection Projection, Guid AssetId, AssetType AssetType)>();

        // Act
        var summary = PortfolioSummary.Create(positions);

        // Assert
        Assert.Equal(0m, summary.TotalNetYield);
    }

    [Fact]
    public void Por_titulo_com_aportes_intercalados_agrupa_na_ordem_da_primeira_aparicao_com_a_soma_de_cada_total()
    {
        // Arrange: títulos intercalados (P, Q, P). O título que aparece primeiro tem o menor valor e o
        // maior Guid, para que a ordem por valor líquido ou por id desfaça a ordem esperada. Cada
        // campo tem um valor distinto em cada item, para a soma de cada total ser conferida sozinha.
        var assetP = new Guid("00000000-0000-0000-0000-000000000002");
        var assetQ = new Guid("00000000-0000-0000-0000-000000000001");
        var positions = new[]
        {
            (CriarProjecao(investedAmount: 500m, grossAmount: 503.21m, taxAmount: 0.12m, netAmount: 502.43m, iofAmount: 0.66m), assetP, AssetType.Cdb),
            (CriarProjecao(investedAmount: 1_500m, grossAmount: 1_506.82m, taxAmount: 0m, netAmount: 1_504.57m, iofAmount: 2.25m), assetQ, AssetType.Lci),
            (CriarProjecao(investedAmount: 300m, grossAmount: 302.14m, taxAmount: 0.05m, netAmount: 301.78m, iofAmount: 0.31m), assetP, AssetType.Cdb)
        };

        // Act
        var summary = PortfolioSummary.Create(positions);

        // Assert
        Assert.Equal(2, summary.ByAsset.Count);

        var p = summary.ByAsset[0];
        Assert.Equal(assetP, p.AssetId);
        Assert.Equal(2, p.PositionCount);
        Assert.Equal(800m, p.TotalInvestedAmount);
        Assert.Equal(805.35m, p.TotalGrossAmount);
        Assert.Equal(5.35m, p.TotalGrossYield);
        Assert.Equal(0.17m, p.TotalTaxAmount);
        Assert.Equal(0.97m, p.TotalIofAmount);
        Assert.Equal(804.21m, p.TotalNetAmount);
        Assert.Equal(4.21m, p.TotalNetYield);

        var q = summary.ByAsset[1];
        Assert.Equal(assetQ, q.AssetId);
        Assert.Equal(1, q.PositionCount);
        Assert.Equal(1_500m, q.TotalInvestedAmount);
        Assert.Equal(1_506.82m, q.TotalGrossAmount);
        Assert.Equal(6.82m, q.TotalGrossYield);
        Assert.Equal(0m, q.TotalTaxAmount);
        Assert.Equal(2.25m, q.TotalIofAmount);
        Assert.Equal(1_504.57m, q.TotalNetAmount);
        Assert.Equal(4.57m, q.TotalNetYield);
    }

    [Fact]
    public void Por_titulo_cada_total_somado_entre_os_titulos_e_igual_ao_total_da_raiz()
    {
        // Arrange
        var assetP = Guid.NewGuid();
        var assetQ = Guid.NewGuid();
        var positions = new[]
        {
            (CriarProjecao(investedAmount: 500m, grossAmount: 503.21m, taxAmount: 0.12m, netAmount: 502.43m, iofAmount: 0.66m), assetP, AssetType.Cdb),
            (CriarProjecao(investedAmount: 1_500m, grossAmount: 1_506.82m, taxAmount: 0m, netAmount: 1_504.57m, iofAmount: 2.25m), assetQ, AssetType.Lci),
            (CriarProjecao(investedAmount: 300m, grossAmount: 302.14m, taxAmount: 0.05m, netAmount: 301.78m, iofAmount: 0.31m), assetP, AssetType.Cdb)
        };

        // Act
        var summary = PortfolioSummary.Create(positions);

        // Assert
        Assert.Equal(summary.PositionCount, summary.ByAsset.Sum(item => item.PositionCount));
        Assert.Equal(summary.TotalInvestedAmount, summary.ByAsset.Sum(item => item.TotalInvestedAmount));
        Assert.Equal(summary.TotalGrossAmount, summary.ByAsset.Sum(item => item.TotalGrossAmount));
        Assert.Equal(summary.TotalGrossYield, summary.ByAsset.Sum(item => item.TotalGrossYield));
        Assert.Equal(summary.TotalTaxAmount, summary.ByAsset.Sum(item => item.TotalTaxAmount));
        Assert.Equal(summary.TotalIofAmount, summary.ByAsset.Sum(item => item.TotalIofAmount));
        Assert.Equal(summary.TotalNetAmount, summary.ByAsset.Sum(item => item.TotalNetAmount));
        Assert.Equal(summary.TotalNetYield, summary.ByAsset.Sum(item => item.TotalNetYield));
    }

    [Fact]
    public void Por_titulo_e_vazio_com_a_lista_vazia()
    {
        // Arrange
        var positions = Array.Empty<(PositionProjection Projection, Guid AssetId, AssetType AssetType)>();

        // Act
        var summary = PortfolioSummary.Create(positions);

        // Assert
        Assert.Empty(summary.ByAsset);
    }

    [Fact]
    public void Por_titulo_com_um_titulo_so_tem_os_mesmos_totais_da_raiz()
    {
        // Arrange
        var assetId = Guid.NewGuid();
        var positions = new[]
        {
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_004.93m, taxAmount: 0.78m, netAmount: 1_002.67m, iofAmount: 1.48m), assetId, AssetType.Cdb),
            (CriarProjecao(investedAmount: 1_000m, grossAmount: 1_004.93m, taxAmount: 0.78m, netAmount: 1_002.67m, iofAmount: 1.48m), assetId, AssetType.Cdb)
        };

        // Act
        var summary = PortfolioSummary.Create(positions);

        // Assert
        var item = Assert.Single(summary.ByAsset);
        Assert.Equal(assetId, item.AssetId);
        Assert.Equal(summary.PositionCount, item.PositionCount);
        Assert.Equal(summary.TotalInvestedAmount, item.TotalInvestedAmount);
        Assert.Equal(summary.TotalGrossAmount, item.TotalGrossAmount);
        Assert.Equal(summary.TotalGrossYield, item.TotalGrossYield);
        Assert.Equal(summary.TotalTaxAmount, item.TotalTaxAmount);
        Assert.Equal(summary.TotalIofAmount, item.TotalIofAmount);
        Assert.Equal(summary.TotalNetAmount, item.TotalNetAmount);
        Assert.Equal(summary.TotalNetYield, item.TotalNetYield);
    }

    private static PositionProjection CriarProjecao(
        decimal investedAmount, decimal grossAmount, decimal taxAmount, decimal netAmount, decimal iofAmount = 0m,
        bool isProjected = false)
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
            IsEstimated: false,
            IsProjected: isProjected,
            NextTaxRate: null,
            NextTaxRateDate: null,
            IofFreeDate: null);
    }
}
