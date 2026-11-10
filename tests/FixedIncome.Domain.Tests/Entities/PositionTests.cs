using System.Globalization;
using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Domain.Tests.TestData;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Domain.Tests.Entities;

public class PositionTests
{
    [Fact]
    public void Titulo_nulo_e_rejeitado_informando_que_o_asset_e_obrigatorio()
    {
        // Arrange
        FixedIncomeAsset? asset = null;

        // Act
        Action act = () => new Position(asset!, 1000m, new DateOnly(2024, 6, 1));

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Contains("Asset é obrigatório", exception.Message);
    }

    [Fact]
    public void Valor_investido_zero_informa_que_deve_ser_maior_que_zero()
    {
        // Arrange
        var asset = CriarAsset();

        // Act
        Action act = () => new Position(asset, 0m, new DateOnly(2024, 6, 1));

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Contains("InvestedAmount deve ser maior que zero", exception.Message);
    }

    [Fact]
    public void Aporte_fora_do_periodo_do_titulo_informa_que_deve_estar_entre_emissao_e_vencimento()
    {
        // Arrange
        var asset = CriarAsset();

        // Act
        Action act = () => new Position(asset, 1000m, asset.MaturityDate.AddDays(1));

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Contains("ApplicationDate deve estar entre a emissão e o vencimento", exception.Message);
    }

    [Fact]
    public void Projecao_sem_dados_de_mercado_e_rejeitada_com_argument_null_exception()
    {
        // Arrange
        var asset = CriarAsset();
        var position = new Position(asset, 1000m, asset.IssueDate);

        // Act
        Action act = () => position.Project(asset, asset.IssueDate.AddDays(30), null!);

        // Assert
        var exception = Assert.Throws<ArgumentNullException>(act);
        Assert.Equal("market", exception.ParamName);
    }

    [Fact]
    public void Projecao_com_titulo_de_outra_posicao_informa_que_o_titulo_nao_corresponde()
    {
        // Arrange
        var asset = CriarAsset();
        var otherAsset = CriarAsset();
        var position = new Position(asset, 1000m, asset.IssueDate);

        // Act
        Action act = () => position.Project(otherAsset, asset.IssueDate.AddDays(30), Market);

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Contains("título informado não corresponde a esta posição", exception.Message);
    }

    [Fact]
    public void Projecao_com_data_de_referencia_anterior_ao_aporte_informa_que_a_data_nao_pode_ser_anterior()
    {
        // Arrange
        var asset = CriarAsset();
        var applicationDate = new DateOnly(2024, 6, 1);
        var position = new Position(asset, 1000m, applicationDate);

        // Act
        Action act = () => position.Project(asset, applicationDate.AddDays(-1), Market);

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Contains("data de referência não pode ser anterior à data de aporte", exception.Message);
    }

    [Fact]
    public void Projecao_de_titulo_com_indexador_fora_do_enum_informa_que_o_indexador_nao_e_suportado()
    {
        // Arrange: o valor 99 não existe em IndexType; a entidade do título não valida o enum.
        var asset = CriarAsset(indexType: (IndexType)99);
        var position = new Position(asset, 1000m, asset.IssueDate);

        // Act
        Action act = () => position.Project(asset, asset.IssueDate.AddDays(30), Market);

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Contains("IndexType não suportado: 99", exception.Message);
    }

    [Fact]
    public void Projecao_de_ipca_sem_nenhuma_cotacao_publicada_informa_que_nao_ha_cotacao_para_estimar()
    {
        // Arrange: 2026-03-02 a 2026-03-10 tem dias úteis, então o mês de março é consultado
        // e, sem nenhum IPCA na série, não há última variação para estimar.
        var asset = CriarAsset(indexType: IndexType.Ipca, issueDate: new DateOnly(2026, 3, 2));
        var position = new Position(asset, 1000m, asset.IssueDate);

        // Act
        Action act = () => position.Project(asset, new DateOnly(2026, 3, 10), Market);

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Contains("Não há cotação de IPCA publicada", exception.Message);
    }

