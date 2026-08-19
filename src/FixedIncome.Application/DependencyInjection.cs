using FixedIncome.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;

namespace FixedIncome.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICreateAssetUseCase, CreateAssetUseCase>();
        services.AddScoped<IGetAllAssetsUseCase, GetAllAssetsUseCase>();
        services.AddScoped<IGetAssetByIdUseCase, GetAssetByIdUseCase>();
        services.AddScoped<IUpdateAssetUseCase, UpdateAssetUseCase>();
        services.AddScoped<IDeleteAssetUseCase, DeleteAssetUseCase>();
        services.AddScoped<ICreatePositionUseCase, CreatePositionUseCase>();
        services.AddScoped<IGetAllPositionsUseCase, GetAllPositionsUseCase>();
        services.AddScoped<IGetPositionProjectionUseCase, GetPositionProjectionUseCase>();

        return services;
    }
}
