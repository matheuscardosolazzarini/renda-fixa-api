using System.Globalization;

namespace FixedIncome.Infrastructure.Seeds;

// Feriados nacionais ANBIMA de 2020 a 2030 (139 datas, inclusive os que caem em fim de
// semana), conforme a seção "Feriados ANBIMA" de docs/SPEC-F7-EXECUCAO.md.
internal static class AnbimaHolidaySeed
{
    private const string Csv = """
        2020-01-01,Confraternização Universal
        2020-02-24,Carnaval
        2020-02-25,Carnaval
        2020-04-10,Paixão de Cristo
        2020-04-21,Tiradentes
        2020-05-01,Dia do Trabalho
        2020-06-11,Corpus Christi
        2020-09-07,Independência do Brasil
        2020-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2020-11-02,Finados
        2020-11-15,Proclamação da República
        2020-12-25,Natal
        2021-01-01,Confraternização Universal
        2021-02-15,Carnaval
        2021-02-16,Carnaval
        2021-04-02,Paixão de Cristo
        2021-04-21,Tiradentes
        2021-05-01,Dia do Trabalho
        2021-06-03,Corpus Christi
        2021-09-07,Independência do Brasil
        2021-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2021-11-02,Finados
        2021-11-15,Proclamação da República
        2021-12-25,Natal
        2022-01-01,Confraternização Universal
        2022-02-28,Carnaval
        2022-03-01,Carnaval
        2022-04-15,Paixão de Cristo
        2022-04-21,Tiradentes
        2022-05-01,Dia do Trabalho
        2022-06-16,Corpus Christi
        2022-09-07,Independência do Brasil
        2022-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2022-11-02,Finados
        2022-11-15,Proclamação da República
        2022-12-25,Natal
        2023-01-01,Confraternização Universal
        2023-02-20,Carnaval
        2023-02-21,Carnaval
        2023-04-07,Paixão de Cristo
        2023-04-21,Tiradentes
        2023-05-01,Dia do Trabalho
        2023-06-08,Corpus Christi
        2023-09-07,Independência do Brasil
        2023-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2023-11-02,Finados
        2023-11-15,Proclamação da República
        2023-12-25,Natal
        2024-01-01,Confraternização Universal
        2024-02-12,Carnaval
        2024-02-13,Carnaval
        2024-03-29,Paixão de Cristo
        2024-04-21,Tiradentes
        2024-05-01,Dia do Trabalho
        2024-05-30,Corpus Christi
        2024-09-07,Independência do Brasil
        2024-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2024-11-02,Finados
        2024-11-15,Proclamação da República
        2024-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2024-12-25,Natal
        2025-01-01,Confraternização Universal
        2025-03-03,Carnaval
        2025-03-04,Carnaval
        2025-04-18,Paixão de Cristo
        2025-04-21,Tiradentes
        2025-05-01,Dia do Trabalho
        2025-06-19,Corpus Christi
        2025-09-07,Independência do Brasil
        2025-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2025-11-02,Finados
        2025-11-15,Proclamação da República
        2025-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2025-12-25,Natal
        2026-01-01,Confraternização Universal
        2026-02-16,Carnaval
        2026-02-17,Carnaval
        2026-04-03,Paixão de Cristo
        2026-04-21,Tiradentes
        2026-05-01,Dia do Trabalho
        2026-06-04,Corpus Christi
        2026-09-07,Independência do Brasil
        2026-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2026-11-02,Finados
        2026-11-15,Proclamação da República
        2026-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2026-12-25,Natal
        2027-01-01,Confraternização Universal
        2027-02-08,Carnaval
        2027-02-09,Carnaval
        2027-03-26,Paixão de Cristo
        2027-04-21,Tiradentes
        2027-05-01,Dia do Trabalho
        2027-05-27,Corpus Christi
        2027-09-07,Independência do Brasil
        2027-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2027-11-02,Finados
        2027-11-15,Proclamação da República
        2027-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2027-12-25,Natal
        2028-01-01,Confraternização Universal
        2028-02-28,Carnaval
        2028-02-29,Carnaval
        2028-04-14,Paixão de Cristo
        2028-04-21,Tiradentes
        2028-05-01,Dia do Trabalho
        2028-06-15,Corpus Christi
        2028-09-07,Independência do Brasil
        2028-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2028-11-02,Finados
        2028-11-15,Proclamação da República
        2028-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2028-12-25,Natal
        2029-01-01,Confraternização Universal
        2029-02-12,Carnaval
        2029-02-13,Carnaval
        2029-03-30,Paixão de Cristo
        2029-04-21,Tiradentes
        2029-05-01,Dia do Trabalho
        2029-05-31,Corpus Christi
        2029-09-07,Independência do Brasil
        2029-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2029-11-02,Finados
        2029-11-15,Proclamação da República
        2029-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2029-12-25,Natal
        2030-01-01,Confraternização Universal
        2030-03-04,Carnaval
        2030-03-05,Carnaval
        2030-04-19,Paixão de Cristo
        2030-04-21,Tiradentes
        2030-05-01,Dia do Trabalho
        2030-06-20,Corpus Christi
        2030-09-07,Independência do Brasil
        2030-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2030-11-02,Finados
        2030-11-15,Proclamação da República
        2030-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2030-12-25,Natal
        """;

    public static IReadOnlyList<(DateOnly Date, string Name)> Holidays { get; } =
        Csv
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(',', 2))
            .Select(parts => (
                Date: DateOnly.ParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Name: parts[1]))
            .ToList();
}