    private static readonly MarketData Market = TestMarket.Create();

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

    // B-01: InvestedAmount é numeric(18,2), então o maior valor aceito é 9999999999999999,99.
    [Theory]
    [InlineData("9999999999999999.99")]
    [InlineData("9999999999999999")]
    public void Valor_investido_no_ultimo_valor_que_cabe_na_coluna_e_aceito(string amount)
    {
        // Arrange
        var asset = CriarAsset();
        var value = decimal.Parse(amount, CultureInfo.InvariantCulture);

        // Act
        var position = new Position(asset, value, new DateOnly(2024, 6, 1));

        // Assert
        Assert.Equal(value, position.InvestedAmount);
    }

    [Theory]
    [InlineData("10000000000000000")]
    [InlineData("10000000000000000.01")]
    [InlineData("100000000000000000000")]
    public void Valor_investido_a_partir_de_1e16_e_rejeitado_citando_o_limite(string amount)
    {
        // Arrange
        var asset = CriarAsset();
        var value = decimal.Parse(amount, CultureInfo.InvariantCulture);

        // Act
        Action act = () => new Position(asset, value, new DateOnly(2024, 6, 1));

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Equal("InvestedAmount deve ser menor que 10000000000000000.", exception.Message);
    }

    // B-07: InvestedAmount guarda 2 casas; 10,999 seria gravado como 11,00 e ecoado como 10,999.
    [Theory]
    [InlineData("12")]
    [InlineData("12.5")]
    [InlineData("12.50")]
    [InlineData("12.99")]
    [InlineData("12.500")] // zeros à direita não contam como casa decimal
    public void Valor_investido_com_ate_2_casas_decimais_e_aceito(string amount)
    {
        // Arrange
        var asset = CriarAsset();
        var value = decimal.Parse(amount, CultureInfo.InvariantCulture);

        // Act
        var position = new Position(asset, value, new DateOnly(2024, 6, 1));

        // Assert
        Assert.Equal(value, position.InvestedAmount);
    }

