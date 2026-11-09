using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FixedIncome.Api.Tests;

public class IndexRatesImportStartupLogTests
{
    [Fact]
    public void Partida_da_api_com_importacao_desligada_por_valor_invalido_registra_o_aviso()
    {
        // Arrange
        var provider = new CapturingLoggerProvider();
        using var factory = CriarFabrica("0", provider);

        // Act
        _ = factory.Services;

        // Assert
        Assert.Contains(provider.Messages,
            m => m == "Importação de cotações desligada (IndexRatesImport:Enabled = '0'). Somente 'true' liga a importação.");
    }

    [Fact]
    public void Partida_da_api_com_importacao_ligada_nao_registra_o_aviso_de_desligada()
    {
        // Arrange
        var provider = new CapturingLoggerProvider();
        using var factory = CriarFabrica("true", provider, semJobDeImportacao: true);

        // Act
        _ = factory.Services;

        // Assert
        Assert.DoesNotContain(provider.Messages, m => m.Contains("Importação de cotações desligada"));
    }

    // Com "true" o job real seria registrado e chamaria o Banco Central; ele é removido do
    // contêiner depois do registro, só para o host subir sem rede. O log da partida é
    // decidido pela configuração, não pelo contêiner, então o teste continua válido.
    private static WebApplicationFactory<Program> CriarFabrica(
        string enabled, CapturingLoggerProvider provider, bool semJobDeImportacao = false)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("IndexRatesImport:Enabled", enabled);
            builder.ConfigureLogging(logging => logging.AddProvider(provider));

            if (semJobDeImportacao)
            {
                builder.ConfigureServices(services =>
                {
                    foreach (var descriptor in services
                        .Where(d => d.ImplementationFactory is not null
                            && (d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                                || d.ServiceType == typeof(FixedIncome.Infrastructure.Jobs.IndexRatesImportJob)))
                        .ToList())
                    {
                        services.Remove(descriptor);
                    }
                });
            }
        });
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly object _lock = new();
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_lock)
                {
                    return _messages.ToList();
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private void Add(string message)
        {
            lock (_lock)
            {
                _messages.Add(message);
            }
        }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                owner.Add(formatter(state, exception));
        }
    }
}
