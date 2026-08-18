using FixedIncome.Application.DTOs;
using FixedIncome.Application.Repositories;
using FixedIncome.Application.UseCases;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Application.Tests.UseCases;

public class CreatePositionUseCaseTests
{
    private static FixedIncomeAsset CriarTitulo()
    {
        return new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 12m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
    }

    [Fact]
    public async Task Aporte_valido_e_criado_e_retorna_201()
    {
        // Arrange
        var asset = CriarTitulo();
        var positionRepository = new PositionRepositoryStub();
        var assetRepository = new FixedIncomeAssetRepositoryStub(asset);
        var useCase = new CreatePositionUseCase(positionRepository, assetRepository);
        var request = new CreatePositionRequest(asset.Id, 1_000m, new DateOnly(2025, 6, 1));

        // Act
        var response = await useCase.ExecuteAsync(request);

        // Assert
        Assert.Equal(201, response.StatusCode);
        Assert.NotNull(positionRepository.PositionAdicionada);
        Assert.Equal(asset.Id, response.Data!.AssetId);
    }

    [Fact]
    public async Task Titulo_inexistente_retorna_404()
    {
        // Arrange
        var positionRepository = new PositionRepositoryStub();
        var assetRepository = new FixedIncomeAssetRepositoryStub(null);
        var useCase = new CreatePositionUseCase(positionRepository, assetRepository);
        var request = new CreatePositionRequest(Guid.NewGuid(), 1_000m, new DateOnly(2025, 6, 1));

        // Act
        var response = await useCase.ExecuteAsync(request);

        // Assert
        Assert.Equal(404, response.StatusCode);
        Assert.Null(positionRepository.PositionAdicionada);
    }

    [Fact]
    public async Task Aporte_fora_da_vigencia_do_titulo_retorna_400()
    {
        // Arrange
        var asset = CriarTitulo();
        var positionRepository = new PositionRepositoryStub();
        var assetRepository = new FixedIncomeAssetRepositoryStub(asset);
        var useCase = new CreatePositionUseCase(positionRepository, assetRepository);
        var request = new CreatePositionRequest(asset.Id, 1_000m, asset.IssueDate.AddDays(-1));

        // Act
        var response = await useCase.ExecuteAsync(request);

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Null(positionRepository.PositionAdicionada);
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

    private sealed class PositionRepositoryStub : IPositionRepository
    {
        public Position? PositionAdicionada { get; private set; }

        public Task<IEnumerable<Position>> GetAllAsync() => Task.FromResult(Enumerable.Empty<Position>());

        public Task<Position?> GetByIdAsync(Guid id) => Task.FromResult<Position?>(null);

        public Task<IEnumerable<Position>> GetByAssetIdAsync(Guid assetId) =>
            Task.FromResult(Enumerable.Empty<Position>());

        public Task AddAsync(Position position)
        {
            PositionAdicionada = position;
            return Task.CompletedTask;
        }
    }
}
