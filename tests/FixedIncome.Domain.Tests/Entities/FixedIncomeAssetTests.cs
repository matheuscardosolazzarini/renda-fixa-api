using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Domain.Tests.Entities;

public class FixedIncomeAssetTests
{
    [Fact]
    public void Construcao_valida_atribui_todas_as_propriedades()
    {
        // Arrange
        var issueDate = new DateOnly(2024, 1, 1);
        var maturityDate = new DateOnly(2026, 1, 1);

        // Act
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 12m, issueDate, maturityDate);

        // Assert
        Assert.Equal("CDB Banco X", asset.Name);
        Assert.Equal("Banco X", asset.Issuer);
        Assert.Equal(AssetType.Cdb, asset.AssetType);
        Assert.Equal(IndexType.PreFixed, asset.IndexType);
        Assert.Equal(12m, asset.Rate);
        Assert.Equal(issueDate, asset.IssueDate);
        Assert.Equal(maturityDate, asset.MaturityDate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Nome_vazio_ou_em_branco_e_rejeitado(string name)
    {
        // Arrange
        var issueDate = new DateOnly(2024, 1, 1);
        var maturityDate = new DateOnly(2026, 1, 1);

        // Act
        Action act = () => new FixedIncomeAsset(
            name, "Banco X", AssetType.Cdb, IndexType.PreFixed, 12m, issueDate, maturityDate);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Nome_com_mais_de_120_caracteres_e_rejeitado()
    {
        // Arrange
        var name = new string('A', 121);
        var issueDate = new DateOnly(2024, 1, 1);
        var maturityDate = new DateOnly(2026, 1, 1);

        // Act
        Action act = () => new FixedIncomeAsset(
            name, "Banco X", AssetType.Cdb, IndexType.PreFixed, 12m, issueDate, maturityDate);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Emissor_vazio_ou_em_branco_e_rejeitado(string issuer)
    {
        // Arrange
        var issueDate = new DateOnly(2024, 1, 1);
        var maturityDate = new DateOnly(2026, 1, 1);

        // Act
        Action act = () => new FixedIncomeAsset(
            "CDB Banco X", issuer, AssetType.Cdb, IndexType.PreFixed, 12m, issueDate, maturityDate);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Emissor_com_mais_de_120_caracteres_e_rejeitado()
    {
        // Arrange
        var issuer = new string('A', 121);
        var issueDate = new DateOnly(2024, 1, 1);
        var maturityDate = new DateOnly(2026, 1, 1);

        // Act
        Action act = () => new FixedIncomeAsset(
            "CDB Banco X", issuer, AssetType.Cdb, IndexType.PreFixed, 12m, issueDate, maturityDate);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Taxa_menor_ou_igual_a_zero_e_rejeitada(decimal rate)
    {
        // Arrange
        var issueDate = new DateOnly(2024, 1, 1);
        var maturityDate = new DateOnly(2026, 1, 1);

        // Act
        Action act = () => new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, rate, issueDate, maturityDate);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Vencimento_anterior_ou_igual_a_emissao_e_rejeitado(int offsetDays)
    {
        // Arrange
        var issueDate = new DateOnly(2024, 1, 1);
        var maturityDate = issueDate.AddDays(offsetDays);

        // Act
        Action act = () => new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 12m, issueDate, maturityDate);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void ResolveAnnualRate_prefixado_retorna_a_propria_taxa()
    {
        // Arrange
        var asset = CriarAsset(IndexType.PreFixed, rate: 12m);
        var rates = new IndexRates(Cdi: 13m, Ipca: 4m);

        // Act
        var annualRate = asset.ResolveAnnualRate(rates);

        // Assert
        Assert.Equal(12m, annualRate);
    }

    [Fact]
    public void ResolveAnnualRate_cdi_aplica_o_percentual_sobre_o_cdi()
    {
        // Arrange
        var asset = CriarAsset(IndexType.Cdi, rate: 110m); // 110% do CDI
        var rates = new IndexRates(Cdi: 13m, Ipca: 4m);

        // Act
        var annualRate = asset.ResolveAnnualRate(rates);

        // Assert
        Assert.Equal(14.3m, annualRate); // 13 * 1,10
    }

    [Fact]
    public void ResolveAnnualRate_ipca_soma_a_taxa_a_variacao_do_ipca()
    {
        // Arrange
        var asset = CriarAsset(IndexType.Ipca, rate: 6m);
        var rates = new IndexRates(Cdi: 13m, Ipca: 4m);

        // Act
        var annualRate = asset.ResolveAnnualRate(rates);

        // Assert
        Assert.Equal(10m, annualRate); // 4 + 6
    }

    [Theory]
    [InlineData(AssetType.Cdb, false)]
    [InlineData(AssetType.Lci, true)]
    [InlineData(AssetType.Lca, true)]
    [InlineData(AssetType.TreasuryBond, false)]
    public void IsTaxExempt_reflete_a_isencao_por_tipo_de_titulo(AssetType assetType, bool expectedExempt)
    {
        // Arrange
        var asset = CriarAsset(IndexType.PreFixed, rate: 10m, assetType: assetType);

        // Act
        var isExempt = asset.IsTaxExempt();

        // Assert
        Assert.Equal(expectedExempt, isExempt);
    }

    [Fact]
    public void Update_com_aportes_altera_nome_emissor_e_taxa_mantendo_as_datas()
    {
        // Arrange
        var asset = CriarAsset(IndexType.PreFixed, rate: 12m);

        // Act
        asset.Update("Novo Nome", "Novo Emissor", 15m, asset.IssueDate, asset.MaturityDate, hasPositions: true);

        // Assert
        Assert.Equal("Novo Nome", asset.Name);
        Assert.Equal("Novo Emissor", asset.Issuer);
        Assert.Equal(15m, asset.Rate);
    }

    [Fact]
    public void Update_com_aportes_rejeita_alteracao_da_data_de_emissao()
    {
        // Arrange
        var asset = CriarAsset(IndexType.PreFixed, rate: 12m);
        var novaIssueDate = asset.IssueDate.AddDays(1);

        // Act
        Action act = () => asset.Update(
            asset.Name, asset.Issuer, asset.Rate, novaIssueDate, asset.MaturityDate, hasPositions: true);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Update_com_aportes_rejeita_alteracao_da_data_de_vencimento()
    {
        // Arrange
        var asset = CriarAsset(IndexType.PreFixed, rate: 12m);
        var novaMaturityDate = asset.MaturityDate.AddDays(1);

        // Act
        Action act = () => asset.Update(
            asset.Name, asset.Issuer, asset.Rate, asset.IssueDate, novaMaturityDate, hasPositions: true);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Update_sem_aportes_permite_alterar_as_datas()
    {
        // Arrange
        var asset = CriarAsset(IndexType.PreFixed, rate: 12m);
        var novaIssueDate = asset.IssueDate.AddDays(1);
        var novaMaturityDate = asset.MaturityDate.AddDays(1);

        // Act
        asset.Update(asset.Name, asset.Issuer, asset.Rate, novaIssueDate, novaMaturityDate, hasPositions: false);

        // Assert
        Assert.Equal(novaIssueDate, asset.IssueDate);
        Assert.Equal(novaMaturityDate, asset.MaturityDate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_com_nome_vazio_ou_em_branco_e_rejeitado(string name)
    {
        // Arrange
        var asset = CriarAsset(IndexType.PreFixed, rate: 12m);

        // Act
        Action act = () => asset.Update(
            name, asset.Issuer, asset.Rate, asset.IssueDate, asset.MaturityDate, hasPositions: false);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_com_emissor_vazio_ou_em_branco_e_rejeitado(string issuer)
    {
        // Arrange
        var asset = CriarAsset(IndexType.PreFixed, rate: 12m);

        // Act
        Action act = () => asset.Update(
            asset.Name, issuer, asset.Rate, asset.IssueDate, asset.MaturityDate, hasPositions: false);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Update_com_taxa_menor_ou_igual_a_zero_e_rejeitada(decimal rate)
    {
        // Arrange
        var asset = CriarAsset(IndexType.PreFixed, rate: 12m);

        // Act
        Action act = () => asset.Update(
            asset.Name, asset.Issuer, rate, asset.IssueDate, asset.MaturityDate, hasPositions: false);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Update_com_vencimento_anterior_ou_igual_a_emissao_e_rejeitado()
    {
        // Arrange
        var asset = CriarAsset(IndexType.PreFixed, rate: 12m);

        // Act
        Action act = () => asset.Update(
            asset.Name, asset.Issuer, asset.Rate, asset.IssueDate, asset.IssueDate, hasPositions: false);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    private static FixedIncomeAsset CriarAsset(IndexType indexType, decimal rate, AssetType assetType = AssetType.Cdb)
    {
        return new FixedIncomeAsset(
            "Título Teste",
            "Emissor Teste",
            assetType,
            indexType,
            rate,
            new DateOnly(2024, 1, 1),
            new DateOnly(2026, 1, 1));
    }
}
