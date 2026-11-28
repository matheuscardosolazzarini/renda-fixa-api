using System.Globalization;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Domain.ValueObjects;
using static FixedIncome.Domain.Tests.TestData.TestMarket;

namespace FixedIncome.Domain.Tests.Entities;

// RN-16 e RN-17: próxima faixa de IR e fim do IOF. Valores esperados dos casos 1b, 2, 4, 5, 6, 10 e
// do teste 11 da seção "Casos de referência" de docs/SPEC-F13-EXECUCAO.md. As datas são sempre
// aporte + n dias corridos, sem ajuste para dia útil.
public class PositionNextBracketTests
{
    private static readonly DateOnly LongApplication = new(2027, 9, 1);
    private static readonly DateOnly LongMaturity = new(2030, 7, 1);

    // Caso 4: referências dos dias 0, 180, 181, 360, 361 e 720 (o 721 não tem próxima faixa).
    [Theory]
    [InlineData(0, "2027-09-01", 22.5, 20.0, "2028-02-29")]
    [InlineData(180, "2028-02-28", 22.5, 20.0, "2028-02-29")]
    [InlineData(181, "2028-02-29", 20.0, 17.5, "2028-08-27")]
    [InlineData(360, "2028-08-26", 20.0, 17.5, "2028-08-27")]
    [InlineData(361, "2028-08-27", 17.5, 15.0, "2029-08-22")]
    [InlineData(720, "2029-08-21", 17.5, 15.0, "2029-08-22")]
    public void Proxima_faixa_de_ir_nas_fronteiras_da_tabela_regressiva(
        int elapsedDays, string reference, decimal taxRate, decimal nextTaxRate, string nextTaxRateDate)
    {
        // Arrange: a data de 29/02/2028 só sai certa com AddDays (AddMonths daria 01/03/2028).
        var asset = CriarAsset(AssetType.Cdb, LongApplication, LongMaturity);
        var position = new Position(asset, 1000m, LongApplication);

        // Act
        var projection = position.Project(asset, DateOnly.Parse(reference), Create());

        // Assert
        Assert.Equal(elapsedDays, projection.ElapsedDays);
        Assert.Equal(taxRate, projection.TaxRate);
        Assert.Equal(nextTaxRate, projection.NextTaxRate);
        Assert.Equal(DateOnly.Parse(nextTaxRateDate), projection.NextTaxRateDate);
    }

    [Fact]
    public void Na_ultima_faixa_de_ir_nao_ha_proxima_faixa()
    {
        // Arrange: caso 4, d = 721 (2029-08-22), alíquota 15,0.
        var asset = CriarAsset(AssetType.Cdb, LongApplication, LongMaturity);
        var position = new Position(asset, 1000m, LongApplication);

        // Act
        var projection = position.Project(asset, new DateOnly(2029, 8, 22), Create());

        // Assert
        Assert.Equal(721, projection.ElapsedDays);
        Assert.Equal(15.0m, projection.TaxRate);
        Assert.Null(projection.NextTaxRate);
        Assert.Null(projection.NextTaxRateDate);
    }

    // Caso 5: LCI e LCA PreFixed, aporte 01/04/2025, vencimento 01/04/2027. O IR é zero (RN-04) em
    // qualquer prazo e não há próxima faixa; o IOF da LCI segue a tabela e o da LCA é sempre zero.
    [Theory]
    [InlineData(AssetType.Lci, "2025-04-01", 0, "2025-05-01")]
    [InlineData(AssetType.Lci, "2025-04-22", 30, "2025-05-01")]
    [InlineData(AssetType.Lci, "2025-09-28", 0, null)]
    [InlineData(AssetType.Lci, "2025-09-29", 0, null)]
    [InlineData(AssetType.Lci, "2027-03-23", 0, null)]
    [InlineData(AssetType.Lca, "2025-04-01", 0, null)]
    [InlineData(AssetType.Lca, "2025-04-22", 0, null)]
    [InlineData(AssetType.Lca, "2025-09-28", 0, null)]
    [InlineData(AssetType.Lca, "2025-09-29", 0, null)]
    [InlineData(AssetType.Lca, "2027-03-23", 0, null)]
    public void Lci_e_lca_nunca_tem_proxima_faixa_de_ir_e_so_a_lci_recebe_a_data_do_fim_do_iof(
        AssetType assetType, string reference, decimal iofRate, string? iofFreeDate)
    {
        // Arrange
        var application = new DateOnly(2025, 4, 1);
        var asset = CriarAsset(assetType, new DateOnly(2025, 1, 1), new DateOnly(2027, 4, 1));
        var position = new Position(asset, 1000m, application);

        // Act
        var projection = position.Project(asset, DateOnly.Parse(reference), Create());

        // Assert
        Assert.Equal(0m, projection.TaxRate);
        Assert.Null(projection.NextTaxRate);
        Assert.Null(projection.NextTaxRateDate);
        Assert.Equal(iofRate, projection.IofRate);
        Assert.Equal(iofFreeDate is null ? null : DateOnly.Parse(iofFreeDate), projection.IofFreeDate);
    }

