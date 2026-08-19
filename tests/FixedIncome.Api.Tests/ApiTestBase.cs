using System.Text.Json;
using FixedIncome.Infrastructure.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FixedIncome.Api.Tests;

public abstract class ApiTestBase : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    protected HttpClient Client { get; }

    protected ApiTestBase()
    {
        // O nome do banco é gerado uma única vez fora do delegate: UseInMemoryDatabase é
        // reavaliado a cada escopo (a cada requisição HTTP), e um Guid gerado dentro dele
        // criaria um banco novo e desconectado por requisição.
        var databaseName = Guid.NewGuid().ToString();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<FixedIncomeDbContext>));

                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<FixedIncomeDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
            });
        });

        Client = _factory.CreateClient();
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
        GC.SuppressFinalize(this);
    }
}
