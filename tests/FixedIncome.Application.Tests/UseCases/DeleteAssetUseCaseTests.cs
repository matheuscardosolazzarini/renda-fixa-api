using FixedIncome.Application.Repositories;
using FixedIncome.Application.UseCases;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Application.Tests.UseCases;

public class DeleteAssetUseCaseTests
{
    private static FixedIncomeAsset CriarTitulo()
    {
        return new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 12m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
    }

    [Fact]
    public async Task Titulo_sem_aportes_e_removido_e_retorna_204()
    {
        // Arrange
        var asset = CriarTitulo();
        var assetRepository = new FixedIncomeAssetRepositoryStub(asset);
        var positionRepository = new PositionRepositoryStub(Enumerable.Empty<Position>());
        var useCase = new DeleteAssetUseCase(assetRepository, positionRepository);

        // Act
        var response = await useCase.ExecuteAsync(asset.Id);

        // Assert
        Assert.Equal(204, response.StatusCode);
        Assert.NotNull(assetRepository.AssetRemovido);
    }

    [Fact]
    public async Task Titulo_inexistente_retorna_404()
    {
        // Arrange
        var assetRepository = new FixedIncomeAssetRepositoryStub(null);
        var positionRepository = new PositionRepositoryStub(Enumerable.Empty<Position>());
        var useCase = new DeleteAssetUseCase(assetRepository, positionRepository);

        // Act
        var response = await useCase.ExecuteAsync(Guid.NewGuid());

        // Assert
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Titulo_com_aportes_nao_e_removido_e_retorna_400()
    {
        // Arrange
        var asset = CriarTitulo();
        var position = new Position(asset, 1_000m, new DateOnly(2025, 6, 1));
        var assetRepository = new FixedIncomeAssetRepositoryStub(asset);
        var positionRepository = new PositionRepositoryStub(new[] { position });
        var useCase = new DeleteAssetUseCase(assetRepository, positionRepository);

        // Act
        var response = await useCase.ExecuteAsync(asset.Id);

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Null(assetRepository.AssetRemovido);
    }

    private sealed class FixedIncomeAssetRepositoryStub : IFixedIncomeAssetRepository
    {
        private readonly FixedIncomeAsset? _asset;

        public FixedIncomeAssetRepositoryStub(FixedIncomeAsset? asset)
        {
            _asset = asset;
        }

        public FixedIncomeAsset? AssetRemovido { get; private set; }

        public Task<IEnumerable<FixedIncomeAsset>> GetAllAsync() =>
            Task.FromResult(Enumerable.Empty<FixedIncomeAsset>());

        public Task<FixedIncomeAsset?> GetByIdAsync(Guid id) => Task.FromResult(_asset);

        public Task AddAsync(FixedIncomeAsset asset) => Task.CompletedTask;

        public Task UpdateAsync(FixedIncomeAsset asset) => Task.CompletedTask;

        public Task DeleteAsync(FixedIncomeAsset asset)
        {
            AssetRemovido = asset;
            return Task.CompletedTask;
        }
    }

    private sealed class PositionRepositoryStub : IPositionRepository
    {
        private readonly IEnumerable<Position> _positions;

        public PositionRepositoryStub(IEnumerable<Position> positions)
        {
            _positions = positions;
        }

        public Task<IEnumerable<Position>> GetAllAsync() => Task.FromResult(_positions);

        public Task<Position?> GetByIdAsync(Guid id) => Task.FromResult<Position?>(null);

        public Task<IEnumerable<Position>> GetByAssetIdAsync(Guid assetId) => Task.FromResult(_positions);

        public Task AddAsync(Position position) => Task.CompletedTask;
    }
}
