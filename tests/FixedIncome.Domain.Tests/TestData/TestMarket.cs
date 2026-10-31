using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Domain.Tests.TestData;

// Monta o MarketData dos testes de projeção com o calendário ANBIMA de 2020 a 2030.
public static class TestMarket
{
    public static MarketData Create(params IndexRate[] rates) => new(new BusinessDayCalendar(AnbimaHolidays.All()), rates);

    public static MarketData CreateWithoutHoliday(DateOnly holiday, params IndexRate[] rates)
    {
        var holidays = AnbimaHolidays.All().Where(h => h.Date != holiday);

        return new MarketData(new BusinessDayCalendar(holidays), rates);
    }

    public static IndexRate Cdi(string date, decimal value) => new(IndexType.Cdi, DateOnly.Parse(date), value);

    public static IndexRate Ipca(string month, decimal value) => new(IndexType.Ipca, DateOnly.Parse(month), value);
}
