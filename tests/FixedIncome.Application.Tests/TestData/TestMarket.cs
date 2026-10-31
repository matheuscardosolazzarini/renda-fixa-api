using FixedIncome.Domain.Entities;

namespace FixedIncome.Application.Tests.TestData;

// Calendário mínimo para os casos de uso de projeção: os feriados ANBIMA de 2025 (o ano dos
// títulos de teste, que tem 252 dias úteis) e o Natal de 2030 só para estender a cobertura do
// calendário de 2025-01-01 a 2030-12-31.
public static class TestMarket
{
    private static readonly string[] Holidays2025 =
    [
        "2025-01-01", "2025-03-03", "2025-03-04", "2025-04-18", "2025-04-21", "2025-05-01",
        "2025-06-19", "2025-09-07", "2025-10-12", "2025-11-02", "2025-11-15", "2025-11-20",
        "2025-12-25"
    ];

    public static IReadOnlyList<Holiday> Holidays()
    {
        return Holidays2025
            .Append("2030-12-25")
            .Select(date => new Holiday(DateOnly.Parse(date), "Feriado nacional"))
            .ToList();
    }
}
