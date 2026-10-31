using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Tests.TestData;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Domain.Tests.ValueObjects;

public class BusinessDayCalendarTests
{
    private static BusinessDayCalendar BuildCalendar() => new(AnbimaHolidays.All());

    // Casos 2.1 a 2.12 de docs/SPEC-F7-EXECUCAO.md (RN-08: inclui o início, exclui o fim).
    [Theory]
    [InlineData("2026-09-11", "2026-09-14", 1)] // 2.1 sexta para segunda
    [InlineData("2026-04-20", "2026-04-24", 3)] // 2.2 Tiradentes na terça
    [InlineData("2026-09-04", "2026-09-08", 1)] // 2.3 feriado na segunda (07/09)
    [InlineData("2025-12-31", "2026-01-05", 2)] // 2.4 virada de ano, 31/12 é dia útil
    [InlineData("2020-12-31", "2021-01-04", 1)] // 2.5 virada com 01/01 na sexta
    [InlineData("2026-02-13", "2026-02-18", 1)] // 2.6 Carnaval 16 e 17/02
    [InlineData("2026-02-13", "2026-02-19", 2)] // 2.7 Quarta de Cinzas é dia útil
    [InlineData("2020-02-21", "2020-02-26", 1)] // 2.8 Carnaval 2020
    [InlineData("2026-11-19", "2026-11-23", 1)] // 2.9 Consciência Negra na sexta
    [InlineData("2026-09-07", "2026-09-08", 0)] // 2.10 início em feriado
    [InlineData("2026-09-12", "2026-09-14", 0)] // 2.11 início no sábado
    [InlineData("2026-09-14", "2026-09-14", 0)] // 2.12 start == end
    public void Contagem_de_dias_uteis_segue_os_casos_de_referencia(string start, string end, int expected)
    {
        // Arrange
        var calendar = BuildCalendar();

        // Act
        var count = calendar.CountBusinessDays(DateOnly.Parse(start), DateOnly.Parse(end));

        // Assert
        Assert.Equal(expected, count);
    }

    [Fact]
    public void Contagem_com_inicio_fora_da_cobertura_lanca_excecao_com_o_inicio_da_cobertura() // 2.13
    {
        // Arrange
        var calendar = BuildCalendar();

        // Act
        Action act = () => calendar.CountBusinessDays(new DateOnly(2019, 12, 31), new DateOnly(2020, 1, 2));

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Contains("2020-01-01", exception.Message);
    }

