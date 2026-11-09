using FixedIncome.Application.Providers;
using FixedIncome.Application.Repositories;
using FixedIncome.Infrastructure.Jobs;
using FixedIncome.Infrastructure.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FixedIncome.Infrastructure.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void Infraestrutura_registra_repositorios_e_o_provedor_do_sgs()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddInfrastructure("Host=sem-banco;Database=teste;Username=teste;Password=teste");

        // Assert
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        Assert.IsType<SgsIndexRateProvider>(scope.ServiceProvider.GetRequiredService<IIndexRateProvider>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IIndexRateRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IHolidayRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPositionRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IFixedIncomeAssetRepository>());
    }

    [Fact]
    public void Job_de_importacao_so_e_registrado_quando_habilitado()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = CriarConfiguracao("true");

        // Act
        services.AddIndexRatesImportJob(configuration);

        // Assert
        using var provider = services.BuildServiceProvider();
        Assert.IsType<IndexRatesImportJob>(provider.GetServices<IHostedService>().Single());
    }

    [Theory]
    [InlineData("false")]
    [InlineData("talvez")]
    [InlineData("")]
    [InlineData(null)]
    public void Job_de_importacao_fica_desligado_com_valor_ausente_ou_invalido(string? valor)
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddIndexRatesImportJob(CriarConfiguracao(valor));

        // Assert
        Assert.Empty(services);
    }

    [Theory]
    [InlineData("false", "Importação de cotações desligada (IndexRatesImport:Enabled = 'false'). Somente 'true' liga a importação.")]
    [InlineData("0", "Importação de cotações desligada (IndexRatesImport:Enabled = '0'). Somente 'true' liga a importação.")]
    [InlineData("yes", "Importação de cotações desligada (IndexRatesImport:Enabled = 'yes'). Somente 'true' liga a importação.")]
    [InlineData("", "Importação de cotações desligada (IndexRatesImport:Enabled = ''). Somente 'true' liga a importação.")]
    [InlineData(null, "Importação de cotações desligada (IndexRatesImport:Enabled = ''). Somente 'true' liga a importação.")]
    public void Partida_com_importacao_desligada_registra_aviso_com_o_valor_recebido(string? valor, string mensagemEsperada)
    {
        // Arrange
        var logger = new LoggerStub();
        var configuration = CriarConfiguracao(valor);

        // Act
        DependencyInjection.LogIndexRatesImportStatus(configuration, logger);

        // Assert
        var entrada = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entrada.Level);
        Assert.Equal(mensagemEsperada, entrada.Message);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    public void Partida_com_importacao_ligada_nao_registra_o_aviso_de_desligada(string valor)
    {
        // Arrange
        var logger = new LoggerStub();
        var configuration = CriarConfiguracao(valor);

        // Act
        DependencyInjection.LogIndexRatesImportStatus(configuration, logger);

        // Assert
        Assert.Empty(logger.Entries);
    }

    private sealed class LoggerStub : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private static IConfiguration CriarConfiguracao(string? enabled)
    {
        var valores = new Dictionary<string, string?>();
        if (enabled is not null)
        {
            valores["IndexRatesImport:Enabled"] = enabled;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
    }
}
