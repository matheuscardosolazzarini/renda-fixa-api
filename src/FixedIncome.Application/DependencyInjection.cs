using FixedIncome.Application.Services;
using FixedIncome.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FixedIncome.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Relógio injetado para a data atual nunca vir de DateTime.UtcNow; TryAdd deixa os testes trocarem.
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<MarketDataLoader>();

        services.AddScoped<ICreateAssetUseCase, CreateAssetUseCase>();
        services.AddScoped<IGetAllAssetsUseCase, GetAllAssetsUseCase>();
        services.AddScoped<IGetAssetByIdUseCase, GetAssetByIdUseCase>();
        services.AddScoped<IUpdateAssetUseCase, UpdateAssetUseCase>();
        services.AddScoped<IDeleteAssetUseCase, DeleteAssetUseCase>();
        services.AddScoped<ICreatePositionUseCase, CreatePositionUseCase>();
        services.AddScoped<IGetAllPositionsUseCase, GetAllPositionsUseCase>();
        services.AddScoped<IGetPositionProjectionUseCase, GetPositionProjectionUseCase>();
        services.AddScoped<IGetPortfolioSummaryUseCase, GetPortfolioSummaryUseCase>();
        services.AddScoped<IImportIndexRatesUseCase, ImportIndexRatesUseCase>();
        services.AddScoped<IGetIndexRatesUseCase, GetIndexRatesUseCase>();

        return services;
    }
}
