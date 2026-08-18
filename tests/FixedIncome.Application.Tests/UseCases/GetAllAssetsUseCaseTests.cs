using FixedIncome.Application.Repositories;
using FixedIncome.Application.UseCases;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Application.Tests.UseCases;

public class GetAllAssetsUseCaseTests
{
    [Fact]
    public async Task Lista_de_titulos_e_convertida_para_resposta()
    {
        // Arrange
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 12m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var repository = new FixedIncomeAssetRepositoryStub(new[] { asset });
        var useCase = new GetAllAssetsUseCase(repository);

        // Act
        var response = await useCase.ExecuteAsync();

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Single(response.Data!);
        Assert.Equal(asset.Id, response.Data!.Single().Id);
    }

    [Fact]
    public async Task Ausencia_de_titulos_retorna_colecao_vazia()
    {
        // Arrange
        var repository = new FixedIncomeAssetRepositoryStub(Array.Empty<FixedIncomeAsset>());
        var useCase = new GetAllAssetsUseCase(repository);

        // Act
        var response = await useCase.ExecuteAsync();

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Empty(response.Data!);
    }

    private sealed class FixedIncomeAssetRepositoryStub : IFixedIncomeAssetRepository
    {
        private readonly IEnumerable<FixedIncomeAsset> _assets;

        public FixedIncomeAssetRepositoryStub(IEnumerable<FixedIncomeAsset> assets)
        {
            _assets = assets;
        }

        public Task<IEnumerable<FixedIncomeAsset>> GetAllAsync() => Task.FromResult(_assets);

        public Task<FixedIncomeAsset?> GetByIdAsync(Guid id) => Task.FromResult<FixedIncomeAsset?>(null);

        public Task AddAsync(FixedIncomeAsset asset) => Task.CompletedTask;

        public Task UpdateAsync(FixedIncomeAsset asset) => Task.CompletedTask;

        public Task DeleteAsync(FixedIncomeAsset asset) => Task.CompletedTask;
    }
}
