using System.Net;
using FixedIncome.Application.Common;
using FixedIncome.Application.DTOs;
using FixedIncome.Application.UseCases;
using FixedIncome.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FixedIncome.Api.Tests;

public class ExceptionHandlingMiddlewareDomainExceptionTests : ApiTestBase
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<IGetAllAssetsUseCase>();
        services.AddScoped<IGetAllAssetsUseCase, ThrowingDomainExceptionUseCaseStub>();
    }

    [Fact]
    public async Task DomainException_que_escapa_do_caso_de_uso_retorna_400_no_envelope()
    {
        // Act
        var response = await Client.GetAsync("/api/assets");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(400, json.RootElement.GetProperty("statusCode").GetInt32());
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Falha de domínio provocada pelo teste.", json.RootElement.GetProperty("message").GetString());
        Assert.DoesNotContain("StackTrace", content);
        Assert.DoesNotContain("ThrowingDomainExceptionUseCaseStub", content);
    }

    private sealed class ThrowingDomainExceptionUseCaseStub : IGetAllAssetsUseCase
    {
        public Task<ApiResponse<IEnumerable<AssetResponse>>> ExecuteAsync()
        {
            throw new DomainException("Falha de domínio provocada pelo teste.");
        }
    }
}

public class ExceptionHandlingMiddlewareUnhandledExceptionTests : ApiTestBase
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<IGetAllAssetsUseCase>();
        services.AddScoped<IGetAllAssetsUseCase, ThrowingUnhandledExceptionUseCaseStub>();
    }

    [Fact]
    public async Task Excecao_nao_tratada_retorna_500_com_mensagem_generica_sem_detalhe_interno()
    {
        // Act
        var response = await Client.GetAsync("/api/assets");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(500, json.RootElement.GetProperty("statusCode").GetInt32());
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(
            "Ocorreu um erro interno. Tente novamente mais tarde.",
            json.RootElement.GetProperty("message").GetString());
        Assert.DoesNotContain("StackTrace", content);
        Assert.DoesNotContain("InvalidOperationException", content);
        Assert.DoesNotContain("ThrowingUnhandledExceptionUseCaseStub", content);
    }

    private sealed class ThrowingUnhandledExceptionUseCaseStub : IGetAllAssetsUseCase
    {
        public Task<ApiResponse<IEnumerable<AssetResponse>>> ExecuteAsync()
        {
            throw new InvalidOperationException("Detalhe interno que não pode vazar.");
        }
    }
}
