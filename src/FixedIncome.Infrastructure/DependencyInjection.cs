using FixedIncome.Application.Providers;
using FixedIncome.Application.Repositories;
using FixedIncome.Infrastructure.Context;
using FixedIncome.Infrastructure.Jobs;
using FixedIncome.Infrastructure.Providers;
using FixedIncome.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FixedIncome.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<FixedIncomeDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IFixedIncomeAssetRepository, FixedIncomeAssetRepository>();
        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IHolidayRepository, HolidayRepository>();
        services.AddScoped<IIndexRateRepository, IndexRateRepository>();

        var sgsClient = services.AddHttpClient<IIndexRateProvider, SgsIndexRateProvider>(client =>
            client.BaseAddress = SgsIndexRateProvider.BaseAddress);

        // A ordem importa: o primeiro handler registrado é o mais externo. A checagem do corpo vem
        // depois da resiliência para o retry enxergar a falha que ela levanta.
        sgsClient.AddStandardResilienceHandler();
        sgsClient.AddHttpMessageHandler(() => new SgsNonJsonResponseHandler());

        return services;
    }

    // R-05: valores como "0" ou "yes" desligam a importação sem erro; o aviso na partida mostra ao
    // operador o valor recebido. Chamado pelo Program.cs com o logger do app, já que o registro
    // do job acontece antes de o contêiner de serviços existir.
    public static void LogIndexRatesImportStatus(IConfiguration configuration, ILogger logger)
    {
        if (IsIndexRatesImportEnabled(configuration))
        {
            return;
        }

        logger.LogInformation(
            "Importação de cotações desligada (IndexRatesImport:Enabled = '{Value}'). Somente 'true' liga a importação.",
            configuration["IndexRatesImport:Enabled"] ?? string.Empty);
    }

    private static bool IsIndexRatesImportEnabled(IConfiguration configuration) =>
        bool.TryParse(configuration["IndexRatesImport:Enabled"], out var value) && value;

    // Só registra o job quando IndexRatesImport:Enabled é "true"; ausente ou inválido conta como
    // desligado, para que nenhum ambiente passe a chamar o Banco Central sem pedir.
    public static IServiceCollection AddIndexRatesImportJob(this IServiceCollection services, IConfiguration configuration)
    {
        if (!IsIndexRatesImportEnabled(configuration))
        {
            return services;
        }

        services.AddSingleton(sp => new IndexRatesImportJob(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILogger<IndexRatesImportJob>>(),
            IndexRatesImportJob.DefaultInterval));
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<IndexRatesImportJob>());

        return services;
    }
}
