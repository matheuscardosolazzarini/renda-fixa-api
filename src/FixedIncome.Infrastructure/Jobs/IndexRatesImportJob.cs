using FixedIncome.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FixedIncome.Infrastructure.Jobs;

// Importa as cotações de CDI e IPCA ao iniciar e depois a cada 6 horas. As séries do SGS
// publicam no máximo uma vez por dia, então a repetição só garante a recuperação após uma
// falha do provedor sem exigir intervenção manual.
public class IndexRatesImportJob : BackgroundService
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IndexRatesImportJob> _logger;
    private readonly TimeSpan _interval;

    public IndexRatesImportJob(
        IServiceScopeFactory scopeFactory,
        ILogger<IndexRatesImportJob> logger,
        TimeSpan interval)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = interval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);

        try
        {
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Desligamento da aplicação: não é falha.
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Escopo por execução: o caso de uso depende de repositórios scoped (DbContext).
            using var scope = _scopeFactory.CreateScope();
            var useCase = scope.ServiceProvider.GetRequiredService<IImportIndexRatesUseCase>();

            var response = await useCase.ExecuteAsync();

            if (response.Success)
            {
                _logger.LogInformation(
                    "Importação de cotações concluída: {CdiImported} de CDI, {CdiDiscarded} de CDI descartadas e {IpcaImported} de IPCA.",
                    response.Data!.CdiImported, response.Data.CdiDiscarded, response.Data.IpcaImported);
            }
            else
            {
                _logger.LogWarning("Importação de cotações terminou com falha: {Message}", response.Message);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Uma execução que falha nunca derruba o job nem o processo: a próxima tenta de novo.
            _logger.LogError(ex, "Falha inesperada na importação de cotações.");
        }
    }
}
