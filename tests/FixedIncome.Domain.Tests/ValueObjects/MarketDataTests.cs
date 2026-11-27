using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Domain.Tests.TestData;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Domain.Tests.ValueObjects;

public class MarketDataTests
{
    private static readonly BusinessDayCalendar Calendar = new(AnbimaHolidays.All());
    private static readonly DateOnly Today = new(2026, 11, 5);

    private static IndexRate Cdi(string date, decimal value) =>
        new(IndexType.Cdi, DateOnly.Parse(date), value);

    private static IndexRate Ipca(string month, decimal value) =>
        new(IndexType.Ipca, DateOnly.Parse(month), value);

    [Fact]
    public void Expoe_o_calendario_informado()
    {
        // Arrange
        var market = new MarketData(Calendar, [], Today);

        // Act
        var calendar = market.Calendar;

        // Assert
        Assert.Same(Calendar, calendar);
    }

    [Fact]
    public void Cdi_de_data_existente_devolve_o_percentual_ao_dia()
    {
        // Arrange
        var market = new MarketData(Calendar, [Cdi("2026-09-14", 0.055131m), Cdi("2026-09-15", 0.055132m)], Today);

        // Act
        var value = market.GetCdi(new DateOnly(2026, 9, 15));

        // Assert
        Assert.Equal(0.055132m, value);
    }

    [Fact]
    public void Cdi_de_data_ausente_devolve_nulo()
    {
        // Arrange
        var market = new MarketData(Calendar, [Cdi("2026-09-14", 0.055131m)], Today);

        // Act
        var value = market.GetCdi(new DateOnly(2026, 9, 15));

        // Assert
        Assert.Null(value);
    }

    [Fact]
    public void Cdi_zero_publicado_nao_e_confundido_com_ausente()
    {
        // Arrange
        var market = new MarketData(Calendar, [Cdi("2026-09-14", 0m)], Today);

        // Act
        var value = market.GetCdi(new DateOnly(2026, 9, 14));

        // Assert
        Assert.Equal(0m, value);
    }

    [Fact]
    public void Ipca_do_mes_existente_devolve_o_percentual_no_mes()
    {
        // Arrange
        var market = new MarketData(Calendar, [Ipca("2026-07-01", 0.26m), Ipca("2026-08-01", -0.20m)], Today);

        // Act
        var value = market.GetIpca(new DateOnly(2026, 8, 1));

        // Assert
        Assert.Equal(-0.20m, value);
    }

    [Fact]
    public void Ipca_e_consultado_por_qualquer_dia_do_mes()
    {
        // Arrange
        var market = new MarketData(Calendar, [Ipca("2026-07-01", 0.26m)], Today);

        // Act
        var value = market.GetIpca(new DateOnly(2026, 7, 23));

        // Assert
        Assert.Equal(0.26m, value);
    }

    [Fact]
    public void Ipca_de_mes_ausente_devolve_nulo()
    {
        // Arrange
        var market = new MarketData(Calendar, [Ipca("2026-07-01", 0.26m)], Today);

        // Act
        var value = market.GetIpca(new DateOnly(2026, 9, 1));

        // Assert
        Assert.Null(value);
    }

    [Fact]
    public void Ultimo_ipca_publicado_e_o_do_mes_mais_recente()
    {
        // Arrange
        var market = new MarketData(Calendar,
        [
            Ipca("2026-08-01", -0.20m),
            Ipca("2026-06-01", 0.30m),
            Ipca("2026-07-01", 0.26m)
        ], Today);

        // Act
        var last = market.LastPublishedIpca;

        // Assert
        Assert.NotNull(last);
        Assert.Equal(new DateOnly(2026, 8, 1), last.Value.Month);
        Assert.Equal(-0.20m, last.Value.Value);
    }

    [Fact]
    public void Ultimo_ipca_publicado_sem_serie_de_ipca_e_nulo()
    {
        // Arrange
        var market = new MarketData(Calendar, [Cdi("2026-09-14", 0.055131m)], Today);

        // Act
        var last = market.LastPublishedIpca;

        // Assert
        Assert.Null(last);
    }

