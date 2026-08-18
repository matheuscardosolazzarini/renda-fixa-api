using FixedIncome.Application.Repositories;
using FixedIncome.Application.UseCases;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Application.Tests.UseCases;

public class GetAssetByIdUseCaseTests
{
    [Fact]
    public async Task Titulo_existente_e_retornado_com_200()
    {
        // Arrange
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 12m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var repository = new FixedIncomeAssetRepositoryStub(asset);
        var useCase = new GetAssetByIdUseCase(repository);

        // Act
        var response = await useCase.ExecuteAsync(asset.Id);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(asset.Id, response.Data!.Id);
    }

    [Fact]
    public async Task Titulo_inexistente_retorna_404()
    {
        // Arrange
        var repository = new FixedIncomeAssetRepositoryStub(null);
        var useCase = new GetAssetByIdUseCase(repository);

        // Act
        var response = await useCase.ExecuteAsync(Guid.NewGuid());

        // Assert
        Assert.Equal(404, response.StatusCode);
        Assert.Null(response.Data);
    }

    private sealed class FixedIncomeAssetRepositoryStub : IFixedIncomeAssetRepository
    {
        private readonly FixedIncomeAsset? _asset;

        public FixedIncomeAssetRepositoryStub(FixedIncomeAsset? asset)
        {
            _asset = asset;
        }

        public Task<IEnumerable<FixedIncomeAsset>> GetAllAsync() =>
            Task.FromResult(Enumerable.Empty<FixedIncomeAsset>());

        public Task<FixedIncomeAsset?> GetByIdAsync(Guid id) => Task.FromResult(_asset);

        public Task AddAsync(FixedIncomeAsset asset) => Task.CompletedTask;

        public Task UpdateAsync(FixedIncomeAsset asset) => Task.CompletedTask;

        public Task DeleteAsync(FixedIncomeAsset asset) => Task.CompletedTask;
    }
}
