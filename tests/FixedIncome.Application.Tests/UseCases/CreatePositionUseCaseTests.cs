using FixedIncome.Application.DTOs;
using FixedIncome.Application.Repositories;
using FixedIncome.Application.Tests.TestData;
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
        var useCase = new CreatePositionUseCase(positionRepository, assetRepository, new HolidayRepositoryStub(TestMarket.Holidays()));
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
        var useCase = new CreatePositionUseCase(positionRepository, assetRepository, new HolidayRepositoryStub(TestMarket.Holidays()));
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
        var useCase = new CreatePositionUseCase(positionRepository, assetRepository, new HolidayRepositoryStub(TestMarket.Holidays()));
        var request = new CreatePositionRequest(asset.Id, 1_000m, asset.IssueDate.AddDays(-1));

        // Act
        var response = await useCase.ExecuteAsync(request);

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Null(positionRepository.PositionAdicionada);
    }

    // B-02: calendário de 2020-01-01 a 2030-12-31, o mesmo período do seed de produção.
    private static IReadOnlyList<Holiday> Calendario2020A2030() =>
    [
        new Holiday(new DateOnly(2020, 1, 1), "Confraternização Universal"),
        new Holiday(new DateOnly(2030, 12, 25), "Natal")
    ];

    private static FixedIncomeAsset CriarTituloLongo()
    {
        return new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2019, 1, 1), new DateOnly(2032, 1, 1));
    }

    [Theory]
    [InlineData("2019-12-31")]
    [InlineData("2031-01-01")]
    [InlineData("2019-06-03")]
    [InlineData("2031-06-01")]
    public async Task Aporte_fora_do_periodo_coberto_pelo_calendario_retorna_400_com_a_data_e_o_periodo(string date)
    {
        // Arrange
        var asset = CriarTituloLongo();
        var positionRepository = new PositionRepositoryStub();
        var useCase = new CreatePositionUseCase(
            positionRepository, new FixedIncomeAssetRepositoryStub(asset), new HolidayRepositoryStub(Calendario2020A2030()));
        var request = new CreatePositionRequest(asset.Id, 1_000m, DateOnly.Parse(date));

        // Act
        var response = await useCase.ExecuteAsync(request);

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(
            $"Data de aporte {date} fora do período coberto pelo calendário (2020-01-01 a 2030-12-31).",
            response.Message);
        Assert.Null(positionRepository.PositionAdicionada);
    }

    [Theory]
    [InlineData("2020-01-01")]
    [InlineData("2030-12-31")]
    public async Task Aporte_nos_extremos_do_periodo_coberto_pelo_calendario_e_criado(string date)
    {
        // Arrange
        var asset = CriarTituloLongo();
        var positionRepository = new PositionRepositoryStub();
        var useCase = new CreatePositionUseCase(
            positionRepository, new FixedIncomeAssetRepositoryStub(asset), new HolidayRepositoryStub(Calendario2020A2030()));
        var request = new CreatePositionRequest(asset.Id, 1_000m, DateOnly.Parse(date));

        // Act
        var response = await useCase.ExecuteAsync(request);

        // Assert
        Assert.Equal(201, response.StatusCode);
        Assert.NotNull(positionRepository.PositionAdicionada);
    }

    [Fact]
    public async Task Aporte_sem_nenhum_feriado_cadastrado_retorna_400_e_nao_grava()
    {
        // Arrange
        var asset = CriarTitulo();
        var positionRepository = new PositionRepositoryStub();
        var useCase = new CreatePositionUseCase(
            positionRepository, new FixedIncomeAssetRepositoryStub(asset), new HolidayRepositoryStub([]));
        var request = new CreatePositionRequest(asset.Id, 1_000m, new DateOnly(2025, 6, 1));

        // Act
        var response = await useCase.ExecuteAsync(request);

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Null(positionRepository.PositionAdicionada);
    }

    // B-01: investedAmount numeric(18,2) aceita até 9999999999999999,99.
    [Fact]
    public async Task Aporte_no_ultimo_valor_que_cabe_na_coluna_e_criado()
    {
        // Arrange
        var asset = CriarTitulo();
        var positionRepository = new PositionRepositoryStub();
        var useCase = new CreatePositionUseCase(
            positionRepository, new FixedIncomeAssetRepositoryStub(asset), new HolidayRepositoryStub(TestMarket.Holidays()));
        var request = new CreatePositionRequest(asset.Id, 9999999999999999.99m, new DateOnly(2025, 6, 1));

        // Act
        var response = await useCase.ExecuteAsync(request);

        // Assert
        Assert.Equal(201, response.StatusCode);
        Assert.Equal(9999999999999999.99m, response.Data!.InvestedAmount);
    }

    [Fact]
    public async Task Aporte_acima_do_limite_da_coluna_retorna_400_e_nao_grava()
    {
        // Arrange
        var asset = CriarTitulo();
        var positionRepository = new PositionRepositoryStub();
        var useCase = new CreatePositionUseCase(
            positionRepository, new FixedIncomeAssetRepositoryStub(asset), new HolidayRepositoryStub(TestMarket.Holidays()));
        var request = new CreatePositionRequest(asset.Id, 10_000_000_000_000_000m, new DateOnly(2025, 6, 1));

        // Act
        var response = await useCase.ExecuteAsync(request);

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("10000000000000000", response.Message);
        Assert.Null(positionRepository.PositionAdicionada);
    }

    // B-07
    [Fact]
    public async Task Aporte_com_mais_de_duas_casas_decimais_retorna_400_e_nao_grava()
    {
        // Arrange
        var asset = CriarTitulo();
        var positionRepository = new PositionRepositoryStub();
        var useCase = new CreatePositionUseCase(
            positionRepository, new FixedIncomeAssetRepositoryStub(asset), new HolidayRepositoryStub(TestMarket.Holidays()));
        var request = new CreatePositionRequest(asset.Id, 10.999m, new DateOnly(2025, 6, 1));

        // Act
        var response = await useCase.ExecuteAsync(request);

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("2 casas decimais", response.Message);
        Assert.Null(positionRepository.PositionAdicionada);
    }

    private sealed class HolidayRepositoryStub : IHolidayRepository
    {
        private readonly IEnumerable<Holiday> _holidays;

        public HolidayRepositoryStub(IEnumerable<Holiday> holidays)
        {
            _holidays = holidays;
        }

        public Task<IEnumerable<Holiday>> GetAllAsync() => Task.FromResult(_holidays);
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
