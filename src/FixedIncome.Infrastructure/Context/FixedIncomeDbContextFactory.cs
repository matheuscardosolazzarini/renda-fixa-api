using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FixedIncome.Infrastructure.Context;

// Desde a F4b a Api registra o DbContext via DI (AddInfrastructure), mas o dotnet ef
// prioriza um IDesignTimeDbContextFactory quando ele existe, então esta fábrica continua
// sendo o caminho usado — mantém a resolução em tempo de design independente de construir
// o host inteiro.
//
// A connection string vem de duas fontes, nesta ordem: a variável de ambiente
// ConnectionStrings__DefaultConnection (a mesma que o Program.cs da Api lê; é o que permite
// gerar e rodar o migration bundle no pipeline, onde não há appsettings.Development.json) e,
// na falta dela, o appsettings.Development.json da Api (o dotnet ef local). A leitura do
// JSON usa System.Text.Json (BCL) para não exigir o pacote
// Microsoft.Extensions.Configuration.Json.
public class FixedIncomeDbContextFactory : IDesignTimeDbContextFactory<FixedIncomeDbContext>
{
    private const string EnvironmentVariableName = "ConnectionStrings__DefaultConnection";
    private const string SettingsFileName = "appsettings.Development.json";

    public FixedIncomeDbContext CreateDbContext(string[] args)
    {
        var connectionString = ReadConnectionString();

        var optionsBuilder = new DbContextOptionsBuilder<FixedIncomeDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new FixedIncomeDbContext(optionsBuilder.Options);
    }

    private static string ReadConnectionString()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        var apiSettingsPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "FixedIncome.Api", SettingsFileName);

        if (!File.Exists(apiSettingsPath))
        {
            throw new InvalidOperationException(
                $"Connection string não encontrada. Defina a variável de ambiente " +
                $"{EnvironmentVariableName} ou crie o arquivo {SettingsFileName} em " +
                $"{Path.GetFullPath(apiSettingsPath)}.");
        }

        using var stream = File.OpenRead(apiSettingsPath);
        using var document = JsonDocument.Parse(stream);

        return document.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("DefaultConnection")
            .GetString()!;
    }
}
