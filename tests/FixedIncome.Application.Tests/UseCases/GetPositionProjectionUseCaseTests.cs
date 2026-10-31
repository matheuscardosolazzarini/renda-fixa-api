using FixedIncome.Application.Repositories;
using FixedIncome.Application.Services;
using FixedIncome.Application.Tests.TestData;
using FixedIncome.Application.UseCases;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Application.Tests.UseCases;

public class GetPositionProjectionUseCaseTests
{
    // Relógio bem distante das datas dos testes: se o caso de uso usasse a data de hoje onde deve
    // usar a última cotação de CDI, os valores esperados não bateriam.
    private static readonly DateTimeOffset Now = new(2025, 12, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Projecao_valida_calcula_valores_liquidos_e_retorna_200()
    {
        // Arrange
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 1, 1));
        var useCase = CriarCaso(position, asset);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2026, 1, 1));

        // Assert: caso 1.3 da spec (2025 tem 252 dias úteis, então o fator é 1,10 exato).
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(365, response.Data!.ElapsedDays);
        Assert.Equal(252, response.Data!.BusinessDays);
        Assert.Equal(1_100.00m, response.Data!.GrossAmount, precision: 2);
        Assert.Equal(100.00m, response.Data!.GrossYield, precision: 2);
        Assert.Equal(17.5m, response.Data!.TaxRate, precision: 1);
        Assert.Equal(17.50m, response.Data!.TaxAmount, precision: 2);
        Assert.Equal(1_082.50m, response.Data!.NetAmount, precision: 2);
    }

    [Fact]
    public async Task Projecao_valida_devolve_os_campos_de_iof_e_estimativa()
    {
        // Arrange
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 1, 1));
        var useCase = CriarCaso(position, asset);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2026, 1, 1));

        // Assert: 365 dias corridos não pagam IOF; título pré-fixado nunca é estimado.
        Assert.Equal(0m, response.Data!.IofRate);
        Assert.Equal(0m, response.Data!.IofAmount);
        Assert.False(response.Data!.IsEstimated);
    }

    [Fact]
    public async Task Projecao_com_prazo_curto_devolve_iof_antes_do_ir()
    {
        // Arrange: PreFixed 10% a.a., 2025-04-01 a 2025-04-22. Derivação do esperado:
        // - 13 dias úteis (18/04 e 21/04 são feriados), fator 1,10^(13/252) = 1,0049289,
        //   bruto 1.000 x 1,0049289 = 1.004,93 e rendimento 4,93;
        // - 21 dias corridos: IOF de 30% sobre o rendimento = 1,479, arredondado para 1,48;
        // - IR de 22,5% sobre o rendimento menos o IOF: 22,5% de (4,93 - 1,48 = 3,45) = 0,77625,
        //   arredondado para 0,78; líquido 1.004,93 - 1,48 - 0,78 = 1.002,67.
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(position, asset);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(21, response.Data!.ElapsedDays);
        Assert.Equal(13, response.Data!.BusinessDays);
        Assert.Equal(1_004.93m, response.Data!.GrossAmount);
        Assert.Equal(30m, response.Data!.IofRate);
        Assert.Equal(1.48m, response.Data!.IofAmount);
        Assert.Equal(0.78m, response.Data!.TaxAmount);
        Assert.Equal(1_002.67m, response.Data!.NetAmount);
    }

    [Fact]
    public async Task Posicao_inexistente_retorna_404()
    {
        // Arrange
        var useCase = CriarCaso(position: null, asset: null);

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
        var useCase = CriarCaso(position, asset: null);

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
        var useCase = CriarCaso(position, asset);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2025, 1, 1));

        // Assert
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Data_de_referencia_fora_da_cobertura_do_calendario_retorna_400()
    {
        // Arrange: o calendário de teste cobre até 2030-12-31 e o título vence em 2035.
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2035, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 1, 1));
        var useCase = CriarCaso(position, asset);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2031, 6, 1));

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("cobertura do calendário", response.Message);
    }

    [Fact]
    public async Task Cotacao_de_cdi_ausente_retorna_400_com_a_primeira_data_faltante()
    {
        // Arrange: aporte numa terça (2025-04-01); só há CDI de 01/04. Faltam 02/04 e 03/04.
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.Cdi, 100m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(position, asset, new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 1), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2025, 4, 4));

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("2025-04-02", response.Message);
    }

    [Fact]
    public async Task Data_de_referencia_ausente_usa_o_dia_util_seguinte_a_ultima_cotacao_de_cdi()
    {
        // Arrange: a última cotação de CDI é quinta, 2025-04-17; 18/04 (Paixão), 19 e 20/04 (fim de
        // semana) e 21/04 (Tiradentes) não são úteis, então a data padrão é terça, 2025-04-22.
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(
            position, asset, new TimeProviderStub(Now), new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(position.Id, referenceDate: null);

        // Assert: mesmos valores do caso de 13 dias úteis e 21 dias corridos (ver teste de IOF acima).
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(21, response.Data!.ElapsedDays);
        Assert.Equal(13, response.Data!.BusinessDays);
        Assert.Equal(1_004.93m, response.Data!.GrossAmount);
        Assert.Equal(1_002.67m, response.Data!.NetAmount);
    }

    [Fact]
    public async Task Data_de_referencia_ausente_com_ultima_cotacao_de_cdi_na_sexta_usa_a_segunda()
    {
        // Arrange: sexta 2025-04-04 -> segunda 2025-04-07. Aporte em 2025-04-01: 6 dias corridos
        // e 4 dias úteis (02, 03, 04 e 07/04).
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(
            position, asset, new TimeProviderStub(Now), new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 4), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(position.Id, referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(6, response.Data!.ElapsedDays);
        Assert.Equal(4, response.Data!.BusinessDays);
    }

    [Fact]
    public async Task Data_de_referencia_ausente_e_sem_nenhuma_cotacao_de_cdi_usa_a_data_de_hoje_do_relogio()
    {
        // Arrange: sem CDI importado vale a data do TimeProvider, fixada em 2025-04-22.
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var clock = new TimeProviderStub(new DateTimeOffset(2025, 4, 22, 12, 0, 0, TimeSpan.Zero));
        var useCase = CriarCaso(position, asset, clock);

        // Act
        var response = await useCase.ExecuteAsync(position.Id, referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(21, response.Data!.ElapsedDays);
        Assert.Equal(1_004.93m, response.Data!.GrossAmount);
    }

    [Fact]
    public async Task Data_de_referencia_explicita_nao_e_alterada_pela_ultima_cotacao_de_cdi()
    {
        // Arrange: há CDI até 2025-04-17, mas a data informada (2026-01-01) prevalece.
        var asset = new FixedIncomeAsset(
            "CDB Banco X", "Banco X", AssetType.Cdb, IndexType.PreFixed, 10m,
            new DateOnly(2025, 1, 1), new DateOnly(2027, 1, 1));
        var position = new Position(asset, 1_000m, new DateOnly(2025, 1, 1));
        var useCase = CriarCaso(
            position, asset, new TimeProviderStub(Now), new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(position.Id, new DateOnly(2026, 1, 1));

        // Assert: caso 1.3 da spec.
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(365, response.Data!.ElapsedDays);
        Assert.Equal(1_100.00m, response.Data!.GrossAmount, precision: 2);
    }

    private static GetPositionProjectionUseCase CriarCaso(
        Position? position, FixedIncomeAsset? asset, params IndexRate[] rates)
    {
        return CriarCaso(position, asset, new TimeProviderStub(Now), rates);
    }

    private static GetPositionProjectionUseCase CriarCaso(
        Position? position, FixedIncomeAsset? asset, TimeProvider clock, params IndexRate[] rates)
    {
        var loader = new MarketDataLoader(
            new HolidayRepositoryStub(TestMarket.Holidays()), new IndexRateRepositoryStub(rates));

        return new GetPositionProjectionUseCase(
            new PositionRepositoryStub(position), new FixedIncomeAssetRepositoryStub(asset), loader, clock);
    }

    private sealed class TimeProviderStub : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public TimeProviderStub(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;
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

    private sealed class IndexRateRepositoryStub : IIndexRateRepository
    {
        private readonly IReadOnlyList<IndexRate> _rates;

        public IndexRateRepositoryStub(IReadOnlyList<IndexRate> rates)
        {
            _rates = rates;
        }

        public Task<IEnumerable<IndexRate>> GetByIndexTypeAndPeriodAsync(IndexType indexType, DateOnly from, DateOnly to) =>
            Task.FromResult(_rates
                .Where(r => r.IndexType == indexType && r.ReferenceDate >= from && r.ReferenceDate <= to)
                .AsEnumerable());

        public Task<IndexRate?> GetLastBeforeAsync(IndexType indexType, DateOnly date) =>
            Task.FromResult(_rates
                .Where(r => r.IndexType == indexType && r.ReferenceDate < date)
                .OrderByDescending(r => r.ReferenceDate)
                .FirstOrDefault());

        public Task<DateOnly?> GetLastReferenceDateAsync(IndexType indexType) =>
            Task.FromResult(_rates.Where(r => r.IndexType == indexType).Select(r => (DateOnly?)r.ReferenceDate).Max());

        public Task UpsertAsync(IEnumerable<IndexRate> rates) => Task.CompletedTask;
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
