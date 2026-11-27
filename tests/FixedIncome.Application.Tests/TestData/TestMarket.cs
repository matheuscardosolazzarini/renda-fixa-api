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

    // Feriados de 2026 que caem nos intervalos dos casos de referência da F12 (12/10, 02/11, 20/11 e
    // 25/12), para os testes de projeção com data futura.
    private static readonly string[] Holidays2026 = ["2026-10-12", "2026-11-02", "2026-11-20", "2026-12-25"];

    // Os mesmos de Holidays() mais os de 2026.
    public static IReadOnlyList<Holiday> HolidaysWith2026()
    {
        return Holidays2025
            .Concat(Holidays2026)
            .Append("2030-12-25")
            .Select(date => new Holiday(DateOnly.Parse(date), "Feriado nacional"))
            .ToList();
    }

    public static IReadOnlyList<Holiday> Holidays()
    {
        return Holidays2025
            .Append("2030-12-25")
            .Select(date => new Holiday(DateOnly.Parse(date), "Feriado nacional"))
            .ToList();
    }
}
