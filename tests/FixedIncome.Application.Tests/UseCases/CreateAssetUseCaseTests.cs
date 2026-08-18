using FixedIncome.Application.DTOs;
using FixedIncome.Application.Repositories;
using FixedIncome.Application.UseCases;
using FixedIncome.Domain.Entities;

namespace FixedIncome.Application.Tests.UseCases;

public class CreateAssetUseCaseTests
{
    private static CreateAssetRequest CriarRequestValido()
    {
        return new CreateAssetRequest(
            "CDB Banco X", "Banco X", "Cdb", "PreFixed", 12m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
    }

    [Fact]
    public async Task Titulo_valido_e_criado_e_retorna_201()
    {
        // Arrange
        var repository = new FixedIncomeAssetRepositoryStub();
        var useCase = new CreateAssetUseCase(repository);
        var request = CriarRequestValido();

        // Act
        var response = await useCase.ExecuteAsync(request);

        // Assert
        Assert.Equal(201, response.StatusCode);
        Assert.True(response.Success);
        Assert.NotNull(repository.AssetAdicionado);
        Assert.Equal("CDB Banco X", response.Data!.Name);
    }

    [Fact]
    public async Task AssetType_invalido_retorna_400()
    {
        // Arrange
        var repository = new FixedIncomeAssetRepositoryStub();
        var useCase = new CreateAssetUseCase(repository);
        var request = CriarRequestValido() with { AssetType = "Inexistente" };

        // Act
        var response = await useCase.ExecuteAsync(request);

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Null(repository.AssetAdicionado);
    }

    [Fact]
    public async Task IndexType_invalido_retorna_400()
    {
        // Arrange
        var repository = new FixedIncomeAssetRepositoryStub();
        var useCase = new CreateAssetUseCase(repository);
        var request = CriarRequestValido() with { IndexType = "Inexistente" };

        // Act
        var response = await useCase.ExecuteAsync(request);

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Null(repository.AssetAdicionado);
    }

    [Fact]
    public async Task Violacao_de_invariante_do_dominio_retorna_400()
    {
        // Arrange
        var repository = new FixedIncomeAssetRepositoryStub();
        var useCase = new CreateAssetUseCase(repository);
        var request = CriarRequestValido() with { Rate = 0m };

        // Act
        var response = await useCase.ExecuteAsync(request);

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Null(repository.AssetAdicionado);
    }

    private sealed class FixedIncomeAssetRepositoryStub : IFixedIncomeAssetRepository
    {
        public FixedIncomeAsset? AssetAdicionado { get; private set; }

        public Task<IEnumerable<FixedIncomeAsset>> GetAllAsync() =>
            Task.FromResult(Enumerable.Empty<FixedIncomeAsset>());

        public Task<FixedIncomeAsset?> GetByIdAsync(Guid id) => Task.FromResult<FixedIncomeAsset?>(null);

        public Task AddAsync(FixedIncomeAsset asset)
        {
            AssetAdicionado = asset;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(FixedIncomeAsset asset) => Task.CompletedTask;

        public Task DeleteAsync(FixedIncomeAsset asset) => Task.CompletedTask;
    }
}
