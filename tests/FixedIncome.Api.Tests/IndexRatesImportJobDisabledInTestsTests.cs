using FixedIncome.Infrastructure.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FixedIncome.Api.Tests;

public class IndexRatesImportJobDisabledInTestsTests : ApiTestBase
{
    [Fact]
    public void Fabrica_de_testes_sobe_sem_o_job_de_importacao_registrado()
    {
        // Arrange
        // O host já subiu no construtor da base; se o job estivesse ligado, teria iniciado
        // e chamado o Banco Central.
        var services = Services;

        // Act
        var job = services.GetService<IndexRatesImportJob>();
        var hostedServices = services.GetServices<IHostedService>();

        // Assert
        Assert.Null(job);
        Assert.DoesNotContain(hostedServices, s => s is IndexRatesImportJob);
    }
}
