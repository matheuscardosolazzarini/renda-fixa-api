using System.Globalization;

namespace FixedIncome.Infrastructure.Seeds;

// Feriados nacionais ANBIMA de 2020 a 2050 (399 datas, inclusive os que caem em fim de
// semana). De 2020 a 2030, conforme a seção "Feriados ANBIMA" de docs/SPEC-F7-EXECUCAO.md; de 2031
// a 2050 (260 datas, migration ExtendAnbimaHolidaysTo2050), as fixas mais Carnaval, Paixão de Cristo
// e Corpus Christi a partir da Páscoa gregoriana, conferidas contra as páginas anuais da ANBIMA
// (https://www.anbima.com.br/feriados/fer_nacionais/AAAA.asp). AnbimaHolidaySeedAlgorithmTests
// regenera a lista por algoritmo e compara com ela.
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
        2031-01-01,Confraternização Universal
        2031-02-24,Carnaval
        2031-02-25,Carnaval
        2031-04-11,Paixão de Cristo
        2031-04-21,Tiradentes
        2031-05-01,Dia do Trabalho
        2031-06-12,Corpus Christi
        2031-09-07,Independência do Brasil
        2031-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2031-11-02,Finados
        2031-11-15,Proclamação da República
        2031-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2031-12-25,Natal
        2032-01-01,Confraternização Universal
        2032-02-09,Carnaval
        2032-02-10,Carnaval
        2032-03-26,Paixão de Cristo
        2032-04-21,Tiradentes
        2032-05-01,Dia do Trabalho
        2032-05-27,Corpus Christi
        2032-09-07,Independência do Brasil
        2032-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2032-11-02,Finados
        2032-11-15,Proclamação da República
        2032-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2032-12-25,Natal
        2033-01-01,Confraternização Universal
        2033-02-28,Carnaval
        2033-03-01,Carnaval
        2033-04-15,Paixão de Cristo
        2033-04-21,Tiradentes
        2033-05-01,Dia do Trabalho
        2033-06-16,Corpus Christi
        2033-09-07,Independência do Brasil
        2033-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2033-11-02,Finados
        2033-11-15,Proclamação da República
        2033-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2033-12-25,Natal
        2034-01-01,Confraternização Universal
        2034-02-20,Carnaval
        2034-02-21,Carnaval
        2034-04-07,Paixão de Cristo
        2034-04-21,Tiradentes
        2034-05-01,Dia do Trabalho
        2034-06-08,Corpus Christi
        2034-09-07,Independência do Brasil
        2034-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2034-11-02,Finados
        2034-11-15,Proclamação da República
        2034-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2034-12-25,Natal
        2035-01-01,Confraternização Universal
        2035-02-05,Carnaval
        2035-02-06,Carnaval
        2035-03-23,Paixão de Cristo
        2035-04-21,Tiradentes
        2035-05-01,Dia do Trabalho
        2035-05-24,Corpus Christi
        2035-09-07,Independência do Brasil
        2035-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2035-11-02,Finados
        2035-11-15,Proclamação da República
        2035-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2035-12-25,Natal
        2036-01-01,Confraternização Universal
        2036-02-25,Carnaval
        2036-02-26,Carnaval
        2036-04-11,Paixão de Cristo
        2036-04-21,Tiradentes
        2036-05-01,Dia do Trabalho
        2036-06-12,Corpus Christi
        2036-09-07,Independência do Brasil
        2036-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2036-11-02,Finados
        2036-11-15,Proclamação da República
        2036-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2036-12-25,Natal
        2037-01-01,Confraternização Universal
        2037-02-16,Carnaval
        2037-02-17,Carnaval
        2037-04-03,Paixão de Cristo
        2037-04-21,Tiradentes
        2037-05-01,Dia do Trabalho
        2037-06-04,Corpus Christi
        2037-09-07,Independência do Brasil
        2037-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2037-11-02,Finados
        2037-11-15,Proclamação da República
        2037-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2037-12-25,Natal
        2038-01-01,Confraternização Universal
        2038-03-08,Carnaval
        2038-03-09,Carnaval
        2038-04-21,Tiradentes
        2038-04-23,Paixão de Cristo
        2038-05-01,Dia do Trabalho
        2038-06-24,Corpus Christi
        2038-09-07,Independência do Brasil
        2038-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2038-11-02,Finados
        2038-11-15,Proclamação da República
        2038-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2038-12-25,Natal
        2039-01-01,Confraternização Universal
        2039-02-21,Carnaval
        2039-02-22,Carnaval
        2039-04-08,Paixão de Cristo
        2039-04-21,Tiradentes
        2039-05-01,Dia do Trabalho
        2039-06-09,Corpus Christi
        2039-09-07,Independência do Brasil
        2039-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2039-11-02,Finados
        2039-11-15,Proclamação da República
        2039-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2039-12-25,Natal
        2040-01-01,Confraternização Universal
        2040-02-13,Carnaval
        2040-02-14,Carnaval
        2040-03-30,Paixão de Cristo
        2040-04-21,Tiradentes
        2040-05-01,Dia do Trabalho
        2040-05-31,Corpus Christi
        2040-09-07,Independência do Brasil
        2040-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2040-11-02,Finados
        2040-11-15,Proclamação da República
        2040-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2040-12-25,Natal
        2041-01-01,Confraternização Universal
        2041-03-04,Carnaval
        2041-03-05,Carnaval
        2041-04-19,Paixão de Cristo
        2041-04-21,Tiradentes
        2041-05-01,Dia do Trabalho
        2041-06-20,Corpus Christi
        2041-09-07,Independência do Brasil
        2041-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2041-11-02,Finados
        2041-11-15,Proclamação da República
        2041-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2041-12-25,Natal
        2042-01-01,Confraternização Universal
        2042-02-17,Carnaval
        2042-02-18,Carnaval
        2042-04-04,Paixão de Cristo
        2042-04-21,Tiradentes
        2042-05-01,Dia do Trabalho
        2042-06-05,Corpus Christi
        2042-09-07,Independência do Brasil
        2042-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2042-11-02,Finados
        2042-11-15,Proclamação da República
        2042-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2042-12-25,Natal
        2043-01-01,Confraternização Universal
        2043-02-09,Carnaval
        2043-02-10,Carnaval
        2043-03-27,Paixão de Cristo
        2043-04-21,Tiradentes
        2043-05-01,Dia do Trabalho
        2043-05-28,Corpus Christi
        2043-09-07,Independência do Brasil
        2043-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2043-11-02,Finados
        2043-11-15,Proclamação da República
        2043-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2043-12-25,Natal
        2044-01-01,Confraternização Universal
        2044-02-29,Carnaval
        2044-03-01,Carnaval
        2044-04-15,Paixão de Cristo
        2044-04-21,Tiradentes
        2044-05-01,Dia do Trabalho
        2044-06-16,Corpus Christi
        2044-09-07,Independência do Brasil
        2044-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2044-11-02,Finados
        2044-11-15,Proclamação da República
        2044-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2044-12-25,Natal
        2045-01-01,Confraternização Universal
        2045-02-20,Carnaval
        2045-02-21,Carnaval
        2045-04-07,Paixão de Cristo
        2045-04-21,Tiradentes
        2045-05-01,Dia do Trabalho
        2045-06-08,Corpus Christi
        2045-09-07,Independência do Brasil
        2045-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2045-11-02,Finados
        2045-11-15,Proclamação da República
        2045-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2045-12-25,Natal
        2046-01-01,Confraternização Universal
        2046-02-05,Carnaval
        2046-02-06,Carnaval
        2046-03-23,Paixão de Cristo
        2046-04-21,Tiradentes
        2046-05-01,Dia do Trabalho
        2046-05-24,Corpus Christi
        2046-09-07,Independência do Brasil
        2046-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2046-11-02,Finados
        2046-11-15,Proclamação da República
        2046-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2046-12-25,Natal
        2047-01-01,Confraternização Universal
        2047-02-25,Carnaval
        2047-02-26,Carnaval
        2047-04-12,Paixão de Cristo
        2047-04-21,Tiradentes
        2047-05-01,Dia do Trabalho
        2047-06-13,Corpus Christi
        2047-09-07,Independência do Brasil
        2047-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2047-11-02,Finados
        2047-11-15,Proclamação da República
        2047-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2047-12-25,Natal
        2048-01-01,Confraternização Universal
        2048-02-17,Carnaval
        2048-02-18,Carnaval
        2048-04-03,Paixão de Cristo
        2048-04-21,Tiradentes
        2048-05-01,Dia do Trabalho
        2048-06-04,Corpus Christi
        2048-09-07,Independência do Brasil
        2048-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2048-11-02,Finados
        2048-11-15,Proclamação da República
        2048-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2048-12-25,Natal
        2049-01-01,Confraternização Universal
        2049-03-01,Carnaval
        2049-03-02,Carnaval
        2049-04-16,Paixão de Cristo
        2049-04-21,Tiradentes
        2049-05-01,Dia do Trabalho
        2049-06-17,Corpus Christi
        2049-09-07,Independência do Brasil
        2049-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2049-11-02,Finados
        2049-11-15,Proclamação da República
        2049-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2049-12-25,Natal
        2050-01-01,Confraternização Universal
        2050-02-21,Carnaval
        2050-02-22,Carnaval
        2050-04-08,Paixão de Cristo
        2050-04-21,Tiradentes
        2050-05-01,Dia do Trabalho
        2050-06-09,Corpus Christi
        2050-09-07,Independência do Brasil
        2050-10-12,Nossa Sr.a Aparecida - Padroeira do Brasil
        2050-11-02,Finados
        2050-11-15,Proclamação da República
        2050-11-20,Dia Nacional de Zumbi e da Consciência Negra
        2050-12-25,Natal
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
