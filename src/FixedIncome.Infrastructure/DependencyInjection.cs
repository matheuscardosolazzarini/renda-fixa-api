using FixedIncome.Application.Repositories;
using FixedIncome.Infrastructure.Context;
using FixedIncome.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FixedIncome.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<FixedIncomeDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IFixedIncomeAssetRepository, FixedIncomeAssetRepository>();
        services.AddScoped<IPositionRepository, PositionRepository>();

        return services;
    }
}
