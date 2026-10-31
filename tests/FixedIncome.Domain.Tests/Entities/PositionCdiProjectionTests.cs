using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Domain.ValueObjects;
using static FixedIncome.Domain.Tests.TestData.TestMarket;

namespace FixedIncome.Domain.Tests.Entities;

// Projeção de títulos indexados ao CDI (RN-09). Valores esperados dos casos 3 e 4 da seção
// "Casos de referência" de docs/SPEC-F7-EXECUCAO.md, na convenção C6b.
public class PositionCdiProjectionTests
{
    // Caso 3: SGS 12 de 14/09 a 22/09/2026. A cotação de 23/09 fica no mercado de propósito: a
    // data final é excluída (RN-08), então ela não pode entrar no produtório.
    private static readonly IndexRate[] ShortSeries =
    [
        Cdi("2026-09-14", 0.051660m),
        Cdi("2026-09-15", 0.051660m),
        Cdi("2026-09-16", 0.051660m),
        Cdi("2026-09-17", 0.050788m),
        Cdi("2026-09-18", 0.050788m),
        Cdi("2026-09-21", 0.050788m),
        Cdi("2026-09-22", 0.050788m),
        Cdi("2026-09-23", 0.050788m)
    ];

    [Theory]
    [InlineData(AssetType.Cdb, 100, "1003.59", "3.59", "2.51", "0.24", "1000.84")] // 3.1
    [InlineData(AssetType.Cdb, 110, "1003.95", "3.95", "2.77", "0.27", "1000.91")] // 3.2
    [InlineData(AssetType.Lci, 100, "1003.59", "3.59", "2.51", "0.00", "1001.08")] // 3.3
    [InlineData(AssetType.Lci, 110, "1003.95", "3.95", "2.77", "0.00", "1001.18")] // 3.4
    public void Cdi_em_serie_curta_aplica_o_percentual_do_cdi_e_depois_iof_e_ir(
        AssetType assetType, int percentOfCdi, string gross, string yield, string iof, string tax, string net)
    {
        // Arrange: 9 dias corridos (IOF 70%, IR 22,5% no CDB) e 7 dias úteis. No CDB a base do
        // IR é o rendimento menos o IOF. Em 110%, 3,95 x 70% = 2,765 é ponto médio exato e vai
        // para 2,77 (AwayFromZero).
        var market = Create(ShortSeries);
        var asset = CriarAsset(assetType, percentOfCdi);
        var position = new Position(asset, 1000m, new DateOnly(2026, 9, 14));

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 9, 23), market);

        // Assert
        Assert.Equal(Parse(gross), projection.GrossAmount);
        Assert.Equal(Parse(yield), projection.GrossYield);
        Assert.Equal(70m, projection.IofRate);
        Assert.Equal(Parse(iof), projection.IofAmount);
        Assert.Equal(assetType == AssetType.Cdb ? 22.5m : 0m, projection.TaxRate);
        Assert.Equal(Parse(tax), projection.TaxAmount);
        Assert.Equal(Parse(net), projection.NetAmount);
        Assert.Equal(9, projection.ElapsedDays);
        Assert.Equal(7, projection.BusinessDays);
        Assert.False(projection.IsEstimated);
    }


    [Fact]
    public void Cdi_arredonda_o_fator_final_em_oito_casas_antes_de_multiplicar_o_principal()
    {
        // Arrange: caso 3.1 com principal de R$ 1 bilhão para tornar visível a diferença entre o
        // fator de 8 casas (1,00358682) e o acumulado (1,0035868214182642): 1.003.586.820,00
        // contra 1.003.586.821,42.
        var market = Create(ShortSeries);
        var asset = CriarAsset(AssetType.Cdb, 100);
        var position = new Position(asset, 1_000_000_000m, new DateOnly(2026, 9, 14));

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 9, 23), market);

        // Assert
        Assert.Equal(1_003_586_820.00m, projection.GrossAmount);
    }

    [Fact]
    public void Cdi_sem_cotacao_em_dia_util_do_intervalo_lanca_excecao_com_a_primeira_data_faltante()
    {
        // Arrange: a mesma posição do caso 3, sem a cotação de 17/09/2026 (RN-09: sem fallback).
        var series = ShortSeries.Where(rate => rate.ReferenceDate != new DateOnly(2026, 9, 17)).ToArray();
        var market = Create(series);
        var asset = CriarAsset(AssetType.Cdb, 100);
        var position = new Position(asset, 1000m, new DateOnly(2026, 9, 14));

        // Act
        var act = () => position.Project(asset, new DateOnly(2026, 9, 23), market);

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Contains("2026-09-17", exception.Message);
    }

    [Fact]
    public void Cdi_sem_nenhuma_cotacao_cita_a_primeira_data_do_intervalo()
    {
        // Arrange: segunda 14/09/2026 é dia útil e é a primeira data sem cotação.
        var market = Create();
        var asset = CriarAsset(AssetType.Cdb, 100);
        var position = new Position(asset, 1000m, new DateOnly(2026, 9, 14));

        // Act
        var act = () => position.Project(asset, new DateOnly(2026, 9, 23), market);

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Contains("2026-09-14", exception.Message);
    }

    [Fact]
    public void Cdi_sem_cotacao_em_fim_de_semana_ou_feriado_nao_e_exigida()
    {
        // Arrange: 14/09 a 22/09/2026 tem sábado e domingo (19 e 20/09) sem cotação, e o caso 3
        // já prova que a projeção sai sem eles. Aqui o feriado de 07/09/2026 (segunda) também
        // fica sem cotação: aplicação em 04/09 (sexta) até 08/09 conta só o dia 04/09.
        var market = Create(Cdi("2026-09-04", 0.051660m));
        var asset = CriarAsset(AssetType.Cdb, 100);
        var position = new Position(asset, 1000m, new DateOnly(2026, 9, 4));

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 9, 8), market);

        // Assert
        Assert.Equal(1, projection.BusinessDays);
    }

    [Theory]
    [InlineData(100, "1010.82", "10.82", "2.43", "1008.39")]
    [InlineData(110, "1011.90", "11.90", "2.68", "1009.22")]
    public void Cdi_de_um_mes_inteiro_acumula_o_produtorio_com_fator_final_em_oito_casas(
        int percentOfCdi, string gross, string yield, string tax, string net)
    {
        // Arrange: caso 4. Fator de 8 casas esperado: 1,01081673 (100%) e 1,01190451 (110%).
        // Com 30 dias corridos o IOF é zero; DU = 21 (o feriado de 07/09 fica de fora).
        var market = Create(MonthSeries());
        var asset = CriarAsset(AssetType.Cdb, percentOfCdi);
        var position = new Position(asset, 1000m, new DateOnly(2026, 9, 1));

        // Act
        var projection = position.Project(asset, new DateOnly(2026, 10, 1), market);

        // Assert
        Assert.Equal(Parse(gross), projection.GrossAmount);
        Assert.Equal(Parse(yield), projection.GrossYield);
        Assert.Equal(0m, projection.IofRate);
        Assert.Equal(0m, projection.IofAmount);
        Assert.Equal(Parse(tax), projection.TaxAmount);
        Assert.Equal(Parse(net), projection.NetAmount);
        Assert.Equal(30, projection.ElapsedDays);
        Assert.Equal(21, projection.BusinessDays);
    }

    // Caso 4: 0,051660 em 11 dias úteis (01 a 16/09) e 0,050788 em 10 (17 a 30/09).
    private static IndexRate[] MonthSeries()
    {
        var firstPeriod = new[] { 1, 2, 3, 4, 8, 9, 10, 11, 14, 15, 16 };
        var secondPeriod = new[] { 17, 18, 21, 22, 23, 24, 25, 28, 29, 30 };

        return firstPeriod.Select(day => Cdi($"2026-09-{day:00}", 0.051660m))
            .Concat(secondPeriod.Select(day => Cdi($"2026-09-{day:00}", 0.050788m)))
            .ToArray();
    }

    private static FixedIncomeAsset CriarAsset(AssetType assetType, int percentOfCdi)
    {
        return new FixedIncomeAsset(
            "Título CDI", "Emissor Teste", assetType, IndexType.Cdi, percentOfCdi,
            new DateOnly(2026, 9, 1), new DateOnly(2027, 9, 1));
    }

    private static decimal Parse(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}
