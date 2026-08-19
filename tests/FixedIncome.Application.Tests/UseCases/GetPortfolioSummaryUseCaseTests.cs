using FixedIncome.Application.Common;
using FixedIncome.Application.Repositories;
using FixedIncome.Application.UseCases;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Application.Tests.UseCases;

public class GetPortfolioSummaryUseCaseTests
{
    private static readonly IndexRatesOptions IndexRatesOptions = new() { Cdi = 13m, Ipca = 4m };

    [Fact]
    public async Task Caminho_de_sucesso_com_tipos_diferentes_retorna_200_com_totais_por_tipo()
    {
        // Arrange
        var cdb = CriarAsset(AssetType.Cdb);
        var lci = CriarAsset(AssetType.Lci);
        var positionCdb = new Position(cdb, 1_000m, cdb.IssueDate);
        var positionLci = new Position(lci, 500m, lci.IssueDate);
        var positionRepository = new PositionRepositoryStub(new[] { positionCdb, positionLci });
        var assetRepository = new FixedIncomeAssetRepositoryStub(new[] { cdb, lci });
        var useCase = new GetPortfolioSummaryUseCase(positionRepository, assetRepository, IndexRatesOptions);

        // Act
        var response = await useCase.ExecuteAsync(cdb.IssueDate.AddDays(365));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(2, response.Data!.PositionCount);
        Assert.Equal(2, response.Data!.ByAssetType.Count);
    }

    [Fact]
    public async Task Carteira_vazia_retorna_200_com_resumo_zerado()
    {
        // Arrange
        var positionRepository = new PositionRepositoryStub(Array.Empty<Position>());
        var assetRepository = new FixedIncomeAssetRepositoryStub(Array.Empty<FixedIncomeAsset>());
        var useCase = new GetPortfolioSummaryUseCase(positionRepository, assetRepository, IndexRatesOptions);

        // Act
        var response = await useCase.ExecuteAsync(referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(0, response.Data!.PositionCount);
        Assert.Equal(0m, response.Data!.TotalNetAmount);
        Assert.Empty(response.Data!.ByAssetType);
    }

    [Fact]
    public async Task Data_de_referencia_ausente_usa_a_data_atual()
    {
        // Arrange
        var asset = CriarAsset(AssetType.Cdb, issueDate: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10));
        var position = new Position(asset, 1_000m, asset.IssueDate);
        var positionRepository = new PositionRepositoryStub(new[] { position });
        var assetRepository = new FixedIncomeAssetRepositoryStub(new[] { asset });
        var useCase = new GetPortfolioSummaryUseCase(positionRepository, assetRepository, IndexRatesOptions);

        var esperado = position.Project(
            asset, DateOnly.FromDateTime(DateTime.UtcNow), IndexRatesOptions.ToIndexRates());

        // Act
        var response = await useCase.ExecuteAsync(referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(esperado.NetAmount, response.Data!.TotalNetAmount);
    }

    [Fact]
    public async Task Titulos_sao_carregados_uma_unica_vez_independente_do_numero_de_posicoes()
    {
        // Arrange
        var asset = CriarAsset(AssetType.Cdb);
        var positions = Enumerable.Range(0, 5)
            .Select(_ => new Position(asset, 1_000m, asset.IssueDate))
            .ToArray();
        var positionRepository = new PositionRepositoryStub(positions);
        var assetRepository = new FixedIncomeAssetRepositoryStub(new[] { asset });
        var useCase = new GetPortfolioSummaryUseCase(positionRepository, assetRepository, IndexRatesOptions);

        // Act
        var response = await useCase.ExecuteAsync(asset.IssueDate.AddDays(30));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(1, assetRepository.GetAllCallCount);
    }

    private static FixedIncomeAsset CriarAsset(AssetType assetType, DateOnly? issueDate = null)
    {
        var start = issueDate ?? new DateOnly(2025, 1, 1);
        return new FixedIncomeAsset(
            "Título Teste", "Emissor Teste", assetType, IndexType.PreFixed, 10m,
            start, start.AddYears(2));
    }

    private sealed class FixedIncomeAssetRepositoryStub : IFixedIncomeAssetRepository
    {
        private readonly IEnumerable<FixedIncomeAsset> _assets;

        public int GetAllCallCount { get; private set; }

        public FixedIncomeAssetRepositoryStub(IEnumerable<FixedIncomeAsset> assets)
        {
            _assets = assets;
        }

        public Task<IEnumerable<FixedIncomeAsset>> GetAllAsync()
        {
            GetAllCallCount++;
            return Task.FromResult(_assets);
        }

        public Task<FixedIncomeAsset?> GetByIdAsync(Guid id) =>
            Task.FromResult(_assets.SingleOrDefault(asset => asset.Id == id));

        public Task AddAsync(FixedIncomeAsset asset) => Task.CompletedTask;

        public Task UpdateAsync(FixedIncomeAsset asset) => Task.CompletedTask;

        public Task DeleteAsync(FixedIncomeAsset asset) => Task.CompletedTask;
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