    // Caso 6a: aporte 02/04/2025. Vencimento = aporte + 181 devolve a data; = aporte + 180 não.
    [Theory]
    [InlineData("2025-09-30", "2025-04-02", 0, 22.5, 20.0, "2025-09-30", 0, "2025-05-02")]
    [InlineData("2025-09-30", "2025-09-29", 180, 22.5, 20.0, "2025-09-30", 0, null)]
    [InlineData("2025-09-30", "2025-09-30", 181, 20.0, null, null, 0, null)]
    [InlineData("2025-09-30", "2025-10-15", 181, 20.0, null, null, 0, null)]
    [InlineData("2025-09-29", "2025-04-02", 0, 22.5, null, null, 0, "2025-05-02")]
    [InlineData("2025-09-29", "2025-09-26", 177, 22.5, null, null, 0, null)]
    [InlineData("2025-09-29", "2025-10-15", 180, 22.5, null, null, 0, null)]
    public void Proxima_faixa_de_ir_e_devolvida_ate_o_vencimento_e_nula_depois_dele(
        string maturity, string reference, int elapsedDays, decimal taxRate, double? nextTaxRate,
        string? nextTaxRateDate, decimal iofRate, string? iofFreeDate)
    {
        // Arrange
        var application = new DateOnly(2025, 4, 2);
        var asset = CriarAsset(AssetType.Cdb, application, DateOnly.Parse(maturity));
        var position = new Position(asset, 1000m, application);

        // Act
        var projection = position.Project(asset, DateOnly.Parse(reference), Create());

        // Assert
        Assert.Equal(elapsedDays, projection.ElapsedDays);
        Assert.Equal(taxRate, projection.TaxRate);
        Assert.Equal(nextTaxRate is null ? null : (decimal)nextTaxRate, projection.NextTaxRate);
        Assert.Equal(nextTaxRateDate is null ? null : DateOnly.Parse(nextTaxRateDate), projection.NextTaxRateDate);
        Assert.Equal(iofRate, projection.IofRate);
        Assert.Equal(iofFreeDate is null ? null : DateOnly.Parse(iofFreeDate), projection.IofFreeDate);
    }

    // Caso 6c e 6d: aporte 02/06/2025. Vencimento = aporte + 30 devolve a data do IOF; = aporte + 29 não.
    [Theory]
    [InlineData("2025-07-02", "2025-07-01", 29, 3, "2025-07-02")]
    [InlineData("2025-07-02", "2025-07-02", 30, 0, null)]
    [InlineData("2025-07-02", "2025-07-15", 30, 0, null)]
    [InlineData("2025-07-01", "2025-06-23", 21, 30, null)]
    [InlineData("2025-07-01", "2025-07-15", 29, 3, null)]
    public void Data_do_fim_do_iof_e_devolvida_ate_o_vencimento_e_nula_depois_dele(
        string maturity, string reference, int elapsedDays, decimal iofRate, string? iofFreeDate)
    {
        // Arrange: nenhum desses vencimentos alcança a faixa de 181 dias, então não há próxima faixa de IR.
        var application = new DateOnly(2025, 6, 2);
        var asset = CriarAsset(AssetType.Cdb, application, DateOnly.Parse(maturity));
        var position = new Position(asset, 1000m, application);

        // Act
        var projection = position.Project(asset, DateOnly.Parse(reference), Create());

        // Assert
        Assert.Equal(elapsedDays, projection.ElapsedDays);
        Assert.Equal(22.5m, projection.TaxRate);
        Assert.Null(projection.NextTaxRate);
        Assert.Null(projection.NextTaxRateDate);
        Assert.Equal(iofRate, projection.IofRate);
        Assert.Equal(iofFreeDate is null ? null : DateOnly.Parse(iofFreeDate), projection.IofFreeDate);
    }