    [Fact]
    public void Contagem_com_fim_anterior_ao_inicio_lanca_excecao() // 2.14
    {
        // Arrange
        var calendar = BuildCalendar();

        // Act
        Action act = () => calendar.CountBusinessDays(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 11));

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Contagem_com_inicio_igual_ao_fim_fora_da_cobertura_lanca_excecao()
    {
        // Arrange
        var calendar = BuildCalendar();
        var date = new DateOnly(2031, 6, 2);

        // Act
        Action act = () => calendar.CountBusinessDays(date, date);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Contagem_com_fim_um_dia_apos_a_cobertura_e_aceita_porque_o_fim_e_exclusivo()
    {
        // Arrange
        var calendar = BuildCalendar();

        // Act
        var count = calendar.CountBusinessDays(new DateOnly(2030, 12, 30), new DateOnly(2031, 1, 1));

        // Assert
        Assert.Equal(2, count); // 30/12 (seg) e 31/12 (ter)
    }

    [Fact]
    public void Contagem_com_fim_dois_dias_apos_a_cobertura_lanca_excecao_com_o_fim_da_cobertura()
    {
        // Arrange
        var calendar = BuildCalendar();

        // Act
        Action act = () => calendar.CountBusinessDays(new DateOnly(2030, 12, 30), new DateOnly(2031, 1, 2));

        // Assert
        var exception = Assert.Throws<DomainException>(act);
        Assert.Contains("2030-12-31", exception.Message);
    }

    [Fact]
    public void Contagem_de_2020_a_2029_confere_com_o_caso_de_referencia_1_4()
    {
        // Arrange
        var calendar = BuildCalendar();

        // Act
        var count = calendar.CountBusinessDays(new DateOnly(2020, 1, 1), new DateOnly(2030, 1, 1));

        // Assert
        Assert.Equal(2504, count);
    }

    [Theory]
    [InlineData(2020, 251)]
    [InlineData(2021, 251)]
    [InlineData(2022, 251)]
    [InlineData(2023, 249)]
    [InlineData(2024, 253)]
    [InlineData(2025, 252)]
    [InlineData(2026, 249)]
    [InlineData(2027, 251)]
    [InlineData(2028, 248)]
    [InlineData(2029, 249)]
    [InlineData(2030, 252)]
    public void Dias_uteis_do_ano_civil_conferem_com_a_tabela_da_spec(int year, int expected)
    {
        // Arrange
        var calendar = BuildCalendar();

        // Act
        var count = calendar.CountBusinessDays(new DateOnly(year, 1, 1), new DateOnly(year + 1, 1, 1));

        // Assert
        Assert.Equal(expected, count);
    }

    [Theory]
    [InlineData("2020-01-01", "2020-12-31", 250)] // caso 1.1
    [InlineData("2025-01-01", "2026-01-01", 252)] // caso 1.3; sem o feriado de 20/11 seriam 253
    public void Contagem_dos_intervalos_dos_casos_de_referencia_de_prefixado(string start, string end, int expected)
    {
        // Arrange
        var calendar = BuildCalendar();

        // Act
        var count = calendar.CountBusinessDays(DateOnly.Parse(start), DateOnly.Parse(end));

        // Assert
        Assert.Equal(expected, count);
    }

    [Fact]
    public void Lista_de_feriados_de_teste_tem_139_datas_distintas()
    {
        // Arrange
        var holidays = AnbimaHolidays.All();

        // Act
        var distinctDates = holidays.Select(h => h.Date).Distinct().Count();

        // Assert
        Assert.Equal(139, holidays.Count);
        Assert.Equal(139, distinctDates);
    }

    [Fact]
    public void Calendario_sem_feriados_e_rejeitado()
    {
        // Arrange
        var holidays = Array.Empty<Holiday>();

        // Act
        Action act = () => new BusinessDayCalendar(holidays);

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Theory]
    [InlineData("2026-09-14", true)]  // segunda
    [InlineData("2026-09-11", true)]  // sexta
    [InlineData("2026-09-12", false)] // sábado
    [InlineData("2026-09-13", false)] // domingo
    [InlineData("2026-09-07", false)] // Independência (segunda)
    [InlineData("2025-12-31", true)]  // último dia útil do ano não é feriado ANBIMA
    [InlineData("2026-02-18", true)]  // Quarta de Cinzas
    [InlineData("2025-11-20", false)] // Consciência Negra
    [InlineData("2020-01-01", false)] // primeiro dia da cobertura, feriado
    [InlineData("2030-12-31", true)]  // último dia da cobertura
    public void IsBusinessDay_considera_fim_de_semana_e_feriados(string date, bool expected)
    {
        // Arrange
        var calendar = BuildCalendar();

        // Act
        var result = calendar.IsBusinessDay(DateOnly.Parse(date));

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("2019-12-31")]
    [InlineData("2031-01-01")]
    public void IsBusinessDay_fora_da_cobertura_lanca_excecao(string date)
    {
        // Arrange
        var calendar = BuildCalendar();

        // Act
        Action act = () => calendar.IsBusinessDay(DateOnly.Parse(date));

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Theory]
    [InlineData("2026-09-11", "2026-09-14")] // sexta para segunda
    [InlineData("2026-09-04", "2026-09-08")] // pula fim de semana e feriado de 07/09
    [InlineData("2026-09-14", "2026-09-15")] // dia comum
    [InlineData("2026-09-12", "2026-09-14")] // a partir do sábado
    [InlineData("2026-09-07", "2026-09-08")] // a partir de feriado
    [InlineData("2025-12-31", "2026-01-02")] // virada de ano, 01/01 é feriado
    [InlineData("2026-02-13", "2026-02-18")] // Carnaval
    public void NextBusinessDay_devolve_o_dia_util_estritamente_posterior(string date, string expected)
    {
        // Arrange
        var calendar = BuildCalendar();

        // Act
        var next = calendar.NextBusinessDay(DateOnly.Parse(date));

        // Assert
        Assert.Equal(DateOnly.Parse(expected), next);
    }

    [Fact]
    public void NextBusinessDay_alem_da_cobertura_lanca_excecao()
    {
        // Arrange
        var calendar = BuildCalendar();

        // Act
        Action act = () => calendar.NextBusinessDay(new DateOnly(2030, 12, 31));

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void NextBusinessDay_antes_da_cobertura_lanca_excecao()
    {
        // Arrange
        var calendar = BuildCalendar();

        // Act
        Action act = () => calendar.NextBusinessDay(new DateOnly(2019, 12, 31));

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void BusinessDaysBetween_lista_os_dias_uteis_do_caso_de_referencia_3()
    {
        // Arrange
        var calendar = BuildCalendar();
        var expected = new[]
        {
            new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 16),
            new DateOnly(2026, 9, 17), new DateOnly(2026, 9, 18), new DateOnly(2026, 9, 21),
            new DateOnly(2026, 9, 22)
        };

        // Act
        var days = calendar.BusinessDaysBetween(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 23));

        // Assert
        Assert.Equal(expected, days);
    }

    [Fact]
    public void BusinessDaysBetween_exclui_o_feriado_do_caso_de_referencia_4()
    {
        // Arrange
        var calendar = BuildCalendar();

        // Act
        var days = calendar.BusinessDaysBetween(new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1));

        // Assert
        Assert.Equal(21, days.Count);
        Assert.DoesNotContain(new DateOnly(2026, 9, 7), days);
        Assert.Equal(new DateOnly(2026, 9, 30), days[^1]);
    }

    [Fact]
    public void BusinessDaysBetween_com_inicio_igual_ao_fim_devolve_lista_vazia()
    {
        // Arrange
        var calendar = BuildCalendar();
        var date = new DateOnly(2026, 9, 14);

        // Act
        var days = calendar.BusinessDaysBetween(date, date);

        // Assert
        Assert.Empty(days);
    }

    [Fact]
    public void BusinessDaysBetween_com_fim_anterior_ao_inicio_lanca_excecao()
    {
        // Arrange
        var calendar = BuildCalendar();

        // Act
        Action act = () => calendar.BusinessDaysBetween(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 11));

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void BusinessDaysBetween_com_inicio_fora_da_cobertura_lanca_excecao()
    {
        // Arrange
        var calendar = BuildCalendar();

        // Act
        Action act = () => calendar.BusinessDaysBetween(new DateOnly(2019, 12, 31), new DateOnly(2020, 1, 2));

        // Assert
        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void BusinessDaysBetween_tem_a_mesma_quantidade_que_CountBusinessDays()
    {
        // Arrange
        var calendar = BuildCalendar();
        var start = new DateOnly(2025, 1, 1);
        var end = new DateOnly(2026, 1, 1);

        // Act
        var days = calendar.BusinessDaysBetween(start, end);

        // Assert
        Assert.Equal(calendar.CountBusinessDays(start, end), days.Count);
    }
}