    [Theory]
    [InlineData("10.999")]
    [InlineData("12.001")]
    [InlineData("0.001")]
    public void Valor_investido_com_mais_de_2_casas_decimais_e_rejeitado(string amount)
    {
        // Arrange
        var asset = CriarAsset();
        var value = decimal.Parse(amount, CultureInfo.InvariantCulture);

        // Act
        Action act = () => new Position(asset, value, new DateOnly(2024, 6, 1));

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Equal("InvestedAmount deve ter no máximo 2 casas decimais.", exception.Message);
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
        Action act = () => position.Project(asset, referenceDate, Market);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Projecao_com_titulo_de_outra_posicao_e_rejeitada()
    {
        // Arrange
        var asset = CriarAsset();
        var otherAsset = CriarAsset();
        var position = new Position(asset, 1000m, asset.IssueDate);

        // Act
        Action act = () => position.Project(otherAsset, asset.IssueDate.AddDays(30), Market);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Projecao_e_limitada_ao_vencimento_quando_data_de_referencia_o_ultrapassa()
    {
        // Arrange: [2020-01-01, 2030-01-01) tem 3653 dias corridos e 2504 dias úteis.
        var asset = CriarAsset();
        var position = new Position(asset, 1000m, asset.IssueDate);
        var referenceDate = asset.MaturityDate.AddDays(100);

        // Act
        var projection = position.Project(asset, referenceDate, Market);

        // Assert
        Assert.Equal(3653, projection.ElapsedDays);
        Assert.Equal(2504, projection.BusinessDays);
    }

    [Fact]
    public void Projecao_de_titulo_isento_resulta_em_imposto_zero()
    {
        // Arrange
        var asset = CriarAsset(assetType: AssetType.Lci);
        var position = new Position(asset, 1000m, asset.IssueDate);
        var referenceDate = asset.IssueDate.AddDays(365);

        // Act
        var projection = position.Project(asset, referenceDate, Market);

        // Assert
        Assert.Equal(0m, projection.TaxRate);
        Assert.Equal(0m, projection.TaxAmount);
        Assert.Equal(0m, projection.IofAmount); // 365 dias: fora da janela do IOF
    }

    [Theory]
    [InlineData(180, 22.5)]
    [InlineData(181, 20.0)]
    [InlineData(360, 20.0)]
    [InlineData(361, 17.5)]
    [InlineData(720, 17.5)]
    [InlineData(721, 15.0)]
    [InlineData(900, 15.0)]
    public void Projecao_aplica_a_aliquota_conforme_a_faixa_de_prazo_da_tabela_regressiva(
        int elapsedDays, decimal expectedTaxRate)
    {
        // Arrange: a alíquota depende só dos dias corridos; o calendário cobre até 2030.
        var asset = CriarAsset();
        var position = new Position(asset, 1000m, asset.IssueDate);
        var referenceDate = asset.IssueDate.AddDays(elapsedDays);

        // Act
        var projection = position.Project(asset, referenceDate, Market);

        // Assert
        Assert.Equal(expectedTaxRate, projection.TaxRate);
    }

    [Fact]
    public void Projecao_calcula_imposto_sobre_o_rendimento_e_nao_sobre_o_principal()
    {
        // Arrange: caso 1.1 da spec. Principal alto (100.000) e taxa baixa (1% a.a.) para
        // gerar um rendimento pequeno (992,02) em 365 dias corridos e 250 úteis em 2020.
        // Tributar o principal em vez do rendimento produziria um imposto 100x maior.
        var asset = CriarAsset(rate: 1m);
        var position = new Position(asset, 100_000m, asset.IssueDate);
        var referenceDate = new DateOnly(2020, 12, 31); // faixa de 17,5%

        // Act
        var projection = position.Project(asset, referenceDate, Market);

        // Assert
        Assert.Equal(100_992.02m, projection.GrossAmount);
        Assert.Equal(992.02m, projection.GrossYield);
        Assert.Equal(17.5m, projection.TaxRate);
        Assert.Equal(173.60m, projection.TaxAmount);
        Assert.Equal(100_818.42m, projection.NetAmount);
        Assert.Equal(250, projection.BusinessDays);
    }

    [Fact]
    public void Projecao_com_capitalizacao_composta_em_base_252_calcula_bruto_rendimento_imposto_e_liquido()
    {
        // Arrange: caso 1.2 da spec. 10% a.a. em 2020 tem 250 dias úteis, então o fator é
        // 1,10^(250/252) = 1,0991682416..., e não 1,10. O líquido de 1.081,82 vem da ordem de
        // arredondamento da v2: IR = round2(99,17 x 17,5%) = 17,35 e líquido = 1.099,17 - 17,35.
        var asset = CriarAsset(rate: 10m);
        var position = new Position(asset, 1000m, asset.IssueDate);
        var referenceDate = new DateOnly(2020, 12, 31);

        // Act
        var projection = position.Project(asset, referenceDate, Market);

        // Assert
        Assert.Equal(1000m, projection.InvestedAmount);
        Assert.Equal(1099.17m, projection.GrossAmount);
        Assert.Equal(99.17m, projection.GrossYield);
        Assert.Equal(17.5m, projection.TaxRate); // 365 dias cai na faixa de 361 a 720 dias
        Assert.Equal(17.35m, projection.TaxAmount);
        Assert.Equal(1081.82m, projection.NetAmount);
        Assert.Equal(365, projection.ElapsedDays);
        Assert.Equal(250, projection.BusinessDays);
        Assert.False(projection.IsEstimated);
    }

    [Fact]
    public void Projecao_de_um_ano_com_252_dias_uteis_rende_exatamente_a_taxa_anual()
    {
        // Arrange: caso 1.3 da spec. 2025 tem 252 dias úteis, então 1,10^(252/252) é 1,10 exato.
        // Este teste é a sentinela do feriado de 20/11/2025 (Consciência Negra): sem ele no
        // calendário o intervalo teria 253 dias úteis (ver o teste seguinte).
        var asset = CriarAsset(rate: 10m, issueDate: new DateOnly(2025, 1, 1));
        var position = new Position(asset, 1000m, asset.IssueDate);
        var referenceDate = new DateOnly(2026, 1, 1);

        // Act
        var projection = position.Project(asset, referenceDate, Market);

        // Assert
        Assert.Equal(252, projection.BusinessDays);
        Assert.Equal(1100.00m, projection.GrossAmount);
        Assert.Equal(100.00m, projection.GrossYield);
        Assert.Equal(17.5m, projection.TaxRate);
        Assert.Equal(17.50m, projection.TaxAmount);
        Assert.Equal(1082.50m, projection.NetAmount);
    }

    [Fact]
    public void Calendario_sem_o_feriado_de_20_de_novembro_de_2025_muda_o_rendimento()
    {
        // Arrange: caso 1.3, sentinela. Sem o feriado o intervalo passa a ter 253 dias úteis e
        // o bruto vira 1.000 x 1,10^(253/252) = 1.100,42.
        var market = TestMarket.CreateWithoutHoliday(new DateOnly(2025, 11, 20));
        var asset = CriarAsset(rate: 10m, issueDate: new DateOnly(2025, 1, 1));
        var position = new Position(asset, 1000m, asset.IssueDate);

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 1, 1), market);

        // Assert
        Assert.Equal(253, projection.BusinessDays);
        Assert.Equal(1100.42m, projection.GrossAmount);
    }

