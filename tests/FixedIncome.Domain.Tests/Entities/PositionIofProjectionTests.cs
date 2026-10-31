using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using static FixedIncome.Domain.Tests.TestData.TestMarket;

namespace FixedIncome.Domain.Tests.Entities;

// RN-11: IOF regressivo sobre o rendimento, apurado antes do IR. Valores esperados do caso 5
// da seção "Casos de referência" de docs/SPEC-F7-EXECUCAO.md, na convenção C6b.
public class PositionIofProjectionTests
{
    private static readonly DateOnly ApplicationDate = new(2026, 3, 2);

    // Caso 5.B, CDB: R$ 1.000 PreFixed 12% a.a. aplicados em 2026-03-02. Dia (corrido), alíquota
    // de IOF, bruto, rendimento, IOF, IR e líquido.
    public static TheoryData<int, decimal, decimal, decimal, decimal, decimal, decimal> CdbCases => new()
    {
        { 0, 0m, 1000.00m, 0.00m, 0.00m, 0.00m, 1000.00m },    // 5.1
        { 1, 96m, 1000.45m, 0.45m, 0.43m, 0.00m, 1000.02m },   // 5.2
        { 15, 50m, 1004.96m, 4.96m, 2.48m, 0.56m, 1001.92m },  // 5.4
        { 29, 3m, 1009.49m, 9.49m, 0.28m, 2.07m, 1007.14m },   // 5.6
        { 30, 0m, 1009.94m, 9.94m, 0.00m, 2.24m, 1007.70m },   // 5.8: fronteira, IOF deixa de incidir
        { 31, 0m, 1010.40m, 10.40m, 0.00m, 2.34m, 1008.06m }   // 5.10
    };

    [Theory]
    [MemberData(nameof(CdbCases))]
    public void Cdb_prefixado_aplica_iof_regressivo_e_depois_ir_sobre_o_rendimento_menos_o_iof(
        int day, decimal iofRate, decimal gross, decimal yield, decimal iof, decimal tax, decimal net)
    {
        // Arrange
        var asset = CriarAsset(AssetType.Cdb);
        var position = new Position(asset, 1000m, ApplicationDate);

        // Act
        var projection = position.Project(asset, ApplicationDate.AddDays(day), Create());

        // Assert
        Assert.Equal(day, projection.ElapsedDays);
        Assert.Equal(iofRate, projection.IofRate);
        Assert.Equal(22.5m, projection.TaxRate);
        Assert.Equal(gross, projection.GrossAmount);
        Assert.Equal(yield, projection.GrossYield);
        Assert.Equal(iof, projection.IofAmount);
        Assert.Equal(tax, projection.TaxAmount);
        Assert.Equal(net, projection.NetAmount);
        Assert.Equal(projection.GrossAmount - projection.IofAmount - projection.TaxAmount, projection.NetAmount);
    }

    // Caso 5.B, LCI: paga IOF e é isenta de IR.
    public static TheoryData<int, decimal, decimal, decimal, decimal, decimal> LciCases => new()
    {
        { 1, 96m, 1000.45m, 0.45m, 0.43m, 1000.02m },    // 5.3
        { 15, 50m, 1004.96m, 4.96m, 2.48m, 1002.48m },   // 5.5
        { 29, 3m, 1009.49m, 9.49m, 0.28m, 1009.21m },    // 5.7
        { 30, 0m, 1009.94m, 9.94m, 0.00m, 1009.94m },    // 5.9
        { 31, 0m, 1010.40m, 10.40m, 0.00m, 1010.40m }    // 5.11
    };

    [Theory]
    [MemberData(nameof(LciCases))]
    public void Lci_prefixada_paga_iof_e_nao_paga_ir(
        int day, decimal iofRate, decimal gross, decimal yield, decimal iof, decimal net)
    {
        // Arrange
        var asset = CriarAsset(AssetType.Lci);
        var position = new Position(asset, 1000m, ApplicationDate);

        // Act
        var projection = position.Project(asset, ApplicationDate.AddDays(day), Create());

        // Assert
        Assert.Equal(iofRate, projection.IofRate);
        Assert.Equal(0m, projection.TaxRate);
        Assert.Equal(gross, projection.GrossAmount);
        Assert.Equal(yield, projection.GrossYield);
        Assert.Equal(iof, projection.IofAmount);
        Assert.Equal(0m, projection.TaxAmount);
        Assert.Equal(net, projection.NetAmount);
    }

    [Fact]
    public void Lca_tem_iof_zero_mesmo_dentro_dos_trinta_primeiros_dias()
    {
        // Arrange: caso 5.5 com LCA. Decreto 6.306/2007, art. 32, § 2º, V.
        var asset = CriarAsset(AssetType.Lca);
        var position = new Position(asset, 1000m, ApplicationDate);

        // Act
        var projection = position.Project(asset, ApplicationDate.AddDays(15), Create());

        // Assert
        Assert.Equal(1004.96m, projection.GrossAmount);
        Assert.Equal(4.96m, projection.GrossYield);
        Assert.Equal(0m, projection.IofRate);
        Assert.Equal(0m, projection.IofAmount);
        Assert.Equal(0m, projection.TaxAmount);
        Assert.Equal(1004.96m, projection.NetAmount);
    }

    [Fact]
    public void Tesouro_direto_paga_iof_nos_primeiros_trinta_dias()
    {
        // Arrange: Tesouro Direto não tem a redução a zero da LCA (RN-11); mesmo cálculo do CDB.
        var asset = CriarAsset(AssetType.TreasuryBond);
        var position = new Position(asset, 1000m, ApplicationDate);

        // Act
        var projection = position.Project(asset, ApplicationDate.AddDays(15), Create());

        // Assert
        Assert.Equal(50m, projection.IofRate);
        Assert.Equal(2.48m, projection.IofAmount);
        Assert.Equal(0.56m, projection.TaxAmount);
        Assert.Equal(1001.92m, projection.NetAmount);
    }

    private static FixedIncomeAsset CriarAsset(AssetType assetType)
    {
        return new FixedIncomeAsset(
            "Título Teste", "Emissor Teste", assetType, IndexType.PreFixed, 12m,
            ApplicationDate, ApplicationDate.AddYears(1));
    }
}
