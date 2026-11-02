using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FixedIncome.Api.Tests;

public class AssetsControllerTests : ApiTestBase
{
    // Inicializado antes do construtor da base, que chama ConfigureTestServices.
    private readonly ErrorLogProviderStub _logs = new();

    private static object CriarRequestValido() => new
    {
        name = "CDB Banco X",
        issuer = "Banco X",
        assetType = "Cdb",
        indexType = "PreFixed",
        rate = 12,
        issueDate = "2025-01-01",
        maturityDate = "2027-01-01"
    };

    [Fact]
    public async Task Titulo_valido_retorna_201_com_id_gerado()
    {
        // Arrange
        var request = CriarRequestValido();

        // Act
        var response = await Client.PostAsJsonAsync("/api/assets", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var id = json.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task Titulo_valido_retorna_asset_type_e_index_type_como_texto_legivel()
    {
        // Arrange
        var request = CriarRequestValido();

        // Act
        var response = await Client.PostAsJsonAsync("/api/assets", request);

        // Assert
        using var json = await ReadJsonAsync(response);
        var data = json.RootElement.GetProperty("data");
        Assert.Equal("Cdb", data.GetProperty("assetType").GetString());
        Assert.Equal("PreFixed", data.GetProperty("indexType").GetString());
    }

    [Fact]
    public async Task Titulo_com_taxa_negativa_retorna_400()
    {
        // Arrange
        var request = new
        {
            name = "CDB Banco X",
            issuer = "Banco X",
            assetType = "Cdb",
            indexType = "PreFixed",
            rate = -1,
            issueDate = "2025-01-01",
            maturityDate = "2027-01-01"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/assets", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Titulo_com_asset_type_inexistente_retorna_400()
    {
        // Arrange
        var request = new
        {
            name = "CDB Banco X",
            issuer = "Banco X",
            assetType = "Inexistente",
            indexType = "PreFixed",
            rate = 12,
            issueDate = "2025-01-01",
            maturityDate = "2027-01-01"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/assets", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Titulo_inexistente_retorna_404()
    {
        // Act
        var response = await Client.GetAsync($"/api/assets/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Titulo_com_aportes_nao_pode_ser_removido()
    {
        // Arrange
        var assetId = await CriarTituloAsync();
        await Client.PostAsJsonAsync("/api/positions", new
        {
            assetId,
            investedAmount = 1000,
            applicationDate = "2025-06-01"
        });

        // Act
        var response = await Client.DeleteAsync($"/api/assets/{assetId}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Remover_titulo_sem_aportes_responde_204_sem_corpo_e_sem_erro_no_log()
    {
        // Arrange
        var assetId = await CriarTituloAsync();

        // Act
        var response = await Client.DeleteAsync($"/api/assets/{assetId}");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(string.Empty, body);
        Assert.Empty(_logs.Errors);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/assets/{assetId}")).StatusCode);
    }

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.AddSingleton<ILoggerProvider>(_logs);
    }

    private async Task<Guid> CriarTituloAsync()
    {
        var response = await Client.PostAsJsonAsync("/api/assets", CriarRequestValido());
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    // Guarda as mensagens de nível Error ou acima, para provar que o caminho feliz não loga erro.
    private sealed class ErrorLogProviderStub : ILoggerProvider
    {
        private readonly List<string> _errors = new();

        public IReadOnlyList<string> Errors
        {
            get
            {
                lock (_errors)
                {
                    return _errors.ToList();
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => new Logger(this);

        public void Dispose()
        {
        }

        private sealed class Logger : ILogger
        {
            private readonly ErrorLogProviderStub _owner;

            public Logger(ErrorLogProviderStub owner)
            {
                _owner = owner;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (logLevel < LogLevel.Error)
                {
                    return;
                }

                lock (_owner._errors)
                {
                    _owner._errors.Add(formatter(state, exception));
                }
            }
        }
    }
}
