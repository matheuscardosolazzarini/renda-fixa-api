using FixedIncome.Application.Providers;
using FixedIncome.Application.Repositories;
using FixedIncome.Infrastructure.Jobs;
using FixedIncome.Infrastructure.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
