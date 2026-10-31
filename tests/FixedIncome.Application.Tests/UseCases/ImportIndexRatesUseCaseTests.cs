using System.Text.Json;
using FixedIncome.Application.Providers;
using FixedIncome.Application.Repositories;
using FixedIncome.Application.Tests.TestData;
using FixedIncome.Application.UseCases;
using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FixedIncome.Application.Tests.UseCases;

public class ImportIndexRatesUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2025, 4, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2025, 4, 30);

    public static IEnumerable<object[]> ProviderFailures()
    {
        yield return new object[] { new HttpRequestException("SGS indisponível") };
        yield return new object[] { new DomainException("Cotação inválida") };
        yield return new object[] { new FormatException("Data fora do formato") };
        yield return new object[] { new JsonException("JSON malformado") };
    }

    [Fact]
    public async Task Primeira_execucao_busca_cdi_e_ipca_desde_2020_01_01_ate_hoje()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var response = await harness.UseCase.ExecuteAsync();

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(
            new[]
            {
                (IndexType.Cdi, new DateOnly(2020, 1, 1), Today),
                (IndexType.Ipca, new DateOnly(2020, 1, 1), Today),
            },
            harness.Provider.Calls);
    }

    [Fact]
    public async Task Execucao_incremental_do_cdi_parte_do_dia_seguinte_a_ultima_data_importada()
    {
        // Arrange
        var harness = new Harness();
        harness.Repository.Seed(new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 16), 0.05m));

        // Act
        await harness.UseCase.ExecuteAsync();

        // Assert
        Assert.Contains((IndexType.Cdi, new DateOnly(2025, 4, 17), Today), harness.Provider.Calls);
    }

    [Fact]
    public async Task Execucao_incremental_do_ipca_parte_do_primeiro_dia_do_mes_seguinte_ao_ultimo_publicado()
    {
        // Arrange: o IPCA de março é datado em 01/03; o dia seguinte (02/03) perderia o de abril (01/04).
        var harness = new Harness();
        harness.Repository.Seed(new IndexRate(IndexType.Ipca, new DateOnly(2025, 3, 1), 0.56m));

        // Act
        await harness.UseCase.ExecuteAsync();

        // Assert
        Assert.Contains((IndexType.Ipca, new DateOnly(2025, 4, 1), Today), harness.Provider.Calls);
    }

    [Fact]
    public async Task Importa_o_ipca_publicado_com_data_no_primeiro_dia_do_mes_inclusive_em_fim_de_semana()
    {
        // Arrange: 01/06/2025 é domingo; o IPCA é mensal e não passa pelo filtro de dia útil.
        var harness = new Harness();
        harness.Provider.Add(new IndexRate(IndexType.Ipca, new DateOnly(2025, 6, 1), 0.24m));
        var useCase = harness.CreateUseCase(new DateTimeOffset(2025, 7, 15, 12, 0, 0, TimeSpan.Zero));

        // Act
        var response = await useCase.ExecuteAsync();

        // Assert
        Assert.Equal(1, response.Data!.IpcaImported);
        Assert.Equal(0.24m, harness.Repository.Get(IndexType.Ipca, new DateOnly(2025, 6, 1))!.Value);
    }

    [Fact]
    public async Task Executar_duas_vezes_com_os_mesmos_dados_nao_duplica_as_cotacoes()
    {
        // Arrange
        var harness = new Harness();
        harness.Provider.Add(
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 14), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 15), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 16), 0.05m),
            new IndexRate(IndexType.Ipca, new DateOnly(2025, 3, 1), 0.56m));

        // Act
        await harness.UseCase.ExecuteAsync();
        var countAfterFirst = harness.Repository.Count;
        var second = await harness.UseCase.ExecuteAsync();

        // Assert
        Assert.Equal(4, countAfterFirst);
        Assert.Equal(4, harness.Repository.Count);
        Assert.Equal(200, second.StatusCode);
        Assert.Equal(0, second.Data!.CdiImported);
        Assert.Equal(0, second.Data!.IpcaImported);
    }

    [Fact]
    public async Task Cdi_em_dia_nao_util_e_descartado_e_registrado_em_log()
    {
        // Arrange: 16/04/2025 é quarta (útil); 18/04 é a Paixão (feriado) e 19/04 é sábado.
        var harness = new Harness();
        harness.Provider.Add(
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 16), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 18), 0.05m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 19), 0.05m));

        // Act
        var response = await harness.UseCase.ExecuteAsync();

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(1, response.Data!.CdiImported);
        Assert.Equal(2, response.Data!.CdiDiscarded);
        Assert.NotNull(harness.Repository.Get(IndexType.Cdi, new DateOnly(2025, 4, 16)));
        Assert.Null(harness.Repository.Get(IndexType.Cdi, new DateOnly(2025, 4, 18)));
        Assert.Null(harness.Repository.Get(IndexType.Cdi, new DateOnly(2025, 4, 19)));
        Assert.Equal(2, harness.Logger.Entries.Count(e => e.Level == LogLevel.Warning));
    }

    [Fact]
    public async Task Sem_cotacao_nova_a_buscar_nao_chama_o_provedor()
    {
        // Arrange: CDI já importado até hoje e IPCA já importado até o mês corrente.
        var harness = new Harness();
        harness.Repository.Seed(
            new IndexRate(IndexType.Cdi, Today, 0.05m),
            new IndexRate(IndexType.Ipca, new DateOnly(2025, 4, 1), 0.43m));

        // Act
        var response = await harness.UseCase.ExecuteAsync();

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Empty(harness.Provider.Calls);
    }

    [Theory]
    [MemberData(nameof(ProviderFailures))]
    public async Task Falha_do_provedor_no_cdi_vira_erro_com_log_sem_lancar_excecao_e_nao_impede_o_ipca(
        Exception failure)
    {
        // Arrange
        var harness = new Harness();
        harness.Provider.FailWith(IndexType.Cdi, failure);
        harness.Provider.Add(new IndexRate(IndexType.Ipca, new DateOnly(2025, 3, 1), 0.56m));

        // Act
        var response = await harness.UseCase.ExecuteAsync();

        // Assert
        Assert.Equal(500, response.StatusCode);
        Assert.False(response.Success);
        Assert.Contains("Cdi", response.Message);
        Assert.Contains(harness.Logger.Entries, e => e.Level == LogLevel.Error && e.Exception == failure);
        Assert.NotNull(harness.Repository.Get(IndexType.Ipca, new DateOnly(2025, 3, 1)));
    }

    [Theory]
    [MemberData(nameof(ProviderFailures))]
    public async Task Falha_do_provedor_no_ipca_vira_erro_com_log_sem_lancar_excecao_e_nao_impede_o_cdi(
        Exception failure)
    {
        // Arrange
        var harness = new Harness();
        harness.Provider.FailWith(IndexType.Ipca, failure);
        harness.Provider.Add(new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 16), 0.05m));

        // Act
        var response = await harness.UseCase.ExecuteAsync();

        // Assert
        Assert.Equal(500, response.StatusCode);
        Assert.Contains("Ipca", response.Message);
        Assert.Contains(harness.Logger.Entries, e => e.Level == LogLevel.Error && e.Exception == failure);
        Assert.NotNull(harness.Repository.Get(IndexType.Cdi, new DateOnly(2025, 4, 16)));
    }

    private sealed class Harness
    {
        public IndexRateProviderStub Provider { get; } = new();
        public IndexRateRepositoryStub Repository { get; } = new();
        public LoggerStub Logger { get; } = new();
        public ImportIndexRatesUseCase UseCase { get; }

        public Harness()
        {
            UseCase = CreateUseCase(Now);
        }

        public ImportIndexRatesUseCase CreateUseCase(DateTimeOffset now) => new(
            Provider,
            Repository,
            new HolidayRepositoryStub(TestMarket.Holidays()),
            new TimeProviderStub(now),
            Logger);
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

    private sealed class IndexRateProviderStub : IIndexRateProvider
    {
        private readonly List<IndexRate> _available = [];
        private readonly Dictionary<IndexType, Exception> _failures = [];

        public List<(IndexType Type, DateOnly From, DateOnly To)> Calls { get; } = [];

        public void Add(params IndexRate[] rates) => _available.AddRange(rates);

        public void FailWith(IndexType type, Exception failure) => _failures[type] = failure;

        public Task<IEnumerable<IndexRate>> FetchAsync(IndexType type, DateOnly from, DateOnly to)
        {
            Calls.Add((type, from, to));

            if (_failures.TryGetValue(type, out var failure))
            {
                throw failure;
            }

            return Task.FromResult(_available
                .Where(r => r.IndexType == type && r.ReferenceDate >= from && r.ReferenceDate <= to)
                .AsEnumerable());
        }
    }

    private sealed class IndexRateRepositoryStub : IIndexRateRepository
    {
        private readonly Dictionary<(IndexType, DateOnly), IndexRate> _rates = [];

        public int Count => _rates.Count;

        public void Seed(params IndexRate[] rates) => UpsertAsync(rates);

        public IndexRate? Get(IndexType type, DateOnly date) => _rates.GetValueOrDefault((type, date));

        public Task<IEnumerable<IndexRate>> GetByIndexTypeAndPeriodAsync(IndexType indexType, DateOnly from, DateOnly to) =>
            Task.FromResult(_rates.Values
                .Where(r => r.IndexType == indexType && r.ReferenceDate >= from && r.ReferenceDate <= to)
                .AsEnumerable());

        public Task<IndexRate?> GetLastBeforeAsync(IndexType indexType, DateOnly date) =>
            Task.FromResult(_rates.Values
                .Where(r => r.IndexType == indexType && r.ReferenceDate < date)
                .OrderByDescending(r => r.ReferenceDate)
                .FirstOrDefault());

        public Task<DateOnly?> GetLastReferenceDateAsync(IndexType indexType) =>
            Task.FromResult(_rates.Values
                .Where(r => r.IndexType == indexType)
                .Select(r => (DateOnly?)r.ReferenceDate)
                .Max());

        // Mesma semântica do repositório real: o par tipo e data é a chave; repetir atualiza.
        public Task UpsertAsync(IEnumerable<IndexRate> rates)
        {
            foreach (var rate in rates)
            {
                _rates[(rate.IndexType, rate.ReferenceDate)] = rate;
            }

            return Task.CompletedTask;
        }
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

    private sealed class LoggerStub : ILogger<ImportIndexRatesUseCase>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception), exception));
        }
    }
}
