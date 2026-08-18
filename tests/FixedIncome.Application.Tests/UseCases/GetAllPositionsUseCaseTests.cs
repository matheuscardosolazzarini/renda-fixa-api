using FixedIncome.Application.Repositories;
using FixedIncome.Application.UseCases;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Application.Tests.UseCases;

public class GetAllPositionsUseCaseTests
{
    [Fact]
    public async Task Lista_de_aportes_e_convertida_para_resposta()
    {
        // Arrange
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 12m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 6, 1));
        var repository = new PositionRepositoryStub(new[] { position });
        var useCase = new GetAllPositionsUseCase(repository);

        // Act
        var response = await useCase.ExecuteAsync();

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Single(response.Data!);
        Assert.Equal(position.Id, response.Data!.Single().Id);
    }

    [Fact]
    public async Task Ausencia_de_aportes_retorna_colecao_vazia()
    {
        // Arrange
        var repository = new PositionRepositoryStub(Array.Empty<Position>());
        var useCase = new GetAllPositionsUseCase(repository);

        // Act
        var response = await useCase.ExecuteAsync();

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Empty(response.Data!);
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