    [Fact]
    public void Tesouro_direto_recebe_a_proxima_faixa_e_a_data_do_fim_do_iof_como_o_cdb()
    {
        // Arrange: mesmas datas do caso 4 (aporte 01/09/2027, d = 0).
        var asset = CriarAsset(AssetType.TreasuryBond, LongApplication, LongMaturity);
        var position = new Position(asset, 1000m, LongApplication);

        // Act
        var projection = position.Project(asset, LongApplication, Create());

        // Assert
        Assert.Equal(22.5m, projection.TaxRate);
        Assert.Equal(20.0m, projection.NextTaxRate);
        Assert.Equal(new DateOnly(2028, 2, 29), projection.NextTaxRateDate);
        Assert.Equal(new DateOnly(2027, 10, 1), projection.IofFreeDate);
    }

    // D-4: a data devolvida é exatamente a primeira em que a tabela muda, um dia antes ela ainda
    // não mudou. Prazos dos casos 4 e 10.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(29)]
    [InlineData(30)]
    [InlineData(180)]
    [InlineData(181)]
    [InlineData(360)]
    [InlineData(361)]
    [InlineData(720)]
    [InlineData(721)]
    public void Datas_devolvidas_sao_o_primeiro_dia_da_faixa_seguinte_e_o_primeiro_dia_sem_iof(int elapsedDays)
    {
        // Arrange
        var asset = CriarAsset(AssetType.Cdb, LongApplication, LongMaturity);
        var position = new Position(asset, 1000m, LongApplication);

        // Act
        var projection = position.Project(asset, LongApplication.AddDays(elapsedDays), Create());

        // Assert: com vencimento longo, as duas datas existem enquanto a faixa ou o IOF ainda não passou.
        if (elapsedDays < 721)
        {
            Assert.NotNull(projection.NextTaxRateDate);
        }

        if (elapsedDays < 30)
        {
            Assert.NotNull(projection.IofFreeDate);
        }

        if (projection.NextTaxRateDate is { } nextDate)
        {
            var fromDay = nextDate.DayNumber - LongApplication.DayNumber;
            Assert.Equal(projection.NextTaxRate, IncomeTaxTable.RateFor(AssetType.Cdb, fromDay));
            Assert.Equal(projection.TaxRate, IncomeTaxTable.RateFor(AssetType.Cdb, fromDay - 1));
            Assert.True(fromDay > elapsedDays);
        }

        if (projection.IofFreeDate is { } freeDate)
        {
            var freeDay = freeDate.DayNumber - LongApplication.DayNumber;
            Assert.Equal(0m, IofTable.RateFor(freeDay));
            Assert.Equal(3m, IofTable.RateFor(freeDay - 1));
            Assert.True(freeDay > elapsedDays);
        }
    }

    // Caso 10: CDB, LCI e LCA com o mesmo prazo. A LCA é a única com IOF zero.
    [Theory]
    [InlineData(AssetType.Cdb, 0, 0, "2027-10-01")]
    [InlineData(AssetType.Cdb, 1, 96, "2027-10-01")]
    [InlineData(AssetType.Cdb, 29, 3, "2027-10-01")]
    [InlineData(AssetType.Cdb, 30, 0, null)]
    [InlineData(AssetType.Lci, 0, 0, "2027-10-01")]
    [InlineData(AssetType.Lci, 1, 96, "2027-10-01")]
    [InlineData(AssetType.Lci, 29, 3, "2027-10-01")]
    [InlineData(AssetType.Lci, 30, 0, null)]
    [InlineData(AssetType.Lca, 0, 0, null)]
    [InlineData(AssetType.Lca, 1, 0, null)]
    [InlineData(AssetType.Lca, 29, 0, null)]
    [InlineData(AssetType.Lca, 30, 0, null)]
    public void Data_do_fim_do_iof_nas_fronteiras_dos_dias_0_1_29_e_30(
        AssetType assetType, int elapsedDays, decimal iofRate, string? iofFreeDate)
    {
        // Arrange
        var asset = CriarAsset(assetType, LongApplication, LongMaturity);
        var position = new Position(asset, 1000m, LongApplication);

        // Act
        var projection = position.Project(asset, LongApplication.AddDays(elapsedDays), Create());

        // Assert
        Assert.Equal(elapsedDays, projection.ElapsedDays);
        Assert.Equal(iofRate, projection.IofRate);
        Assert.Equal(iofFreeDate is null ? null : DateOnly.Parse(iofFreeDate), projection.IofFreeDate);
    }

    [Theory]
    [InlineData(AssetType.Cdb)]
    [InlineData(AssetType.Lci)]
    [InlineData(AssetType.Lca)]
    [InlineData(AssetType.TreasuryBond)]
    public void Data_do_fim_do_iof_e_nula_com_menos_de_30_dias_se_e_so_se_o_titulo_nao_paga_iof(AssetType assetType)
    {
        // Arrange: no dia 1 a tabela cobra 96%, então IofRate zero só pode vir da isenção do título.
        var asset = CriarAsset(assetType, LongApplication, LongMaturity);
        var position = new Position(asset, 1000m, LongApplication);

        // Act
        var projection = position.Project(asset, LongApplication.AddDays(1), Create());

        // Assert
        Assert.Equal(projection.IofRate == 0m, projection.IofFreeDate is null);
    }

