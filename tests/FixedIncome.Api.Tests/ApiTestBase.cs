using System.Text.Json;
using FixedIncome.Infrastructure.Context;
using FixedIncome.Tests.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FixedIncome.Api.Tests;

public abstract class ApiTestBase : IDisposable
{
    private readonly PostgreSqlFixture _postgres;
    private readonly string _connectionString;
    private readonly WebApplicationFactory<Program> _factory;
    protected HttpClient Client { get; }
    protected IServiceProvider Services => _factory.Services;

    protected ApiTestBase(PostgreSqlFixture postgres)
    {
        _postgres = postgres;

        // Um banco por teste, copiado do modelo com as migrations aplicadas (o seed de feriados,
        // de que o calendário das projeções depende, já vem nele). A connection string é obtida
        // uma única vez, fora do delegate, que roda a cada escopo (a cada requisição HTTP).
        _connectionString = postgres.CreateDatabase();

        try
        {
            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");

                // O job de importação chama o Banco Central: nos testes fica desligado, para
                // nenhum teste depender da internet.
                builder.UseSetting("IndexRatesImport:Enabled", "false");

                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<FixedIncomeDbContext>));

                    if (descriptor is not null)
                    {
                        services.Remove(descriptor);
                    }

                    // O banco é apontado pelo descritor, não por ConnectionStrings:DefaultConnection:
                    // o Program lê a variável de ambiente antes da configuração, e uma variável
                    // definida na máquina ou no runner venceria o teste.
                    services.AddDbContext<FixedIncomeDbContext>(options =>
                        options.UseNpgsql(_connectionString));

                    ConfigureTestServices(services);
                });
            });

            Client = _factory.CreateClient();
        }
        catch
        {
            // O construtor falhou depois de criar o banco: o Dispose não roda, então derruba o banco aqui.
            _postgres.DropDatabase(_connectionString);
            throw;
        }
    }

    // Ponto de extensão para os testes que precisam substituir um caso de uso por um dublê
    // (ex.: para provocar uma exceção e exercitar o middleware), sem duplicar a configuração
    // do WebApplicationFactory em cada classe de teste.
    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
    }

    protected static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content);
    }

    public void Dispose()
    {
        Client.Dispose();
        _factory.Dispose();
        _postgres.DropDatabase(_connectionString);
        GC.SuppressFinalize(this);
    }
}
