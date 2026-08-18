using FixedIncome.Application.Common;
using FixedIncome.Application.Repositories;
using FixedIncome.Application.UseCases;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Application.Tests.UseCases;

public class GetPositionProjectionUseCaseTests
{
    [Fact]
    public async Task Projecao_valida_calcula_valores_liquidos_e_retorna_200()
    {
        // Arrange
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 1, 1));
        var positionRepository = new PositionRepositoryStub(position);
        var assetRepository = new FixedIncomeAssetRepositoryStub(asset);
        var indexRatesOptions = new IndexRatesOptions { Cdi = 13m, Ipca = 4m };
        var useCase = new GetPositionProjectionUseCase(positionRepository, assetRepository, indexRatesOptions);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2026, 1, 1));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(365, response.Data!.ElapsedDays);
        Assert.Equal(1_100.00m, response.Data!.GrossAmount, precision: 2);
        Assert.Equal(100.00m, response.Data!.GrossYield, precision: 2);
        Assert.Equal(17.5m, response.Data!.TaxRate, precision: 1);
        Assert.Equal(17.50m, response.Data!.TaxAmount, precision: 2);
        Assert.Equal(1_082.50m, response.Data!.NetAmount, precision: 2);
    }

    [Fact]
    public async Task Posicao_inexistente_retorna_404()
    {
        // Arrange
        var positionRepository = new PositionRepositoryStub(null);
        var assetRepository = new FixedIncomeAssetRepositoryStub(null);
        var indexRatesOptions = new IndexRatesOptions { Cdi = 13m, Ipca = 4m };
        var useCase = new GetPositionProjectionUseCase(positionRepository, assetRepository, indexRatesOptions);

        // Act
        var response = await useCase.ExecuteAsync(Guid.NewGuid(), new DateOnly(2026, 1, 1));

        // Assert
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Titulo_vinculado_a_posicao_nao_encontrado_retorna_404()
    {
        // Arrange
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 1, 1));
        var positionRepository = new PositionRepositoryStub(position);
        var assetRepository = new FixedIncomeAssetRepositoryStub(null);
        var indexRatesOptions = new IndexRatesOptions { Cdi = 13m, Ipca = 4m };
        var useCase = new GetPositionProjectionUseCase(positionRepository, assetRepository, indexRatesOptions);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2026, 1, 1));

        // Assert
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Data_de_referencia_anterior_ao_aporte_retorna_400()
    {
        // Arrange
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 6, 1));
        var positionRepository = new PositionRepositoryStub(position);
        var assetRepository = new FixedIncomeAssetRepositoryStub(asset);
        var indexRatesOptions = new IndexRatesOptions { Cdi = 13m, Ipca = 4m };
        var useCase = new GetPositionProjectionUseCase(positionRepository, assetRepository, indexRatesOptions);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2025, 1, 1));

        // Assert
        Assert.Equal(400, response.StatusCode);
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
        private readonly Position? _position;

        public PositionRepositoryStub(Position? position)
        {
            _position = position;
        }

        public Task<IEnumerable<Position>> GetAllAsync() => Task.FromResult(Enumerable.Empty<Position>());

        public Task<Position?> GetByIdAsync(Guid id) => Task.FromResult(_position);

        public Task<IEnumerable<Position>> GetByAssetIdAsync(Guid assetId) =>
            Task.FromResult(Enumerable.Empty<Position>());

        public Task AddAsync(Position position) => Task.CompletedTask;
    }
}
