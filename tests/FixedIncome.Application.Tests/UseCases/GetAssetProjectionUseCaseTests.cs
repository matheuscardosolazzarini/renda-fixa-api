using FixedIncome.Application.DTOs;
using FixedIncome.Application.Repositories;
using FixedIncome.Application.Services;
using FixedIncome.Application.Tests.TestData;
using FixedIncome.Application.UseCases;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Application.Tests.UseCases;

// F12, RN-14 (P4b): projeção consolidada de um título e dos seus aportes.
public class GetAssetProjectionUseCaseTests
{
    // Relógio bem distante das datas dos testes: a data padrão vem da última cotação de CDI.
    private static readonly DateTimeOffset Now = new(2025, 12, 1, 12, 0, 0, TimeSpan.Zero);

    // Caso 29 (12a do contrato para o front): dois aportes do mesmo CDB PreFixed 10% a.a., R$ 1.000
    // cada, de 2025-04-01 a 2025-04-22. Cada item: bruto 1.004,93, IOF 1,48, IR 0,78, líquido 1.002,67.
    // O IR total de 1,56 (2 x 0,78) e o líquido de 2.005,34 distinguem a RN-07 de um consolidado
    // recalculado, que daria IR 1,55 e líquido 2.005,35.
    [Fact]
    public async Task Dois_aportes_do_mesmo_cdb_somam_os_itens_ja_arredondados_e_fecham_com_os_totais()
    {
        // Arrange
        var asset = CriarAsset();
        var first = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var second = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(new[] { first, second }, new[] { asset });

        // Act
        var response = await useCase.ExecuteAsync(asset.Id, new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        var data = response.Data!;
        Assert.Equal(asset.Id, data.AssetId);
        Assert.Equal(2_000m, data.TotalInvestedAmount);
        Assert.Equal(2_009.86m, data.TotalGrossAmount);
        Assert.Equal(9.86m, data.TotalGrossYield);
        Assert.Equal(1.56m, data.TotalTaxAmount);
        Assert.Equal(2.96m, data.TotalIofAmount);
        Assert.Equal(2_005.34m, data.TotalNetAmount);
        Assert.All(data.Positions, item =>
        {
            Assert.Equal(1_004.93m, item.GrossAmount);
            Assert.Equal(1.48m, item.IofAmount);
            Assert.Equal(0.78m, item.TaxAmount);
            Assert.Equal(1_002.67m, item.NetAmount);
        });
        Assert.Equal(data.PositionCount, data.Positions.Count);
        Assert.Equal(2, data.PositionCount);
        Assert.Equal(data.TotalInvestedAmount, data.Positions.Sum(p => p.InvestedAmount));
        Assert.Equal(data.TotalGrossAmount, data.Positions.Sum(p => p.GrossAmount));
        Assert.Equal(data.TotalGrossYield, data.Positions.Sum(p => p.GrossYield));
        Assert.Equal(data.TotalTaxAmount, data.Positions.Sum(p => p.TaxAmount));
        Assert.Equal(data.TotalIofAmount, data.Positions.Sum(p => p.IofAmount));
        Assert.Equal(data.TotalNetAmount, data.Positions.Sum(p => p.NetAmount));
    }

    [Fact]
    public async Task Titulo_sem_aportes_com_data_informada_retorna_200_com_totais_zero_lista_vazia_e_a_data()
    {
        // Arrange
        var asset = CriarAsset();
        var useCase = CriarCaso(Array.Empty<Position>(), new[] { asset });

        // Act
        var response = await useCase.ExecuteAsync(asset.Id, new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        AssertTotaisZerados(response.Data!);
        Assert.Equal(new DateOnly(2025, 4, 22), response.Data!.ReferenceDate);
    }

    [Fact]
    public async Task Titulo_sem_aportes_sem_data_retorna_200_com_totais_zero_lista_vazia_e_a_data_padrao()
    {
        // Arrange: a última cotação de CDI é quinta, 2025-04-17; 18/04 e 21/04 são feriados, então a
        // data padrão é terça, 2025-04-22.
        var asset = CriarAsset();
        var useCase = CriarCaso(
            Array.Empty<Position>(), new[] { asset }, new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(asset.Id, referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        AssertTotaisZerados(response.Data!);
        Assert.Equal(new DateOnly(2025, 4, 22), response.Data!.ReferenceDate);
    }

    [Fact]
    public async Task Todos_os_aportes_posteriores_a_data_informada_retornam_200_com_totais_zero_e_lista_vazia()
    {
        // Arrange
        var asset = CriarAsset();
        var position = new Position(asset, 1_000m, new DateOnly(2025, 5, 5));
        var useCase = CriarCaso(new[] { position }, new[] { asset });

        // Act
        var response = await useCase.ExecuteAsync(asset.Id, new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        AssertTotaisZerados(response.Data!);
        Assert.Equal(new DateOnly(2025, 4, 22), response.Data!.ReferenceDate);
    }

    [Fact]
    public async Task Titulo_inexistente_retorna_404_mesmo_com_o_calendario_vazio()
    {
        // Arrange: sem feriados o carregamento de mercado lançaria DomainException (400). O 404 precisa
        // vencer: a existência do título é verificada antes do calendário.
        var id = Guid.NewGuid();
        var useCase = new GetAssetProjectionUseCase(
            new FixedIncomeAssetRepositoryStub(Array.Empty<FixedIncomeAsset>()),
            new PositionRepositoryStub(Array.Empty<Position>()),
            new PositionProjector(
                new MarketDataLoader(
                    new HolidayRepositoryStub(Array.Empty<Holiday>()),
                    new IndexRateRepositoryStub(Array.Empty<IndexRate>()),
                    new TimeProviderStub(Now)),
                new TimeProviderStub(Now)));

        // Act
        var response = await useCase.ExecuteAsync(id, new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(404, response.StatusCode);
        Assert.Equal($"Título com id {id} não encontrado.", response.Message);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task Data_informada_exclui_o_aporte_posterior_a_ela()
    {
        // Arrange
        var asset = CriarAsset();
        var included = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var excluded = new Position(asset, 500m, new DateOnly(2025, 5, 5));
        var useCase = CriarCaso(new[] { excluded, included }, new[] { asset });

        // Act
        var response = await useCase.ExecuteAsync(asset.Id, new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(1, response.Data!.PositionCount);
        Assert.Equal(included.Id, Assert.Single(response.Data!.Positions).PositionId);
        Assert.Equal(1_000m, response.Data!.TotalInvestedAmount);
    }

    [Fact]
    public async Task Sem_data_o_aporte_posterior_a_data_padrao_e_projetado_na_propria_data_do_aporte()
    {
        // Arrange: data padrão 2025-04-22; o aporte de 2025-05-05 rende zero na própria data.
        var asset = CriarAsset();
        var onDefaultDate = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var afterDefaultDate = new Position(asset, 500m, new DateOnly(2025, 5, 5));
        var useCase = CriarCaso(
            new[] { afterDefaultDate, onDefaultDate },
            new[] { asset },
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(asset.Id, referenceDate: null);

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(new DateOnly(2025, 4, 22), response.Data!.ReferenceDate);
        Assert.Equal(2, response.Data!.PositionCount);
        var late = Assert.Single(response.Data!.Positions, p => p.PositionId == afterDefaultDate.Id);
        Assert.Equal(new DateOnly(2025, 5, 5), late.ReferenceDate);
        Assert.Equal(500m, late.GrossAmount);
        Assert.Equal(0m, late.GrossYield);
        var early = Assert.Single(response.Data!.Positions, p => p.PositionId == onDefaultDate.Id);
        Assert.Equal(new DateOnly(2025, 4, 22), early.ReferenceDate);
        Assert.Equal(1_004.93m, early.GrossAmount);
    }

    [Fact]
    public async Task So_traz_os_aportes_do_titulo_consultado_na_ordem_do_repositorio()
    {
        // Arrange: o repositório devolve o mais recente primeiro (D-12); o caso de uso mantém a ordem.
        var asset = CriarAsset(name: "CDB Alvo");
        var other = CriarAsset(name: "CDB Outro");
        var recent = new Position(asset, 1_000m, new DateOnly(2025, 4, 10));
        var old = new Position(asset, 2_000m, new DateOnly(2025, 4, 1));
        var otherPosition = new Position(other, 3_000m, new DateOnly(2025, 4, 5));
        var useCase = CriarCaso(new[] { recent, otherPosition, old }, new[] { asset, other });

        // Act
        var response = await useCase.ExecuteAsync(asset.Id, new DateOnly(2025, 4, 22));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(
            new[] { recent.Id, old.Id },
            response.Data!.Positions.Select(p => p.PositionId));
        Assert.All(response.Data!.Positions, item => Assert.Equal(asset.Id, item.AssetId));
        Assert.Equal(3_000m, response.Data!.TotalInvestedAmount);
    }

    [Fact]
    public async Task Cotacao_de_cdi_ausente_retorna_400_citando_a_posicao_o_titulo_e_o_aviso_da_importacao()
    {
        // Arrange: terça 2025-04-01 a sexta 2025-04-04; só há CDI de 01/04, falta 02/04.
        var asset = CriarAsset(indexType: IndexType.Cdi, rate: 100m);
        var position = new Position(asset, 1_000m, new DateOnly(2025, 4, 1));
        var useCase = CriarCaso(
            new[] { position }, new[] { asset }, new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 1), 0.05m));

        // Act
        var response = await useCase.ExecuteAsync(asset.Id, new DateOnly(2025, 4, 4));

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(
            $"Posição {position.Id} (título 'Título Teste'): Cotação de CDI ausente para 2025-04-02. "
            + "A importação de cotações pode estar desligada (IndexRatesImport:Enabled) ou ainda não ter rodado.",
            response.Message);
    }

    private static void AssertTotaisZerados(AssetProjectionResponse data)
    {
        Assert.Equal(0, data.PositionCount);
        Assert.Equal(0m, data.TotalInvestedAmount);
        Assert.Equal(0m, data.TotalGrossAmount);
        Assert.Equal(0m, data.TotalGrossYield);
        Assert.Equal(0m, data.TotalTaxAmount);
        Assert.Equal(0m, data.TotalIofAmount);
        Assert.Equal(0m, data.TotalNetAmount);
        Assert.Empty(data.Positions);
    }

    private static GetAssetProjectionUseCase CriarCaso(
        IEnumerable<Position> positions, IEnumerable<FixedIncomeAsset> assets, params IndexRate[] rates)
    {
        var clock = new TimeProviderStub(Now);
        var loader = new MarketDataLoader(
            new HolidayRepositoryStub(TestMarket.Holidays()), new IndexRateRepositoryStub(rates), clock);

        return new GetAssetProjectionUseCase(
            new FixedIncomeAssetRepositoryStub(assets),
            new PositionRepositoryStub(positions),
            new PositionProjector(loader, clock));
    }

    private static FixedIncomeAsset CriarAsset(
        IndexType indexType = IndexType.PreFixed, decimal rate = 10m, string name = "Título Teste")
    {
        var start = new DateOnly(2025, 1, 1);
        return new FixedIncomeAsset(name, "Emissor Teste", AssetType.Cdb, indexType, rate, start, start.AddYears(2));
    }

    private sealed class TimeProviderStub : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public TimeProviderStub(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;

        // Brasília fixo em UTC-3 (sem horário de verão desde 2019), para os testes não
        // dependerem do fuso nem do tzdata da máquina.
        public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.CreateCustomTimeZone(
            "America/Sao_Paulo", TimeSpan.FromHours(-3), "Brasília", "Brasília");
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
        private readonly IEnumerable<FixedIncomeAsset> _assets;

        public FixedIncomeAssetRepositoryStub(IEnumerable<FixedIncomeAsset> assets)
        {
            _assets = assets;
        }

        public Task<IEnumerable<FixedIncomeAsset>> GetAllAsync() => Task.FromResult(_assets);

        public Task<FixedIncomeAsset?> GetByIdAsync(Guid id) =>
            Task.FromResult(_assets.SingleOrDefault(asset => asset.Id == id));

        public Task AddAsync(FixedIncomeAsset asset) => Task.CompletedTask;

        public Task UpdateAsync(FixedIncomeAsset asset) => Task.CompletedTask;

        public Task DeleteAsync(FixedIncomeAsset asset) => Task.CompletedTask;
    }

    // Guarda os aportes de vários títulos e devolve, como o repositório real, só os do título pedido,
    // na ordem em que foram dados (o teste os entrega do mais recente para o mais antigo).
    private sealed class PositionRepositoryStub : IPositionRepository
    {
        private readonly IEnumerable<Position> _positions;

        public PositionRepositoryStub(IEnumerable<Position> positions)
        {
            _positions = positions;
        }

        public Task<IEnumerable<Position>> GetAllAsync() => Task.FromResult(_positions);

        public Task<Position?> GetByIdAsync(Guid id) => Task.FromResult<Position?>(null);

        public Task<IEnumerable<Position>> GetByAssetIdAsync(Guid assetId) =>
            Task.FromResult(_positions.Where(p => p.AssetId == assetId).AsEnumerable());

        public Task AddAsync(Position position) => Task.CompletedTask;
    }
}
