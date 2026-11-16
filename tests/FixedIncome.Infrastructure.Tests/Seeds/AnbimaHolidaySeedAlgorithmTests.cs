using FixedIncome.Infrastructure.Repositories;
using FixedIncome.Tests.Shared;

namespace FixedIncome.Infrastructure.Tests.Seeds;

// Confere o seed de feriados nacionais ANBIMA contra a regra que o gera: datas fixas mais as
// móveis derivadas da Páscoa gregoriana. O seed de produção segue sendo uma lista estática (o HasData
// precisa de dados fixos); o algoritmo existe só aqui, como oráculo. O seed é lido do banco do teste,
// onde chega pelas migrations, o mesmo caminho da aplicação. As datas de referência dos testes
// de 2035 a 2049 vêm de fonte independente (tabela de Páscoa do US Census e páginas anuais da ANBIMA).
[Collection("PostgreSQL")]
public class AnbimaHolidaySeedAlgorithmTests(PostgreSqlFixture fixture) : RepositoryTestBase(fixture)
{
    private const int FirstYearWithConsciousnessDay = 2024;

    [Fact]
    public async Task Gerador_por_algoritmo_reproduz_exatamente_os_feriados_semeados_de_2020_a_2030()
    {
        // Arrange
        var seeded = (await LerSeedAsync()).Where(h => h.Date.Year <= 2030).ToList();

        // Act
        var generated = GenerateHolidays(2020, 2030);

        // Assert
        Assert.Equal(139, generated.Count);
        Assert.Equal(generated, seeded);
    }

    [Fact]
    public async Task Feriados_de_2031_a_2050_sao_os_gerados_pelo_algoritmo()
    {
        // Arrange
        var seeded = (await LerSeedAsync()).Where(h => h.Date.Year >= 2031).ToList();

        // Act
        var generated = GenerateHolidays(2031, 2050);

        // Assert
        Assert.Equal(260, generated.Count);
        Assert.Equal(generated, seeded);
    }

    [Fact]
    public async Task Seed_tem_399_feriados_e_termina_em_2050_12_25()
    {
        // Arrange
        var seeded = await LerSeedAsync();

        // Act
        var last = seeded.Last().Date;

        // Assert
        Assert.Equal(399, seeded.Count);
        Assert.Equal(new DateOnly(2050, 12, 25), last);
    }

    [Fact]
    public async Task Cada_ano_de_2024_a_2050_tem_13_feriados()
    {
        // Arrange
        var seeded = await LerSeedAsync();

        // Act
        var wrongYears = seeded
            .GroupBy(h => h.Date.Year)
            .Where(g => g.Key >= 2024 && g.Count() != 13)
            .Select(g => $"{g.Key}: {g.Count()}")
            .ToList();

        // Assert
        Assert.True(wrongYears.Count == 0, $"Anos de 2024 a 2050 que não têm 13 feriados: {string.Join("; ", wrongYears)}.");
        Assert.Equal(27, seeded.Where(h => h.Date.Year >= 2024).Select(h => h.Date.Year).Distinct().Count());
    }

    // Valores conferidos à mão: Páscoa pela tabela do US Census; os demais, por aritmética de calendário
    // a partir dela (Carnaval = Páscoa − 48 e − 47; Paixão = Páscoa − 2; Corpus Christi = Páscoa + 60).
    [Theory]
    [InlineData(2035, "2035-02-05", "2035-02-06", "2035-03-23", "2035-05-24")]
    [InlineData(2038, "2038-03-08", "2038-03-09", "2038-04-23", "2038-06-24")]
    [InlineData(2043, "2043-02-09", "2043-02-10", "2043-03-27", "2043-05-28")]
    [InlineData(2044, "2044-02-29", "2044-03-01", "2044-04-15", "2044-06-16")]
    [InlineData(2049, "2049-03-01", "2049-03-02", "2049-04-16", "2049-06-17")]
    public async Task Feriados_moveis_de_anos_de_referencia_batem_com_a_pascoa_conferida_a_mao(
        int year, string carnivalMonday, string carnivalTuesday, string goodFriday, string corpusChristi)
    {
        // Arrange
        var seeded = await LerSeedAsync();

        // Act
        var ofYear = seeded.Where(h => h.Date.Year == year).ToList();

        // Assert
        Assert.Contains((DateOnly.Parse(carnivalMonday), "Carnaval"), ofYear);
        Assert.Contains((DateOnly.Parse(carnivalTuesday), "Carnaval"), ofYear);
        Assert.Contains((DateOnly.Parse(goodFriday), "Paixão de Cristo"), ofYear);
        Assert.Contains((DateOnly.Parse(corpusChristi), "Corpus Christi"), ofYear);
    }

