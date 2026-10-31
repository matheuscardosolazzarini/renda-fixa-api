using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FixedIncome.Infrastructure.Tests.Repositories;

public class IndexRateRepositoryTests : RepositoryTestBase
{
    [Fact]
    public async Task Upsert_com_os_mesmos_dados_duas_vezes_nao_duplica_as_cotacoes()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        await repository.UpsertAsync(CriarCotacoesCdi(0.05m, 0.06m));

        // Act
        await repository.UpsertAsync(CriarCotacoesCdi(0.05m, 0.06m));

        // Assert
        Assert.Equal(2, await Context.IndexRates.CountAsync());
    }

    [Fact]
    public async Task Upsert_com_valor_diferente_atualiza_a_cotacao_existente_sem_trocar_o_id()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        var original = new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 2), 0.05m);
        await repository.UpsertAsync(new[] { original });

        // Act
        await repository.UpsertAsync(new[] { new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 2), 0.07m) });

        // Assert
        var stored = await Context.IndexRates.AsNoTracking().SingleAsync();
        Assert.Equal(original.Id, stored.Id);
        Assert.Equal(0.07m, stored.Value);
    }

    [Fact]
    public async Task Upsert_insere_apenas_as_datas_novas_e_mantem_as_existentes()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        await repository.UpsertAsync(CriarCotacoesCdi(0.05m, 0.06m));

        // Act
        await repository.UpsertAsync(new[]
        {
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 3), 0.06m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 4), 0.08m),
        });

        // Assert
        var stored = await Context.IndexRates.AsNoTracking().OrderBy(r => r.ReferenceDate).ToListAsync();
        Assert.Equal(
            new[] { new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 3), new DateOnly(2025, 6, 4) },
            stored.Select(r => r.ReferenceDate));
    }

    [Fact]
    public async Task Upsert_da_mesma_data_para_outro_indexador_nao_altera_a_cotacao_existente()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        await repository.UpsertAsync(new[] { new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 1), 0.05m) });

        // Act
        await repository.UpsertAsync(new[] { new IndexRate(IndexType.Ipca, new DateOnly(2025, 6, 1), 0.35m) });

        // Assert
        var stored = await Context.IndexRates.AsNoTracking().ToListAsync();
        Assert.Equal(2, stored.Count);
        Assert.Equal(0.05m, stored.Single(r => r.IndexType == IndexType.Cdi).Value);
        Assert.Equal(0.35m, stored.Single(r => r.IndexType == IndexType.Ipca).Value);
    }

    [Fact]
    public async Task Upsert_com_colecao_vazia_nao_altera_as_cotacoes()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        await repository.UpsertAsync(CriarCotacoesCdi(0.05m));

        // Act
        await repository.UpsertAsync(Array.Empty<IndexRate>());

        // Assert
        Assert.Equal(1, await Context.IndexRates.CountAsync());
    }

    [Fact]
    public async Task Listar_por_periodo_inclui_as_datas_de_inicio_e_fim()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        await repository.UpsertAsync(new[]
        {
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 2), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 13), 0.06m),
        });

        // Act
        var result = (await repository.GetByIndexTypeAndPeriodAsync(
            IndexType.Cdi,
            new DateOnly(2025, 6, 2),
            new DateOnly(2025, 6, 13))).ToList();

        // Assert
        Assert.Equal(
            new[] { new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 13) },
            result.Select(r => r.ReferenceDate));
    }

    [Fact]
    public async Task Listar_por_periodo_deixa_de_fora_as_datas_vizinhas_ao_intervalo()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        await repository.UpsertAsync(new[]
        {
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 1), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 2), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 13), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 16), 0.05m),
        });

        // Act
        var result = (await repository.GetByIndexTypeAndPeriodAsync(
            IndexType.Cdi,
            new DateOnly(2025, 6, 2),
            new DateOnly(2025, 6, 13))).ToList();

        // Assert
        Assert.Equal(
            new[] { new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 13) },
            result.Select(r => r.ReferenceDate));
    }

    [Fact]
    public async Task Listar_por_periodo_ignora_cotacoes_de_outro_indexador()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        await repository.UpsertAsync(new[]
        {
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 2), 0.05m),
            new IndexRate(IndexType.Ipca, new DateOnly(2025, 6, 1), 0.35m),
        });

        // Act
        var result = (await repository.GetByIndexTypeAndPeriodAsync(
            IndexType.Ipca,
            new DateOnly(2025, 6, 1),
            new DateOnly(2025, 6, 30))).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(IndexType.Ipca, result[0].IndexType);
    }

    [Fact]
    public async Task Listar_por_periodo_retorna_as_cotacoes_em_ordem_crescente_de_data()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        await repository.UpsertAsync(new[]
        {
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 4), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 2), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 3), 0.05m),
        });

        // Act
        var result = (await repository.GetByIndexTypeAndPeriodAsync(
            IndexType.Cdi,
            new DateOnly(2025, 6, 1),
            new DateOnly(2025, 6, 30))).ToList();

        // Assert
        Assert.Equal(
            new[] { new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 3), new DateOnly(2025, 6, 4) },
            result.Select(r => r.ReferenceDate));
    }

    [Fact]
    public async Task Ultima_data_sem_nenhuma_cotacoes_do_indexador_e_nula()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);

        // Act
        var result = await repository.GetLastReferenceDateAsync(IndexType.Cdi);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Ultima_data_e_a_maior_data_importada_do_indexador()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        await repository.UpsertAsync(new[]
        {
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 3), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 30), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 10), 0.05m),
        });

        // Act
        var result = await repository.GetLastReferenceDateAsync(IndexType.Cdi);

        // Assert
        Assert.Equal(new DateOnly(2025, 6, 30), result);
    }

    [Fact]
    public async Task Ultima_data_de_um_indexador_sem_cotacoes_e_nula_mesmo_que_outro_tenha_dados()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        await repository.UpsertAsync(new[] { new IndexRate(IndexType.Ipca, new DateOnly(2025, 6, 1), 0.35m) });

        // Act
        var result = await repository.GetLastReferenceDateAsync(IndexType.Cdi);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Ultima_cotacao_anterior_a_uma_data_devolve_a_mais_recente_estritamente_anterior()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        await repository.UpsertAsync(new[]
        {
            new IndexRate(IndexType.Ipca, new DateOnly(2025, 5, 1), 0.26m),
            new IndexRate(IndexType.Ipca, new DateOnly(2025, 6, 1), 0.24m),
            new IndexRate(IndexType.Ipca, new DateOnly(2025, 7, 1), 0.26m),
        });

        // Act
        var result = await repository.GetLastBeforeAsync(IndexType.Ipca, new DateOnly(2025, 7, 1));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(new DateOnly(2025, 6, 1), result.ReferenceDate);
        Assert.Equal(0.24m, result.Value);
    }

    [Fact]
    public async Task Ultima_cotacao_anterior_sem_nenhuma_cotacao_antes_da_data_e_nula()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        await repository.UpsertAsync(new[] { new IndexRate(IndexType.Ipca, new DateOnly(2025, 7, 1), 0.26m) });

        // Act
        var result = await repository.GetLastBeforeAsync(IndexType.Ipca, new DateOnly(2025, 7, 1));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Ultima_cotacao_anterior_ignora_cotacoes_de_outro_indexador()
    {
        // Arrange
        var repository = new IndexRateRepository(Context);
        await repository.UpsertAsync(new[] { new IndexRate(IndexType.Cdi, new DateOnly(2025, 6, 2), 0.05m) });

        // Act
        var result = await repository.GetLastBeforeAsync(IndexType.Ipca, new DateOnly(2025, 7, 1));

        // Assert
        Assert.Null(result);
    }

    private static IReadOnlyList<IndexRate> CriarCotacoesCdi(params decimal[] valores)
    {
        // Datas consecutivas a partir de 02/06/2025. Cada chamada cria instâncias novas, como a importação faria.
        var inicio = new DateOnly(2025, 6, 2);
        return valores
            .Select((valor, i) => new IndexRate(IndexType.Cdi, inicio.AddDays(i), valor))
            .ToList();
    }
}
