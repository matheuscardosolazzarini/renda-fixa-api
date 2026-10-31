using FixedIncome.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FixedIncome.Infrastructure.Tests.Repositories;

public class HolidayRepositoryTests : RepositoryTestBase
{
    [Fact]
    public async Task EnsureCreated_materializa_os_139_feriados_semeados()
    {
        // Arrange
        await Context.Database.EnsureCreatedAsync();

        // Act
        var quantidade = await Context.Holidays.CountAsync();

        // Assert
        Assert.Equal(139, quantidade);
    }

    [Fact]
    public async Task Listar_feriados_retorna_os_semeados_em_ordem_crescente_de_data()
    {
        // Arrange
        await Context.Database.EnsureCreatedAsync();
        var repository = new HolidayRepository(Context);

        // Act
        var datas = (await repository.GetAllAsync()).Select(h => h.Date).ToList();

        // Assert
        Assert.Equal(139, datas.Count);
        Assert.Equal(new DateOnly(2020, 1, 1), datas.First());
        Assert.Equal(new DateOnly(2030, 12, 25), datas.Last());
        Assert.Equal(datas.OrderBy(d => d).ToList(), datas);
    }

    [Fact]
    public async Task Feriados_de_2023_sao_12_e_de_2024_em_diante_sao_13()
    {
        // Arrange
        await Context.Database.EnsureCreatedAsync();
        var repository = new HolidayRepository(Context);

        // Act
        var datas = (await repository.GetAllAsync()).Select(h => h.Date).ToList();

        // Assert
        Assert.Equal(12, datas.Count(d => d.Year == 2023));
        Assert.Equal(13, datas.Count(d => d.Year == 2024));
        Assert.Equal(13, datas.Count(d => d.Year == 2030));
    }

    [Fact]
    public async Task Dia_da_Consciencia_Negra_so_entra_nos_feriados_a_partir_de_2024()
    {
        // Arrange
        await Context.Database.EnsureCreatedAsync();
        var repository = new HolidayRepository(Context);

        // Act
        var datas = (await repository.GetAllAsync()).Select(h => h.Date).ToList();

        // Assert
        Assert.DoesNotContain(new DateOnly(2023, 11, 20), datas);
        Assert.Contains(new DateOnly(2024, 11, 20), datas);
    }

    [Fact]
    public async Task Feriado_semeado_tem_id_fixo_derivado_da_data()
    {
        // Arrange
        await Context.Database.EnsureCreatedAsync();
        var repository = new HolidayRepository(Context);

        // Act
        var holidays = (await repository.GetAllAsync()).ToList();

        // Assert
        var confraternizacao2020 = holidays.Single(h => h.Date == new DateOnly(2020, 1, 1));
        Assert.Equal(new Guid("20200101-0000-0000-0000-000000000000"), confraternizacao2020.Id);
        Assert.Equal(139, holidays.Select(h => h.Id).Distinct().Count());
    }
}
