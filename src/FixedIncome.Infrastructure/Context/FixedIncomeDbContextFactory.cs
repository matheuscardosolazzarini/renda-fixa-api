using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FixedIncome.Infrastructure.Context;

// Desde a F4b a Api registra o DbContext via DI (AddInfrastructure), mas o dotnet ef
// prioriza um IDesignTimeDbContextFactory quando ele existe, então esta fábrica continua
// sendo o caminho usado — mantém a resolução em tempo de design independente de construir
// o host inteiro. A leitura do JSON usa System.Text.Json (BCL) para não exigir o pacote
// Microsoft.Extensions.Configuration.Json.
public class FixedIncomeDbContextFactory : IDesignTimeDbContextFactory<FixedIncomeDbContext>
{
    public FixedIncomeDbContext CreateDbContext(string[] args)
    {
        var connectionString = ReadConnectionString();

        var optionsBuilder = new DbContextOptionsBuilder<FixedIncomeDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new FixedIncomeDbContext(optionsBuilder.Options);
    }

    private static string ReadConnectionString()
    {
        var apiSettingsPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "FixedIncome.Api", "appsettings.Development.json");

        using var stream = File.OpenRead(apiSettingsPath);
        using var document = JsonDocument.Parse(stream);

        return document.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("DefaultConnection")
            .GetString()!;
    }
}
