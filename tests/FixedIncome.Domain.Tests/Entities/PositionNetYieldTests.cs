using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using static FixedIncome.Domain.Tests.TestData.TestMarket;

namespace FixedIncome.Domain.Tests.Entities;

// RN-15: NetYield = NetAmount - InvestedAmount. Valores esperados dos casos 1a, 1b e 2 da seção
// "Casos de referência" de docs/SPEC-F13-EXECUCAO.md, calculados à mão e não pela saída do código.
public class PositionNetYieldTests
{
    [Fact]
    public void Net_yield_de_cdb_prefixado_com_iof_e_ir_e_o_liquido_menos_o_investido()
    {
        // Arrange: caso 1a. 13 DU, fator 1,10^(13/252), bruto 1.004,93, IOF 30% de 4,93 = 1,48,
        // IR 22,5% sobre 3,45 = 0,78, líquido 1.002,67.
        var asset = new FixedIncomeAsset(
            "CDB Pré", "Emissor Teste", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1000m, new DateOnly(2025, 4, 1));

        // Act
        var projection = position.Project(asset, new DateOnly(2025, 4, 22), Create());

        // Assert
        Assert.Equal(1_002.67m, projection.NetAmount);
        Assert.Equal(2.67m, projection.NetYield);
    }

    [Theory]
    [InlineData(100, 1_000.94, 0.94)]
    [InlineData(110, 1_001.03, 1.03)]
    public void Net_yield_de_cdi_projetado_fecha_com_o_liquido_dos_casos_1_e_2_da_f12(
        int percentOfCdi, decimal netAmount, decimal netYield)
    {
        // Arrange: CDB do caso 1 (100%) e do caso 2 (110%) da F12, 10 dias corridos, IsProjected.
        var rates = new[]
        {
            Cdi("2026-11-16", 0.050788m), Cdi("2026-11-17", 0.050788m),
            Cdi("2026-11-18", 0.050788m), Cdi("2026-11-19", 0.050788m)
        };
        var market = Create(new DateOnly(2026, 11, 19), rates);
        var asset = new FixedIncomeAsset(
            "CDB CDI", "Emissor Teste", AssetType.Cdb, IndexType.Cdi, percentOfCdi,
            new DateOnly(2026, 1, 1), new DateOnly(2028, 1, 1));
        var position = new Position(asset, 1000m, new DateOnly(2026, 11, 16));

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 11, 26), market);

        // Assert
        Assert.True(projection.IsProjected);
        Assert.Equal(netAmount, projection.NetAmount);
        Assert.Equal(netYield, projection.NetYield);
    }

    [Fact]
    public void Net_yield_e_negativo_no_ipca_mais_com_taxa_real_baixa_num_mes_de_deflacao()
    {
        // Arrange: caso 2a. IPCA+ 2% a.a., IPCA de 08/2026 = -0,32, 20 DU de 21: fator
        // 0,9968^(20/21) x 1,02^(20/252) = 0,9985202274, bruto 998,52. Sem rendimento não há IOF
        // nem IR, mas as alíquotas saem pela tabela (28 dias corridos: IOF 6%, IR 22,5%).
        var market = Create(Ipca("2026-08-01", -0.32m));
        var asset = new FixedIncomeAsset(
            "CDB IPCA+", "Emissor Teste", AssetType.Cdb, IndexType.Ipca, 2m,
            new DateOnly(2026, 1, 1), new DateOnly(2028, 1, 1));
        var position = new Position(asset, 1000m, new DateOnly(2026, 8, 3));

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 8, 31), market);

        // Assert
        Assert.Equal(998.52m, projection.GrossAmount);
        Assert.Equal(-1.48m, projection.GrossYield);
        Assert.Equal(6m, projection.IofRate);
        Assert.Equal(22.5m, projection.TaxRate);
        Assert.Equal(0m, projection.IofAmount);
        Assert.Equal(0m, projection.TaxAmount);
        Assert.Equal(998.52m, projection.NetAmount);
        Assert.Equal(-1.48m, projection.NetYield);
    }

    [Fact]
    public void Net_yield_e_zero_no_dia_do_aporte()
    {
        // Arrange: caso 2b. Referência igual ao aporte: nenhum dia útil decorrido, bruto 1.000,00.
        var market = Create(Ipca("2026-08-01", -0.32m));
        var asset = new FixedIncomeAsset(
            "CDB IPCA+", "Emissor Teste", AssetType.Cdb, IndexType.Ipca, 2m,
            new DateOnly(2026, 1, 1), new DateOnly(2028, 1, 1));
        var position = new Position(asset, 1000m, new DateOnly(2026, 8, 3));

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 8, 3), market);

        // Assert
        Assert.Equal(0, projection.BusinessDays);
        Assert.Equal(1_000.00m, projection.GrossAmount);
        Assert.Equal(0m, projection.IofRate);
        Assert.Equal(22.5m, projection.TaxRate);
        Assert.Equal(1_000.00m, projection.NetAmount);
        Assert.Equal(0m, projection.NetYield);
    }

    [Theory]
    [InlineData(AssetType.Lci, 1_004.93, 1.48, 3.45)]
    [InlineData(AssetType.Lca, 1_004.93, 0, 4.93)]
    public void Net_yield_de_lci_paga_iof_e_o_de_lca_nao_paga_e_nenhum_dos_dois_paga_ir(
        AssetType assetType, decimal grossAmount, decimal iofAmount, decimal netYield)
    {
        // Arrange: caso 5 em d = 21. Mesmo prazo e fator do caso 1a (bruto 1.004,93); a LCI paga IOF
        // de 30% sobre 4,93 = 1,48, a LCA não paga; as duas são isentas de IR.
        var asset = new FixedIncomeAsset(
            "Título isento", "Emissor Teste", assetType, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 4, 1));
        var position = new Position(asset, 1000m, new DateOnly(2025, 4, 1));

        // Act
        var projection = position.Project(asset, new DateOnly(2025, 4, 22), Create());

        // Assert
        Assert.Equal(grossAmount, projection.GrossAmount);
        Assert.Equal(iofAmount, projection.IofAmount);
        Assert.Equal(0m, projection.TaxAmount);
        Assert.Equal(netYield, projection.NetYield);
    }
}
