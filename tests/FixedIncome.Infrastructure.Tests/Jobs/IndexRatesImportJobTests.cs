using FixedIncome.Application.Common;
using FixedIncome.Application.DTOs;
using FixedIncome.Application.UseCases;
using FixedIncome.Infrastructure.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FixedIncome.Infrastructure.Tests.Jobs;

public class IndexRatesImportJobTests
{
    private static readonly TimeSpan ShortInterval = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan LongInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Executa_o_caso_de_uso_ao_iniciar_sem_esperar_o_intervalo()
    {
        // Arrange
        var useCase = new UseCaseStub();
        var job = CreateJob(useCase, new LoggerStub(), LongInterval);

        // Act
        await job.StartAsync(CancellationToken.None);
        await useCase.WaitForCallsAsync(1, WaitLimit);
        await job.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, useCase.Calls);
    }

    [Fact]
    public async Task Executa_novamente_a_cada_intervalo()
    {
        // Arrange
        var useCase = new UseCaseStub();
        var job = CreateJob(useCase, new LoggerStub(), ShortInterval);

        // Act
        await job.StartAsync(CancellationToken.None);
        await useCase.WaitForCallsAsync(3, WaitLimit);
        await job.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(useCase.Calls >= 3);
    }

    [Fact]
    public async Task Excecao_do_caso_de_uso_nao_derruba_o_job_e_e_registrada_em_log()
    {
        // Arrange
        var useCase = new UseCaseStub { ThrowOnCalls = 1 };
        var logger = new LoggerStub();
        var job = CreateJob(useCase, logger, ShortInterval);

        // Act
        await job.StartAsync(CancellationToken.None);
        await useCase.WaitForCallsAsync(2, WaitLimit);
        await job.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(useCase.Calls >= 2);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Exception is InvalidOperationException);
        Assert.Equal(TaskStatus.RanToCompletion, job.ExecuteTask!.Status);
    }

    [Fact]
    public async Task Resposta_de_falha_do_caso_de_uso_e_registrada_como_aviso_e_o_job_continua()
    {
        // Arrange
        var useCase = new UseCaseStub { FailResponse = true };
        var logger = new LoggerStub();
        var job = CreateJob(useCase, logger, ShortInterval);

        // Act
        await job.StartAsync(CancellationToken.None);
        await useCase.WaitForCallsAsync(2, WaitLimit);
        await job.StopAsync(CancellationToken.None);

        // Assert
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Falha ao importar"));
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task Cancelamento_encerra_o_job_sem_erro_e_sem_log_de_erro()
    {
        // Arrange
        var useCase = new UseCaseStub();
        var logger = new LoggerStub();
        var job = CreateJob(useCase, logger, LongInterval);
        await job.StartAsync(CancellationToken.None);
        await useCase.WaitForCallsAsync(1, WaitLimit);

        // Act
        await job.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(TaskStatus.RanToCompletion, job.ExecuteTask!.Status);
        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Error);
    }

    private static IndexRatesImportJob CreateJob(UseCaseStub useCase, LoggerStub logger, TimeSpan interval)
    {
        var services = new ServiceCollection();
        services.AddScoped<IImportIndexRatesUseCase>(_ => useCase);
        var provider = services.BuildServiceProvider();

        return new IndexRatesImportJob(
            provider.GetRequiredService<IServiceScopeFactory>(), logger, interval);
    }

    private sealed class UseCaseStub : IImportIndexRatesUseCase
    {
        private int _calls;
        private readonly SemaphoreSlim _signal = new(0);

        public int Calls => Volatile.Read(ref _calls);
        public int ThrowOnCalls { get; init; }
        public bool FailResponse { get; init; }

        public Task<ApiResponse<ImportIndexRatesResponse>> ExecuteAsync()
        {
            var call = Interlocked.Increment(ref _calls);
            _signal.Release();

            if (call <= ThrowOnCalls)
            {
                throw new InvalidOperationException("Falha provocada pelo teste.");
            }

            return Task.FromResult(FailResponse
                ? ApiResponse<ImportIndexRatesResponse>.InternalError("Falha ao importar as cotações de Cdi.")
                : ApiResponse<ImportIndexRatesResponse>.Ok(new ImportIndexRatesResponse(1, 0, 1)));
        }

        public async Task WaitForCallsAsync(int count, TimeSpan limit)
        {
            using var cts = new CancellationTokenSource(limit);

            while (Calls < count)
            {
                await _signal.WaitAsync(cts.Token);
            }
        }
    }

    private sealed class LoggerStub : ILogger<IndexRatesImportJob>
    {
        private readonly object _lock = new();
        private readonly List<(LogLevel Level, string Message, Exception? Exception)> _entries = [];

        public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Entries
        {
            get
            {
                lock (_lock)
                {
                    return _entries.ToList();
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (_lock)
            {
                _entries.Add((logLevel, formatter(state, exception), exception));
            }
        }
    }
}
