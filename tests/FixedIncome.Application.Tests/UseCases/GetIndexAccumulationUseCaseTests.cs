using FixedIncome.Application.Repositories;
using FixedIncome.Application.Services;
using FixedIncome.Application.Tests.TestData;
using FixedIncome.Application.UseCases;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Application.Tests.UseCases;

// RN-13. Valores esperados dos casos 15 e 16/17 de docs/SPEC-F12-EXECUCAO.md.
public class GetIndexAccumulationUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    // Última cotação em 19/11/2026; o dia útil seguinte é 23/11 (20/11 feriado).
    private static IndexRate[] CdiAte19DeNovembro() =>
    [
        new(IndexType.Cdi, new DateOnly(2026, 11, 16), 0.050788m),
        new(IndexType.Cdi, new DateOnly(2026, 11, 17), 0.050788m),
        new(IndexType.Cdi, new DateOnly(2026, 11, 18), 0.050788m),
        new(IndexType.Cdi, new DateOnly(2026, 11, 19), 0.050788m)
    ];

    // Série 433 de 01/2026 a 09/2026, com deflação em agosto.
    private static IndexRate[] IpcaAteSetembro() =>
    [
        new(IndexType.Ipca, new DateOnly(2026, 1, 1), 0.33m),
        new(IndexType.Ipca, new DateOnly(2026, 2, 1), 0.70m),
        new(IndexType.Ipca, new DateOnly(2026, 3, 1), 0.88m),
        new(IndexType.Ipca, new DateOnly(2026, 4, 1), 0.67m),
        new(IndexType.Ipca, new DateOnly(2026, 5, 1), 0.58m),
        new(IndexType.Ipca, new DateOnly(2026, 6, 1), 0.16m),
        new(IndexType.Ipca, new DateOnly(2026, 7, 1), 0.07m),
        new(IndexType.Ipca, new DateOnly(2026, 8, 1), -0.32m),
        new(IndexType.Ipca, new DateOnly(2026, 9, 1), 0.82m)
    ];

    [Fact]
    public async Task Cdi_com_to_depois_da_ultima_cotacao_retorna_200_com_from_to_e_effective_to()
    {
        // Arrange: caso 15, linha 1.
        var useCase = CriarCaso(CdiAte19DeNovembro());

        // Act
        var response = await useCase.ExecuteAsync(
            IndexType.Cdi, new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 26));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(IndexType.Cdi, response.Data!.IndexType);
        Assert.Equal(new DateOnly(2026, 11, 16), response.Data.From);
        Assert.Equal(new DateOnly(2026, 11, 26), response.Data.To);
        Assert.Equal(new DateOnly(2026, 11, 23), response.Data.EffectiveTo);
        Assert.Equal(0.203307m, response.Data.AccumulatedPercentage);
    }

    [Fact]
    public async Task Ipca_com_to_depois_do_ultimo_mes_publicado_retorna_200_com_effective_to_no_ultimo_dia_dele()
    {
        // Arrange: caso 17a.
        var useCase = CriarCaso(IpcaAteSetembro());

        // Act
        var response = await useCase.ExecuteAsync(
            IndexType.Ipca, new DateOnly(2026, 1, 15), new DateOnly(2026, 11, 26));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(IndexType.Ipca, response.Data!.IndexType);
        Assert.Equal(new DateOnly(2026, 1, 15), response.Data.From);
        Assert.Equal(new DateOnly(2026, 11, 26), response.Data.To);
        Assert.Equal(new DateOnly(2026, 9, 30), response.Data.EffectiveTo);
        Assert.Equal(3.951320m, response.Data.AccumulatedPercentage);
    }

    [Fact]
    public async Task Ipca_de_periodo_maior_que_a_janela_de_12_meses_usa_todos_os_meses_do_periodo()
    {
        // Arrange: caso 16a, 12 meses de 2025, com o último publicado (09/2026) fora do período.
        var rates = new List<IndexRate>
        {
            new(IndexType.Ipca, new DateOnly(2025, 1, 1), 0.16m),
            new(IndexType.Ipca, new DateOnly(2025, 2, 1), 1.31m),
            new(IndexType.Ipca, new DateOnly(2025, 3, 1), 0.56m),
            new(IndexType.Ipca, new DateOnly(2025, 4, 1), 0.43m),
            new(IndexType.Ipca, new DateOnly(2025, 5, 1), 0.26m),
            new(IndexType.Ipca, new DateOnly(2025, 6, 1), 0.24m),
            new(IndexType.Ipca, new DateOnly(2025, 7, 1), 0.26m),
            new(IndexType.Ipca, new DateOnly(2025, 8, 1), -0.11m),
            new(IndexType.Ipca, new DateOnly(2025, 9, 1), 0.48m),
            new(IndexType.Ipca, new DateOnly(2025, 10, 1), 0.09m),
            new(IndexType.Ipca, new DateOnly(2025, 11, 1), 0.18m),
            new(IndexType.Ipca, new DateOnly(2025, 12, 1), 0.33m)
        };
        rates.AddRange(IpcaAteSetembro());
        var useCase = CriarCaso(rates.ToArray());

        // Act
        var response = await useCase.ExecuteAsync(
            IndexType.Ipca, new DateOnly(2025, 1, 10), new DateOnly(2025, 12, 20));

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(new DateOnly(2025, 12, 20), response.Data!.EffectiveTo);
        Assert.Equal(4.264380m, response.Data.AccumulatedPercentage);
    }

    [Fact]
    public async Task Data_inicial_posterior_a_data_final_retorna_400()
    {
        // Arrange
        var useCase = CriarCaso(CdiAte19DeNovembro());

        // Act
        var response = await useCase.ExecuteAsync(
            IndexType.Cdi, new DateOnly(2026, 11, 18), new DateOnly(2026, 11, 17));

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Equal("A data inicial não pode ser posterior à data final.", response.Message);
    }

    [Fact]
    public async Task Indexador_pre_fixado_retorna_400_porque_nao_tem_cotacao()
    {
        // Arrange
        var useCase = CriarCaso(CdiAte19DeNovembro());

        // Act
        var response = await useCase.ExecuteAsync(
            IndexType.PreFixed, new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 19));

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Equal("Só há cotação para CDI e IPCA.", response.Message);
    }

    [Fact]
    public async Task Cdi_com_from_depois_da_ultima_cotacao_retorna_400_de_periodo_sem_cotacao()
    {
        // Arrange: caso 15, última linha.
        var useCase = CriarCaso(CdiAte19DeNovembro());

        // Act
        var response = await useCase.ExecuteAsync(
            IndexType.Cdi, new DateOnly(2026, 11, 23), new DateOnly(2026, 11, 26));

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(
            "Não há cotação de CDI publicada no período de 2026-11-23 a 2026-11-26.", response.Message);
    }

    [Fact]
    public async Task Ipca_com_mes_de_from_depois_do_ultimo_publicado_retorna_400_de_periodo_sem_cotacao()
    {
        // Arrange
        var useCase = CriarCaso(IpcaAteSetembro());

        // Act
        var response = await useCase.ExecuteAsync(
            IndexType.Ipca, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(
            "Não há cotação de IPCA publicada no período de 2026-10-01 a 2026-10-31.", response.Message);
    }

    [Fact]
    public async Task Cdi_com_lacuna_antes_da_ultima_cotacao_retorna_400_com_a_data_ausente()
    {
        // Arrange: falta 17/11, anterior à última cotação.
        var rates = CdiAte19DeNovembro().Where(rate => rate.ReferenceDate != new DateOnly(2026, 11, 17)).ToArray();
        var useCase = CriarCaso(rates);

        // Act
        var response = await useCase.ExecuteAsync(
            IndexType.Cdi, new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 26));

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Equal("Cotação de CDI ausente para 2026-11-17.", response.Message);
    }

    [Fact]
    public async Task Cdi_com_from_fora_da_cobertura_do_calendario_retorna_400()
    {
        // Arrange: a cobertura dos feriados do teste vai de 2025-01-01 a 2030-12-31.
        var useCase = CriarCaso(CdiAte19DeNovembro());

        // Act
        var response = await useCase.ExecuteAsync(
            IndexType.Cdi, new DateOnly(2024, 12, 2), new DateOnly(2026, 11, 19));

        // Assert
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("fora do período coberto pelo calendário", response.Message);
    }

    private static GetIndexAccumulationUseCase CriarCaso(params IndexRate[] rates)
    {
        var loader = new MarketDataLoader(
            new HolidayRepositoryStub(TestMarket.HolidaysWith2026()),
            new IndexRateRepositoryStub(rates),
            new TimeProviderStub(Now));

        return new GetIndexAccumulationUseCase(loader);
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
}
