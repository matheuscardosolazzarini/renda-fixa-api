using FixedIncome.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace FixedIncome.Infrastructure.Tests.Context;

// Variável de ambiente e diretório atual são estado global do processo: a classe fica
// numa coleção que não roda em paralelo com as demais e restaura os dois no Dispose.
[CollectionDefinition(nameof(FixedIncomeDbContextFactoryTests), DisableParallelization = true)]
public class FixedIncomeDbContextFactoryCollection;

[Collection(nameof(FixedIncomeDbContextFactoryTests))]
public sealed class FixedIncomeDbContextFactoryTests : IDisposable
{
    private const string VariableName = "ConnectionStrings__DefaultConnection";

    private readonly string? _originalVariable = Environment.GetEnvironmentVariable(VariableName);
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "fixedincome-factory-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(VariableName, _originalVariable);
        Directory.SetCurrentDirectory(_originalDirectory);

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Deve_usar_a_connection_string_da_variavel_de_ambiente_sem_ler_arquivo()
    {
        // Arrange
        const string conexao = "Host=banco-do-ambiente;Database=ambiente;Username=u;Password=p";
        Environment.SetEnvironmentVariable(VariableName, conexao);
        EnterDirectoryWithoutSettingsFile();
        var factory = new FixedIncomeDbContextFactory();

        // Act
        using var context = factory.CreateDbContext([]);

        // Assert
        Assert.Equal(conexao, context.Database.GetConnectionString());
    }

    [Fact]
    public void Deve_priorizar_a_variavel_de_ambiente_sobre_o_arquivo()
    {
        // Arrange
        const string conexao = "Host=banco-do-ambiente;Database=ambiente;Username=u;Password=p";
        Environment.SetEnvironmentVariable(VariableName, conexao);
        EnterDirectoryWithSettingsFile("Host=banco-do-arquivo;Database=arquivo;Username=u;Password=p");
        var factory = new FixedIncomeDbContextFactory();

        // Act
        using var context = factory.CreateDbContext([]);

        // Assert
        Assert.Equal(conexao, context.Database.GetConnectionString());
    }

    [Fact]
    public void Deve_ler_o_appsettings_de_desenvolvimento_quando_a_variavel_nao_esta_definida()
    {
        // Arrange
        const string conexao = "Host=banco-do-arquivo;Database=arquivo;Username=u;Password=p";
        Environment.SetEnvironmentVariable(VariableName, null);
        EnterDirectoryWithSettingsFile(conexao);
        var factory = new FixedIncomeDbContextFactory();

        // Act
        using var context = factory.CreateDbContext([]);

        // Assert
        Assert.Equal(conexao, context.Database.GetConnectionString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Deve_ler_o_appsettings_quando_a_variavel_e_vazia_ou_so_espacos(string valor)
    {
        // Arrange
        const string conexao = "Host=banco-do-arquivo;Database=arquivo;Username=u;Password=p";
        Environment.SetEnvironmentVariable(VariableName, valor);
        EnterDirectoryWithSettingsFile(conexao);
        var factory = new FixedIncomeDbContextFactory();

        // Act
        using var context = factory.CreateDbContext([]);

        // Assert
        Assert.Equal(conexao, context.Database.GetConnectionString());
    }

    [Fact]
    public void Deve_lancar_excecao_citando_as_duas_fontes_quando_nao_ha_variavel_nem_arquivo()
    {
        // Arrange
        Environment.SetEnvironmentVariable(VariableName, null);
        EnterDirectoryWithoutSettingsFile();
        var factory = new FixedIncomeDbContextFactory();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext([]));

        // Assert
        Assert.Contains(VariableName, exception.Message);
        Assert.Contains("appsettings.Development.json", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Deve_lancar_excecao_quando_a_variavel_e_vazia_ou_so_espacos_e_nao_ha_arquivo(string valor)
    {
        // Arrange
        Environment.SetEnvironmentVariable(VariableName, valor);
        EnterDirectoryWithoutSettingsFile();
        var factory = new FixedIncomeDbContextFactory();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext([]));

        // Assert
        Assert.Contains(VariableName, exception.Message);
        Assert.Contains("appsettings.Development.json", exception.Message);
    }

    // Simula rodar o dotnet ef a partir de src/FixedIncome.Infrastructure, sem o irmão Api.
    private void EnterDirectoryWithoutSettingsFile()
    {
        var infrastructure = Path.Combine(_root, "FixedIncome.Infrastructure");
        Directory.CreateDirectory(infrastructure);
        Directory.SetCurrentDirectory(infrastructure);
    }

    private void EnterDirectoryWithSettingsFile(string connectionString)
    {
        var infrastructure = Path.Combine(_root, "FixedIncome.Infrastructure");
        var api = Path.Combine(_root, "FixedIncome.Api");
        Directory.CreateDirectory(infrastructure);
        Directory.CreateDirectory(api);
        File.WriteAllText(
            Path.Combine(api, "appsettings.Development.json"),
            $$"""{ "ConnectionStrings": { "DefaultConnection": "{{connectionString}}" } }""");
        Directory.SetCurrentDirectory(infrastructure);
    }
}
