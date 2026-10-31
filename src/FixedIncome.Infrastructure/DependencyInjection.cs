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

        services.AddHttpClient<IIndexRateProvider, SgsIndexRateProvider>(client =>
                client.BaseAddress = SgsIndexRateProvider.BaseAddress)
            .AddStandardResilienceHandler();

        return services;
    }

    // Só registra o job quando IndexRatesImport:Enabled é "true"; ausente ou inválido conta como
    // desligado, para que nenhum ambiente passe a chamar o Banco Central sem pedir.
    public static IServiceCollection AddIndexRatesImportJob(this IServiceCollection services, IConfiguration configuration)
    {
        var enabled = bool.TryParse(configuration["IndexRatesImport:Enabled"], out var value) && value;

        if (!enabled)
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
