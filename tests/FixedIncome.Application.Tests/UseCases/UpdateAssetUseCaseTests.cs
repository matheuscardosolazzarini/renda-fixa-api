using FixedIncome.Application.DTOs;
using FixedIncome.Application.Repositories;
using FixedIncome.Application.UseCases;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Application.Tests.UseCases;

public class UpdateAssetUseCaseTests
{
    private static FixedIncomeAsset CriarTitulo()
    {
        return new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 12m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
    }

    [Fact]
    public async Task Titulo_sem_aportes_permite_alterar_as_datas_e_retorna_200()
    {
        // Arrange
        var asset = CriarTitulo();
        var assetRepository = new FixedIncomeAssetRepositoryStub(asset);
        var positionRepository = new PositionRepositoryStub(Enumerable.Empty<Position>());
        var useCase = new UpdateAssetUseCase(assetRepository, positionRepository);
        var request = new UpdateAssetRequest(
            "Novo Nome", "Novo Emissor", 15m, new DateOnly(2025, 2, 1), new DateOnly(2028, 1, 1));

        // Act
        var response = await useCase.ExecuteAsync(asset.Id, request);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Novo Nome", response.Data!.Name);
        Assert.NotNull(assetRepository.AssetAtualizado);
    }

    [Fact]
    public async Task Titulo_inexistente_retorna_404()
    {
        // Arrange
        var assetRepository = new FixedIncomeAssetRepositoryStub(null);
        var positionRepository = new PositionRepositoryStub(Enumerable.Empty<Position>());
        var useCase = new UpdateAssetUseCase(assetRepository, positionRepository);
        var request = new UpdateAssetRequest(
            "Novo Nome", "Novo Emissor", 15m, new DateOnly(2025, 2, 1), new DateOnly(2028, 1, 1));

        // Act
        var response = await useCase.ExecuteAsync(Guid.NewGuid(), request);

        // Assert
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Titulo_com_aportes_rejeita_alteracao_de_data_e_retorna_400()
    {
        // Arrange
        var asset = CriarTitulo();
        var position = new Position(asset, 1_000m, new DateOnly(2025, 6, 1));
        var assetRepository = new FixedIncomeAssetRepositoryStub(asset);
        var positionRepository = new PositionRepositoryStub(new[] { position });
        var useCase = new UpdateAssetUseCase(assetRepository, positionRepository);
        var request = new UpdateAssetRequest(
            asset.Name, asset.Issuer, asset.Rate, asset.IssueDate.AddDays(1), asset.MaturityDate);

        // Act
        var response = await useCase.ExecuteAsync(asset.Id, request);

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Null(assetRepository.AssetAtualizado);
    }

    [Fact]
    public async Task Violacao_de_invariante_do_dominio_retorna_400()
    {
        // Arrange
        var asset = CriarTitulo();
        var assetRepository = new FixedIncomeAssetRepositoryStub(asset);
        var positionRepository = new PositionRepositoryStub(Enumerable.Empty<Position>());
        var useCase = new UpdateAssetUseCase(assetRepository, positionRepository);
        var request = new UpdateAssetRequest(
            string.Empty, asset.Issuer, asset.Rate, asset.IssueDate, asset.MaturityDate);

        // Act
        var response = await useCase.ExecuteAsync(asset.Id, request);

        // Assert
        Assert.Equal(400, response.StatusCode);
    }

    // B-01: rate numeric(18,6) aceita até 999999999999,999999.
    [Fact]
    public async Task Taxa_no_ultimo_valor_que_cabe_na_coluna_atualiza_e_retorna_200()
    {
        // Arrange
        var asset = CriarTitulo();
        var assetRepository = new FixedIncomeAssetRepositoryStub(asset);
        var useCase = new UpdateAssetUseCase(assetRepository, new PositionRepositoryStub(Enumerable.Empty<Position>()));
        var request = new UpdateAssetRequest(
            asset.Name, asset.Issuer, 999999999999.999999m, asset.IssueDate, asset.MaturityDate);

        // Act
        var response = await useCase.ExecuteAsync(asset.Id, request);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(999999999999.999999m, response.Data!.Rate);
    }

    [Theory]
    [InlineData("1000000000000")]
    [InlineData("1000000000000000")]
    public async Task Taxa_acima_do_limite_da_coluna_retorna_400_e_nao_grava(string rate)
    {
        // Arrange
        var asset = CriarTitulo();
        var assetRepository = new FixedIncomeAssetRepositoryStub(asset);
        var useCase = new UpdateAssetUseCase(assetRepository, new PositionRepositoryStub(Enumerable.Empty<Position>()));
        var request = new UpdateAssetRequest(
            asset.Name, asset.Issuer, decimal.Parse(rate), asset.IssueDate, asset.MaturityDate);

        // Act
        var response = await useCase.ExecuteAsync(asset.Id, request);

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Equal("Rate deve ser menor que 1000000000000.", response.Message);
        Assert.Null(assetRepository.AssetAtualizado);
    }

    // B-07
    [Fact]
    public async Task Taxa_com_mais_de_seis_casas_decimais_retorna_400_e_nao_grava()
    {
        // Arrange
        var asset = CriarTitulo();
        var assetRepository = new FixedIncomeAssetRepositoryStub(asset);
        var useCase = new UpdateAssetUseCase(assetRepository, new PositionRepositoryStub(Enumerable.Empty<Position>()));
        var request = new UpdateAssetRequest(
            asset.Name, asset.Issuer, 12.1234567m, asset.IssueDate, asset.MaturityDate);

        // Act
        var response = await useCase.ExecuteAsync(asset.Id, request);

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Equal("Rate deve ter no máximo 6 casas decimais.", response.Message);
        Assert.Null(assetRepository.AssetAtualizado);
    }

    private sealed class FixedIncomeAssetRepositoryStub : IFixedIncomeAssetRepository
    {
        private readonly FixedIncomeAsset? _asset;

        public FixedIncomeAssetRepositoryStub(FixedIncomeAsset? asset)
        {
            _asset = asset;
        }

        public FixedIncomeAsset? AssetAtualizado { get; private set; }

        public Task<IEnumerable<FixedIncomeAsset>> GetAllAsync() =>
            Task.FromResult(Enumerable.Empty<FixedIncomeAsset>());

        public Task<FixedIncomeAsset?> GetByIdAsync(Guid id) => Task.FromResult(_asset);

        public Task AddAsync(FixedIncomeAsset asset) => Task.CompletedTask;

        public Task UpdateAsync(FixedIncomeAsset asset)
        {
            AssetAtualizado = asset;
            return Task.CompletedTask;
        }

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