    // ANBIMA lista o feriado nacional mesmo quando ele cai em sábado ou domingo.
    [Theory]
    [InlineData("2035-04-21", "Tiradentes", DayOfWeek.Saturday)]
    [InlineData("2038-05-01", "Dia do Trabalho", DayOfWeek.Saturday)]
    [InlineData("2038-11-20", "Dia Nacional de Zumbi e da Consciência Negra", DayOfWeek.Saturday)]
    [InlineData("2038-12-25", "Natal", DayOfWeek.Saturday)]
    [InlineData("2044-05-01", "Dia do Trabalho", DayOfWeek.Sunday)]
    [InlineData("2044-11-20", "Dia Nacional de Zumbi e da Consciência Negra", DayOfWeek.Sunday)]
    [InlineData("2044-12-25", "Natal", DayOfWeek.Sunday)]
    public async Task Feriado_em_fim_de_semana_esta_no_seed(string date, string name, DayOfWeek expectedDay)
    {
        // Arrange
        var seeded = await LerSeedAsync();
        var holiday = DateOnly.Parse(date);

        // Act
        var dayOfWeek = holiday.DayOfWeek;

        // Assert
        Assert.Equal(expectedDay, dayOfWeek);
        Assert.Contains((holiday, name), seeded);
    }

    private async Task<List<(DateOnly Date, string Name)>> LerSeedAsync()
    {
        var holidays = await new HolidayRepository(Context).GetAllAsync();

        return holidays.OrderBy(h => h.Date).Select(h => (h.Date, h.Name)).ToList();
    }

    private static List<(DateOnly Date, string Name)> GenerateHolidays(int firstYear, int lastYear)
    {
        var holidays = new List<(DateOnly Date, string Name)>();

        for (var year = firstYear; year <= lastYear; year++)
        {
            var easter = EasterSunday(year);

            holidays.Add((new DateOnly(year, 1, 1), "Confraternização Universal"));
            holidays.Add((easter.AddDays(-48), "Carnaval"));
            holidays.Add((easter.AddDays(-47), "Carnaval"));
            holidays.Add((easter.AddDays(-2), "Paixão de Cristo"));
            holidays.Add((new DateOnly(year, 4, 21), "Tiradentes"));
            holidays.Add((new DateOnly(year, 5, 1), "Dia do Trabalho"));
            holidays.Add((easter.AddDays(60), "Corpus Christi"));
            holidays.Add((new DateOnly(year, 9, 7), "Independência do Brasil"));
            holidays.Add((new DateOnly(year, 10, 12), "Nossa Sr.a Aparecida - Padroeira do Brasil"));
            holidays.Add((new DateOnly(year, 11, 2), "Finados"));
            holidays.Add((new DateOnly(year, 11, 15), "Proclamação da República"));

            if (year >= FirstYearWithConsciousnessDay)
            {
                holidays.Add((new DateOnly(year, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra"));
            }

            holidays.Add((new DateOnly(year, 12, 25), "Natal"));
        }

        return holidays.OrderBy(h => h.Date).ToList();
    }

    // Algoritmo anônimo gregoriano (Meeus/Jones/Butcher).
    private static DateOnly EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;

        return new DateOnly(year, month, day);
    }
}
