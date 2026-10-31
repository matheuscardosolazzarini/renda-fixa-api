using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Domain.Tests.TestData;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Domain.Tests.ValueObjects;

public class MarketDataTests
{
    private static readonly BusinessDayCalendar Calendar = new(AnbimaHolidays.All());

    private static IndexRate Cdi(string date, decimal value) =>
        new(IndexType.Cdi, DateOnly.Parse(date), value);

    private static IndexRate Ipca(string month, decimal value) =>
        new(IndexType.Ipca, DateOnly.Parse(month), value);

    [Fact]
    public void Expoe_o_calendario_informado()
    {
        // Arrange
        var market = new MarketData(Calendar, []);

        // Act
        var calendar = market.Calendar;

        // Assert
        Assert.Same(Calendar, calendar);
    }

    [Fact]
    public void Cdi_de_data_existente_devolve_o_percentual_ao_dia()
    {
        // Arrange
        var market = new MarketData(Calendar, [Cdi("2026-09-14", 0.055131m), Cdi("2026-09-15", 0.055132m)]);

        // Act
        var value = market.GetCdi(new DateOnly(2026, 9, 15));

        // Assert
        Assert.Equal(0.055132m, value);
    }

    [Fact]
    public void Cdi_de_data_ausente_devolve_nulo()
    {
        // Arrange
        var market = new MarketData(Calendar, [Cdi("2026-09-14", 0.055131m)]);

        // Act
        var value = market.GetCdi(new DateOnly(2026, 9, 15));

        // Assert
        Assert.Null(value);
    }

    [Fact]
    public void Cdi_zero_publicado_nao_e_confundido_com_ausente()
    {
        // Arrange
        var market = new MarketData(Calendar, [Cdi("2026-09-14", 0m)]);

        // Act
        var value = market.GetCdi(new DateOnly(2026, 9, 14));

        // Assert
        Assert.Equal(0m, value);
    }

    [Fact]
    public void Ipca_do_mes_existente_devolve_o_percentual_no_mes()
    {
        // Arrange
        var market = new MarketData(Calendar, [Ipca("2026-07-01", 0.26m), Ipca("2026-08-01", -0.20m)]);

        // Act
        var value = market.GetIpca(new DateOnly(2026, 8, 1));

        // Assert
        Assert.Equal(-0.20m, value);
    }

    [Fact]
    public void Ipca_e_consultado_por_qualquer_dia_do_mes()
    {
        // Arrange
        var market = new MarketData(Calendar, [Ipca("2026-07-01", 0.26m)]);

        // Act
        var value = market.GetIpca(new DateOnly(2026, 7, 23));

        // Assert
        Assert.Equal(0.26m, value);
    }

    [Fact]
    public void Ipca_de_mes_ausente_devolve_nulo()
    {
        // Arrange
        var market = new MarketData(Calendar, [Ipca("2026-07-01", 0.26m)]);

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
        ]);

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
        var market = new MarketData(Calendar, [Cdi("2026-09-14", 0.055131m)]);

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
        var market = new MarketData(Calendar, [Cdi("2026-07-01", 0.055m), Ipca("2026-07-01", 0.26m)]);

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
        Action act = () => new MarketData(Calendar, rates);

        // Assert
        Assert.Throws<DomainException>(act);
    }
}