    [Theory]
    [InlineData(100, 1_000.94)]
    [InlineData(110, 1_001.03)]
    public void Datas_do_cdi_projetado_sao_as_do_aporte_e_is_projected_continua_verdadeiro(
        int percentOfCdi, decimal netAmount)
    {
        // Arrange: casos 1 e 2 da F12, aporte 16/11/2026, referência 26/11/2026 (d = 10).
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
        Assert.Equal(netAmount, projection.NetAmount);
        Assert.Equal(22.5m, projection.TaxRate);
        Assert.Equal(20.0m, projection.NextTaxRate);
        Assert.Equal(new DateOnly(2027, 5, 16), projection.NextTaxRateDate);
        Assert.Equal(new DateOnly(2026, 12, 16), projection.IofFreeDate);
        Assert.True(projection.IsProjected);
        Assert.False(projection.IsEstimated);
    }

    [Fact]
    public void Datas_do_ipca_estimado_e_projetado_sao_as_do_aporte_e_as_marcas_continuam_as_da_f12()
    {
        // Arrange: caso 6 da F12. Hoje 05/11/2026, aporte 01/10/2026, referência 15/12/2026 (d = 75):
        // com 30 dias ou mais não há data de fim do IOF.
        const string realWindow = "0.09 0.18 0.33 0.33 0.70 0.88 0.67 0.58 0.16 0.07 -0.32 0.82";
        var market = Create(new DateOnly(2026, 11, 5), IpcaSeries("2025-10-01", realWindow));
        var asset = new FixedIncomeAsset(
            "CDB IPCA+", "Banco X", AssetType.Cdb, IndexType.Ipca, 6m,
            new DateOnly(2026, 1, 1), new DateOnly(2028, 1, 1));
        var position = new Position(asset, 1000m, new DateOnly(2026, 10, 1));

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 12, 15), market);

        // Assert
        Assert.Equal(75, projection.ElapsedDays);
        Assert.Equal(22.5m, projection.TaxRate);
        Assert.Equal(20.0m, projection.NextTaxRate);
        Assert.Equal(new DateOnly(2027, 3, 31), projection.NextTaxRateDate);
        Assert.Null(projection.IofFreeDate);
        Assert.True(projection.IsEstimated);
        Assert.True(projection.IsProjected);
    }

    [Theory]
    [InlineData("2026-08-31", 28)]
    [InlineData("2026-08-03", 0)]
    public void Datas_do_ipca_com_rendimento_negativo_ou_nulo_saem_mesmo_sem_imposto(string reference, int elapsedDays)
    {
        // Arrange: caso 2. IPCA+ 2% com deflação de 08/2026 (-0,32), aporte 03/08/2026, vencimento 01/01/2028.
        var market = Create(Ipca("2026-08-01", -0.32m));
        var asset = new FixedIncomeAsset(
            "CDB IPCA+", "Emissor Teste", AssetType.Cdb, IndexType.Ipca, 2m,
            new DateOnly(2026, 1, 1), new DateOnly(2028, 1, 1));
        var position = new Position(asset, 1000m, new DateOnly(2026, 8, 3));

        // Act
        var projection = position.Project(asset, DateOnly.Parse(reference), market);

        // Assert
        Assert.Equal(elapsedDays, projection.ElapsedDays);
        Assert.Equal(new DateOnly(2027, 1, 31), projection.NextTaxRateDate);
        Assert.Equal(20.0m, projection.NextTaxRate);
        Assert.Equal(new DateOnly(2026, 9, 2), projection.IofFreeDate);
        Assert.False(projection.IsEstimated);
        Assert.False(projection.IsProjected);
    }

    private static FixedIncomeAsset CriarAsset(AssetType assetType, DateOnly issueDate, DateOnly maturityDate) =>
        new("Título", "Emissor Teste", assetType, IndexType.PreFixed, 10m, issueDate, maturityDate);

    private static IndexRate[] IpcaSeries(string firstMonth, string values)
    {
        var start = DateOnly.Parse(firstMonth, CultureInfo.InvariantCulture);

        return values
            .Split(' ')
            .Select((value, index) => Ipca(
                start.AddMonths(index).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                decimal.Parse(value, CultureInfo.InvariantCulture)))
            .ToArray();
    }
}