    [Theory]
    [InlineData(8, "540.00")]
    [InlineData(10, "550.00")]
    public void Lci_de_um_ano_com_252_dias_uteis_nao_paga_ir_nem_iof(int rate, string expectedAmount)
    {
        // Arrange: caso 1.3 da spec, LCI de R$ 500. Bruto e líquido coincidem.
        var expected = decimal.Parse(expectedAmount, CultureInfo.InvariantCulture);
        var asset = CriarAsset(rate: rate, assetType: AssetType.Lci, issueDate: new DateOnly(2025, 1, 1));
        var position = new Position(asset, 500m, asset.IssueDate);

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 1, 1), Market);

        // Assert
        Assert.Equal(expected, projection.GrossAmount);
        Assert.Equal(0m, projection.TaxAmount);
        Assert.Equal(0m, projection.IofAmount);
        Assert.Equal(expected, projection.NetAmount);
    }

    [Fact]
    public void Projecao_sem_decorrer_tempo_tem_rendimento_zero_e_aliquotas_da_tabela()
    {
        // Arrange: caso 5.1 da spec (dia 0). Rendimento zero não gera imposto, mas a alíquota
        // de IR continua a da tabela pelo prazo (achado I6).
        var asset = CriarAsset(rate: 12m, issueDate: new DateOnly(2026, 3, 2));
        var position = new Position(asset, 1000m, asset.IssueDate);

        // Act
        var projection = position.Project(asset, asset.IssueDate, Market);

        // Assert
        Assert.Equal(1000.00m, projection.GrossAmount);
        Assert.Equal(0m, projection.GrossYield);
        Assert.Equal(0m, projection.IofRate);
        Assert.Equal(0m, projection.IofAmount);
        Assert.Equal(22.5m, projection.TaxRate);
        Assert.Equal(0m, projection.TaxAmount);
        Assert.Equal(1000.00m, projection.NetAmount);
        Assert.Equal(0, projection.BusinessDays);
    }

    private static FixedIncomeAsset CriarAsset(
        decimal rate = 10m,
        AssetType assetType = AssetType.Cdb,
        DateOnly? issueDate = null,
        IndexType indexType = IndexType.PreFixed)
    {
        var issue = issueDate ?? new DateOnly(2020, 1, 1);
        var maturity = issueDate is null ? new DateOnly(2030, 1, 1) : issue.AddYears(2);

        return new FixedIncomeAsset(
            "Título Teste",
            "Emissor Teste",
            assetType,
            indexType,
            rate,
            issue,
            maturity);
    }
}