    [Fact]
    public void Series_de_cdi_e_ipca_nao_se_misturam()
    {
        // Arrange
        // 2026-07-01 é quarta-feira: a mesma data existe nas duas séries, com valores distintos.
        var market = new MarketData(Calendar, [Cdi("2026-07-01", 0.055m), Ipca("2026-07-01", 0.26m)], Today);

        // Act
        var cdi = market.GetCdi(new DateOnly(2026, 7, 1));
        var ipca = market.GetIpca(new DateOnly(2026, 7, 1));

        // Assert
        Assert.Equal(0.055m, cdi);
        Assert.Equal(0.26m, ipca);
    }

    [Fact]
    public void Cotacao_duplicada_para_o_mesmo_indexador_e_data_e_rejeitada()
    {
        // Arrange
        var rates = new[] { Cdi("2026-09-14", 0.055m), Cdi("2026-09-14", 0.056m) };

        // Act
        Action act = () => new MarketData(Calendar, rates, Today);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Calendario_nulo_e_rejeitado_com_argument_null_exception()
    {
        // Arrange
        BusinessDayCalendar? calendar = null;

        // Act
        Action act = () => new MarketData(calendar!, [], Today);

        // Assert
        var exception = Assert.Throws<ArgumentNullException>(act);
        Assert.Equal("calendar", exception.ParamName);
    }

    [Fact]
    public void Cotacoes_nulas_sao_rejeitadas_com_argument_null_exception()
    {
        // Arrange
        IEnumerable<IndexRate>? rates = null;

        // Act
        Action act = () => new MarketData(Calendar, rates!, Today);

        // Assert
        var exception = Assert.Throws<ArgumentNullException>(act);
        Assert.Equal("rates", exception.ParamName);
    }

    [Fact]
    public void Cotacao_duplicada_informa_o_indexador_e_a_data_repetidos()
    {
        // Arrange
        var rates = new[] { Cdi("2026-09-14", 0.055m), Cdi("2026-09-14", 0.056m) };

        // Act
        Action act = () => new MarketData(Calendar, rates, Today);

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Contains("Cotação duplicada de Cdi para 2026-09-14", exception.Message);
    }

    [Fact]
    public void Expoe_o_dia_de_hoje_informado()
    {
        // Arrange
        var market = new MarketData(Calendar, [], Today);

        // Act
        var today = market.Today;

        // Assert
        Assert.Equal(new DateOnly(2026, 11, 5), today);
    }

    [Fact]
    public void Ultima_cotacao_de_cdi_e_a_do_dia_mais_recente_da_serie()
    {
        // Arrange
        var market = new MarketData(Calendar,
        [
            Cdi("2026-09-16", 0.050788m),
            Cdi("2026-09-14", 0.051660m),
            Cdi("2026-09-15", 0.051660m)
        ], Today);

        // Act
        var last = market.LastCdi;

        // Assert
        Assert.NotNull(last);
        Assert.Equal(new DateOnly(2026, 9, 16), last.Value.Date);
        Assert.Equal(0.050788m, last.Value.Value);
    }

    [Fact]
    public void Ultima_cotacao_de_cdi_e_nula_sem_nenhuma_cotacao()
    {
        // Arrange
        var market = new MarketData(Calendar, [Ipca("2026-07-01", 0.26m)], Today);

        // Act
        var last = market.LastCdi;

        // Assert
        Assert.Null(last);
    }

    // Caso 7a: janela real da série 433, 10/2025 a 09/2026. Produto 1,0457611967507, média
    // geométrica 0,37357136%. Caso 7b: seis meses de -2,00 e seis de +1,80; produto 0,99764^6,
    // média -0,11806970% (a média aritmética daria -0,10). Caso 7c: seis de -0,60 e seis de
    // +0,40, média -0,10125126%.
    [Theory]
    [InlineData("0.09 0.18 0.33 0.33 0.70 0.88 0.67 0.58 0.16 0.07 -0.32 0.82", "0.37")]
    [InlineData("-2.00 -2.00 -2.00 -2.00 -2.00 -2.00 1.80 1.80 1.80 1.80 1.80 1.80", "-0.12")]
    [InlineData("-0.60 -0.60 -0.60 -0.60 -0.60 -0.60 0.40 0.40 0.40 0.40 0.40 0.40", "-0.10")]
    public void Variacao_projetada_do_ipca_e_a_media_geometrica_dos_12_meses_em_duas_casas(
        string window, string expected)
    {
        // Arrange: último publicado em 09/2026, janela de 10/2025 a 09/2026.
        var market = new MarketData(Calendar, IpcaSeries("2025-10-01", window), Today);

        // Act
        var found = market.TryGetProjectedIpca(out var variation, out var failure);

        // Assert
        Assert.True(found);
        Assert.Null(failure);
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), variation);
    }

    [Fact]
    public void Variacao_projetada_do_ipca_usa_so_os_12_meses_da_janela_quando_a_serie_tem_13()
    {
        // Arrange: caso 7d. 09/2025 (0,48) fica fora da janela de 10/2025 a 09/2026; se entrasse, o
        // resultado seria 0,41 (expoente 1/12) ou 0,38 (1/13) em vez de 0,37.
        var market = new MarketData(
            Calendar,
            IpcaSeries("2025-09-01", "0.48 0.09 0.18 0.33 0.33 0.70 0.88 0.67 0.58 0.16 0.07 -0.32 0.82"),
            Today);

        // Act
        var found = market.TryGetProjectedIpca(out var variation, out _);

        // Assert
        Assert.True(found);
        Assert.Equal(0.37m, variation);
    }

    [Fact]
    public void Variacao_projetada_do_ipca_com_11_meses_informa_a_contagem_e_o_primeiro_mes_ausente()
    {
        // Arrange: 11/2025 a 09/2026 são 11 meses; a janela é 10/2025 a 09/2026 e falta 10/2025.
        var market = new MarketData(
            Calendar,
            IpcaSeries("2025-11-01", "0.18 0.33 0.33 0.70 0.88 0.67 0.58 0.16 0.07 -0.32 0.82"),
            Today);

        // Act
        var found = market.TryGetProjectedIpca(out _, out var failure);

        // Assert
        Assert.False(found);
        Assert.Equal(
            "A projeção futura do IPCA exige os 12 meses de 2025-10 a 2026-09; há 11, falta 2025-10.",
            failure);
    }

    [Fact]
    public void Variacao_projetada_do_ipca_com_lacuna_dentro_da_janela_cita_o_primeiro_mes_ausente()
    {
        // Arrange: 12 cotações, mas 01/2026 não existe e 09/2025 (fora da janela) existe; dentro da
        // janela de 10/2025 a 09/2026 há 11, e o primeiro ausente é 2026-01.
        var rates = IpcaSeries("2025-09-01", "0.48 0.09 0.18 0.33 0.33 0.70 0.88 0.67 0.58 0.16 0.07 -0.32 0.82")
            .Where(rate => rate.ReferenceDate != new DateOnly(2026, 1, 1))
            .ToList();
        var market = new MarketData(Calendar, rates, Today);

        // Act
        var found = market.TryGetProjectedIpca(out _, out var failure);

        // Assert
        Assert.False(found);
        Assert.Equal(
            "A projeção futura do IPCA exige os 12 meses de 2025-10 a 2026-09; há 11, falta 2026-01.",
            failure);
    }

    [Fact]
    public void Variacao_projetada_do_ipca_sem_nenhuma_cotacao_publicada_falha_dizendo_que_nao_ha_cotacao()
    {
        // Arrange
        var market = new MarketData(Calendar, new List<IndexRate>(), Today);

        // Act
        var found = market.TryGetProjectedIpca(out var variation, out var failure);

        // Assert
        Assert.False(found);
        Assert.Equal(0m, variation);
        Assert.Equal("Não há cotação de IPCA publicada para estimar a projeção.", failure);
    }

    private static List<IndexRate> IpcaSeries(string firstMonth, string values)
    {
        var start = DateOnly.Parse(firstMonth, System.Globalization.CultureInfo.InvariantCulture);

        return values
            .Split(' ')
            .Select((value, index) => Ipca(
                start.AddMonths(index).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();
    }
}
