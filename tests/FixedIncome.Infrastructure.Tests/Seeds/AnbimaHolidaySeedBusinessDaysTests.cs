using FixedIncome.Domain.ValueObjects;
using FixedIncome.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FixedIncome.Infrastructure.Tests.Seeds;

// Contagem de dias úteis por ano civil do seed ANBIMA, conforme a seção "Feriados ANBIMA" de
// docs/SPEC-F7-EXECUCAO.md. O seed chega ao calendário pelo mesmo caminho da aplicação: HasData
// materializado pelo contexto e lido pelo repositório.
public class AnbimaHolidaySeedBusinessDaysTests : RepositoryTestBase
{
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
    public async Task Dias_uteis_do_ano_civil_batem_com_a_contagem_da_spec(int year, int expected)
    {
        // Arrange
        var calendar = await CriarCalendarioAsync();

        // Act
        var businessDays = calendar.CountBusinessDays(new DateOnly(year, 1, 1), new DateOnly(year + 1, 1, 1));

        // Assert
        Assert.Equal(expected, businessDays);
    }

    [Fact]
    public async Task Dias_uteis_de_2020_a_2029_somam_2504()
    {
        // Arrange
        var calendar = await CriarCalendarioAsync();

        // Act
        var businessDays = calendar.CountBusinessDays(new DateOnly(2020, 1, 1), new DateOnly(2030, 1, 1));

        // Assert
        Assert.Equal(2504, businessDays);
    }

    private async Task<BusinessDayCalendar> CriarCalendarioAsync()
    {
        await Context.Database.EnsureCreatedAsync();
        var holidays = await new HolidayRepository(Context).GetAllAsync();

        return new BusinessDayCalendar(holidays);
    }
}
