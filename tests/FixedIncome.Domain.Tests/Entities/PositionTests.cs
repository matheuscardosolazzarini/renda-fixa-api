using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Domain.Tests.Entities;

public class PositionTests
{
    private static readonly IndexRates ZeroRates = new(Cdi: 0m, Ipca: 0m);

    [Fact]
    public void Construcao_valida_atribui_asset_id_valor_investido_e_data_de_aporte()
    {
        // Arrange
        var asset = CriarAsset();
        var applicationDate = new DateOnly(2024, 6, 1);

        // Act
        var position = new Position(asset, 1000m, applicationDate);

        // Assert
        Assert.Equal(asset.Id, position.AssetId);
        Assert.Equal(1000m, position.InvestedAmount);
        Assert.Equal(applicationDate, position.ApplicationDate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Valor_investido_menor_ou_igual_a_zero_e_rejeitado(decimal investedAmount)
    {
        // Arrange
        var asset = CriarAsset();
        var applicationDate = new DateOnly(2024, 6, 1);

        // Act
        Action act = () => new Position(asset, investedAmount, applicationDate);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Aporte_antes_da_emissao_e_rejeitado()
    {
        // Arrange
        var asset = CriarAsset();
        var applicationDate = asset.IssueDate.AddDays(-1);

        // Act
        Action act = () => new Position(asset, 1000m, applicationDate);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Aporte_depois_do_vencimento_e_rejeitado()
    {
        // Arrange
        var asset = CriarAsset();
        var applicationDate = asset.MaturityDate.AddDays(1);

        // Act
        Action act = () => new Position(asset, 1000m, applicationDate);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Aporte_exatamente_na_data_de_emissao_e_aceito()
    {
        // Arrange
        var asset = CriarAsset();

        // Act
        var position = new Position(asset, 1000m, asset.IssueDate);

        // Assert
        Assert.Equal(asset.IssueDate, position.ApplicationDate);
    }

    [Fact]
    public void Aporte_exatamente_na_data_de_vencimento_e_aceito()
    {
        // Arrange
        var asset = CriarAsset();

        // Act
        var position = new Position(asset, 1000m, asset.MaturityDate);

        // Assert
        Assert.Equal(asset.MaturityDate, position.ApplicationDate);
    }

    [Fact]
    public void Projecao_com_data_de_referencia_anterior_ao_aporte_e_rejeitada()
    {
        // Arrange
        var asset = CriarAsset();
        var applicationDate = new DateOnly(2024, 6, 1);
        var position = new Position(asset, 1000m, applicationDate);
        var referenceDate = applicationDate.AddDays(-1);

        // Act
        Action act = () => position.Project(asset, referenceDate, ZeroRates);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Projecao_e_limitada_ao_vencimento_quando_data_de_referencia_o_ultrapassa()
    {
        // Arrange
        var asset = CriarAsset();
        var position = new Position(asset, 1000m, asset.IssueDate);
        var referenceDate = asset.MaturityDate.AddDays(100);
        var expectedElapsedDays = asset.MaturityDate.DayNumber - asset.IssueDate.DayNumber;

        // Act
        var projection = position.Project(asset, referenceDate, ZeroRates);

        // Assert
        Assert.Equal(expectedElapsedDays, projection.ElapsedDays);
    }

    [Fact]
    public void Projecao_de_titulo_isento_resulta_em_imposto_zero()
    {
        // Arrange
        var asset = CriarAsset(assetType: AssetType.Lci);
        var position = new Position(asset, 1000m, asset.IssueDate);
        var referenceDate = asset.IssueDate.AddDays(365);

        // Act
        var projection = position.Project(asset, referenceDate, ZeroRates);

        // Assert
        Assert.Equal(0m, projection.TaxRate);
        Assert.Equal(0m, projection.TaxAmount);
    }

    [Theory]
    [InlineData(180, 22.5)]
    [InlineData(360, 20.0)]
    [InlineData(720, 17.5)]
    [InlineData(900, 15.0)]
    public void Projecao_aplica_a_aliquota_conforme_a_faixa_de_prazo_da_tabela_regressiva(
        int elapsedDays, decimal expectedTaxRate)
    {
        // Arrange
        var asset = CriarAsset();
        var position = new Position(asset, 1000m, asset.IssueDate);
        var referenceDate = asset.IssueDate.AddDays(elapsedDays);

        // Act
        var projection = position.Project(asset, referenceDate, ZeroRates);

        // Assert
        Assert.Equal(expectedTaxRate, projection.TaxRate);
    }

    [Fact]
    public void Projecao_calcula_imposto_sobre_o_rendimento_e_nao_sobre_o_principal()
    {
        // Arrange: principal alto (100.000) e taxa baixa (1% a.a.) para gerar um
        // rendimento pequeno (1.000) em 365 dias. Tributar o principal em vez do
        // rendimento produziria um imposto 100x maior (17.500 em vez de 175), tornando
        // a diferença entre as duas interpretações evidente.
        var asset = CriarAsset(rate: 1m);
        var position = new Position(asset, 100_000m, asset.IssueDate);
        var referenceDate = asset.IssueDate.AddDays(365); // faixa de 17,5%

        // Act
        var projection = position.Project(asset, referenceDate, ZeroRates);

        // Assert
        Assert.Equal(1_000.00m, projection.GrossYield, precision: 2);
        Assert.Equal(175.00m, projection.TaxAmount, precision: 2);
        Assert.Equal(100_825.00m, projection.NetAmount, precision: 2);
    }

    [Fact]
    public void Projecao_com_capitalizacao_composta_calcula_bruto_rendimento_imposto_e_liquido()
    {
        // Arrange: taxa pré-fixada de 10% ao ano, aporte no dia da emissão, 365 dias
        // corridos até a referência — fator de capitalização exato de 1,10.
        var asset = CriarAsset(rate: 10m);
        var position = new Position(asset, 1000m, asset.IssueDate);
        var referenceDate = asset.IssueDate.AddDays(365);

        // Act
        var projection = position.Project(asset, referenceDate, ZeroRates);

        // Assert
        Assert.Equal(1000m, projection.InvestedAmount, precision: 2);
        Assert.Equal(1100.00m, projection.GrossAmount, precision: 2);
        Assert.Equal(100.00m, projection.GrossYield, precision: 2);
        Assert.Equal(17.5m, projection.TaxRate); // 365 dias cai na faixa de 361 a 720 dias
        Assert.Equal(17.50m, projection.TaxAmount, precision: 2);
        Assert.Equal(1082.50m, projection.NetAmount, precision: 2);
        Assert.Equal(365, projection.ElapsedDays);
    }

    private static FixedIncomeAsset CriarAsset(decimal rate = 10m, AssetType assetType = AssetType.Cdb)
    {
        return new FixedIncomeAsset(
            "Título Teste",
            "Emissor Teste",
            assetType,
            IndexType.PreFixed,
            rate,
            new DateOnly(2020, 1, 1),
            new DateOnly(2030, 1, 1));
    }
}
