using FixedIncome.Infrastructure;
using FixedIncome.Infrastructure.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FixedIncome.Api.Tests;

public class IndexRatesImportJobCompositionTests
{
    [Fact]
    public void Job_fica_registrado_quando_Enabled_e_true()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = BuildConfiguration("true");

        // Act
        services.AddIndexRatesImportJob(configuration);

        // Assert
        Assert.Contains(services, d => d.ServiceType == typeof(IndexRatesImportJob));
        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService));
    }

    [Theory]
    [InlineData("false")]
    [InlineData("valor-invalido")]
    [InlineData(null)]
    public void Job_nao_e_registrado_quando_Enabled_nao_e_true(string? enabled)
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(enabled);

        // Act
        services.AddIndexRatesImportJob(configuration);

        // Assert
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IndexRatesImportJob));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IHostedService));
    }

    private static IConfiguration BuildConfiguration(string? enabled)
    {
        var values = new Dictionary<string, string?>();

        if (enabled is not null)
        {
            values["IndexRatesImport:Enabled"] = enabled;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
