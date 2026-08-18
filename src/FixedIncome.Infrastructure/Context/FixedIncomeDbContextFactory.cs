using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FixedIncome.Infrastructure.Context;

// Necessário porque a Api ainda não registra o DbContext via DI (fora do escopo desta
// fase) e não referencia o pacote Design. As ferramentas do dotnet ef precisam de uma
// fábrica em tempo de design para descobrir a connection string. A leitura do JSON usa
// System.Text.Json (BCL) para não exigir o pacote Microsoft.Extensions.Configuration.Json.
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
