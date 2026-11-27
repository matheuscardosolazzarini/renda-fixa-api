using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Domain.Tests.TestData;

// Monta o MarketData dos testes de projeção com o calendário ANBIMA de 2020 a 2030.
public static class TestMarket
{
    // Hoje depois da cobertura do calendário: nenhum mês de IPCA vira projetado e nenhum dia de
    // CDI sem cotação posterior à última escapa da falha de importação (RN-12), então os testes
    // anteriores à F12 seguem com o comportamento que sempre tiveram.
    public static readonly DateOnly DefaultToday = new(2031, 1, 1);

    public static MarketData Create(params IndexRate[] rates) => Create(DefaultToday, rates);

    public static MarketData Create(DateOnly today, params IndexRate[] rates) =>
        new(new BusinessDayCalendar(AnbimaHolidays.All()), rates, today);

    public static MarketData CreateWithoutHoliday(DateOnly holiday, params IndexRate[] rates)
    {
        var holidays = AnbimaHolidays.All().Where(h => h.Date != holiday);

        return new MarketData(new BusinessDayCalendar(holidays), rates, DefaultToday);
    }

    public static IndexRate Cdi(string date, decimal value) => new(IndexType.Cdi, DateOnly.Parse(date), value);

    public static IndexRate Ipca(string month, decimal value) => new(IndexType.Ipca, DateOnly.Parse(month), value);
}
