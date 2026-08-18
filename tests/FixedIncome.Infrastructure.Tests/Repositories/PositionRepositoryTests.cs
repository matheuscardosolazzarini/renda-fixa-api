using FixedIncome.Domain.Entities;
using FixedIncome.Infrastructure.Repositories;

namespace FixedIncome.Infrastructure.Tests.Repositories;

public class PositionRepositoryTests : RepositoryTestBase
{
    [Fact]
    public async Task Aporte_persistido_e_recuperado_por_id()
    {
        // Arrange
        var asset = CriarTitulo();
        Context.FixedIncomeAssets.Add(asset);
        await Context.SaveChangesAsync();
        var repository = new PositionRepository(Context);
        var position = new Position(asset, 1_000m, new DateOnly(2025, 6, 1));

        // Act
        await repository.AddAsync(position);
        var result = await repository.GetByIdAsync(position.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(position.Id, result!.Id);
        Assert.Equal(position.InvestedAmount, result.InvestedAmount);
    }

    [Fact]
    public async Task Listar_por_titulo_retorna_apenas_os_aportes_daquele_titulo()
    {
        // Arrange
        var assetComAporte = CriarTitulo();
        var assetSemAporte = CriarTitulo();
        Context.FixedIncomeAssets.AddRange(assetComAporte, assetSemAporte);
        await Context.SaveChangesAsync();
        var repository = new PositionRepository(Context);
        var position = new Position(assetComAporte, 1_000m, new DateOnly(2025, 6, 1));
        await repository.AddAsync(position);

        // Act
        var result = (await repository.GetByAssetIdAsync(assetComAporte.Id)).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(position.Id, result[0].Id);
    }

    [Fact]
    public async Task Listar_por_titulo_sem_aportes_retorna_colecao_vazia()
    {
        // Arrange
        var asset = CriarTitulo();
        Context.FixedIncomeAssets.Add(asset);
        await Context.SaveChangesAsync();
        var repository = new PositionRepository(Context);

        // Act
        var result = await repository.GetByAssetIdAsync(asset.Id);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Listar_todos_retorna_aportes_ordenados_por_data_de_aporte_decrescente()
    {
        // Arrange
        var asset = CriarTitulo();
        Context.FixedIncomeAssets.Add(asset);
        await Context.SaveChangesAsync();
        var repository = new PositionRepository(Context);
        var positionAntiga = new Position(asset, 1_000m, new DateOnly(2025, 1, 10));
        var positionRecente = new Position(asset, 1_000m, new DateOnly(2025, 6, 1));
        var positionIntermediaria = new Position(asset, 1_000m, new DateOnly(2025, 3, 15));
        await repository.AddAsync(positionAntiga);
        await repository.AddAsync(positionRecente);
        await repository.AddAsync(positionIntermediaria);

        // Act
        var result = (await repository.GetAllAsync()).ToList();

        // Assert
        Assert.Equal(
            new[] { positionRecente.Id, positionIntermediaria.Id, positionAntiga.Id },
            result.Select(p => p.Id));
    }
}
