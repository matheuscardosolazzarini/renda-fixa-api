using System.Net;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Infrastructure.Context;
using Microsoft.Extensions.DependencyInjection;

namespace FixedIncome.Api.Tests;

public class IndexesControllerTests : ApiTestBase
{
    [Fact]
    public async Task Periodo_com_cotacoes_retorna_200_somente_com_as_datas_do_intervalo()
    {
        // Arrange
        await SemearAsync(
            new IndexRate(IndexType.Cdi, new DateOnly(2026, 9, 11), 0.051660m),
            new IndexRate(IndexType.Cdi, new DateOnly(2026, 9, 14), 0.051660m),
            new IndexRate(IndexType.Cdi, new DateOnly(2026, 9, 15), 0.051660m),
            new IndexRate(IndexType.Cdi, new DateOnly(2026, 9, 16), 0.051660m));

        // Act
        var response = await Client.GetAsync("/api/indexes/Cdi?from=2026-09-14&to=2026-09-15");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var data = json.RootElement.GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(2, data.Count);
        Assert.Equal("2026-09-14", data[0].GetProperty("referenceDate").GetString());
        Assert.Equal("2026-09-15", data[1].GetProperty("referenceDate").GetString());
        Assert.Equal(0.051660m, data[0].GetProperty("value").GetDecimal());
        Assert.Equal("Cdi", data[0].GetProperty("indexType").GetString());
    }

    [Fact]
    public async Task Ipca_retorna_200_com_as_datas_no_dia_1_de_cada_mes()
    {
        // Arrange
        await SemearAsync(
            new IndexRate(IndexType.Ipca, new DateOnly(2026, 8, 1), -0.32m),
            new IndexRate(IndexType.Ipca, new DateOnly(2026, 9, 1), 0.82m));

        // Act
        var response = await Client.GetAsync("/api/indexes/Ipca?from=2026-08-01&to=2026-09-30");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var data = json.RootElement.GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(2, data.Count);
        Assert.Equal("2026-08-01", data[0].GetProperty("referenceDate").GetString());
        Assert.Equal(-0.32m, data[0].GetProperty("value").GetDecimal());
        Assert.Equal(0.82m, data[1].GetProperty("value").GetDecimal());
    }

    [Fact]
    public async Task Nome_do_indexador_e_aceito_sem_diferenciar_maiusculas()
    {
        // Arrange
        await SemearAsync(new IndexRate(IndexType.Cdi, new DateOnly(2026, 9, 14), 0.051660m));

        // Act
        var response = await Client.GetAsync("/api/indexes/cdi?from=2026-09-14&to=2026-09-14");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Single(json.RootElement.GetProperty("data").EnumerateArray());
    }

    [Fact]
    public async Task Periodo_sem_cotacoes_retorna_200_com_lista_vazia()
    {
        // Arrange
        await SemearAsync(new IndexRate(IndexType.Cdi, new DateOnly(2026, 9, 14), 0.051660m));

        // Act
        var response = await Client.GetAsync("/api/indexes/Cdi?from=2025-01-01&to=2025-01-31");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(200, json.RootElement.GetProperty("statusCode").GetInt32());
        Assert.Empty(json.RootElement.GetProperty("data").EnumerateArray());
    }

    [Fact]
    public async Task Data_inicial_posterior_a_data_final_retorna_400()
    {
        // Act
        var response = await Client.GetAsync("/api/indexes/Cdi?from=2026-09-15&to=2026-09-14");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(
            "A data inicial não pode ser posterior à data final.",
            json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Indexador_pre_fixado_retorna_400_porque_nao_tem_cotacao()
    {
        // Act
        var response = await Client.GetAsync("/api/indexes/PreFixed?from=2026-09-14&to=2026-09-15");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal("Só há cotação para CDI e IPCA.", json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Indexador_inexistente_retorna_400_com_nome_do_campo_em_portugues()
    {
        // Act
        var response = await Client.GetAsync("/api/indexes/Selic?from=2026-09-14&to=2026-09-15");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal("O valor do campo 'indexType' é inválido.", json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Data_inicial_ausente_retorna_400_informando_o_campo_obrigatorio()
    {
        // Act
        var response = await Client.GetAsync("/api/indexes/Cdi?to=2026-09-15");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal("O campo 'from' é obrigatório.", json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Data_final_ausente_retorna_400_informando_o_campo_obrigatorio()
    {
        // Act
        var response = await Client.GetAsync("/api/indexes/Cdi?from=2026-09-14");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal("O campo 'to' é obrigatório.", json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Data_em_formato_diferente_de_yyyy_mm_dd_retorna_400()
    {
        // Act
        var response = await Client.GetAsync("/api/indexes/Cdi?from=14/09/2026&to=2026-09-15");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal("O valor do campo 'from' é inválido.", json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Erro_de_validacao_mantem_o_envelope_de_ApiResponse()
    {
        // Act
        var response = await Client.GetAsync("/api/indexes/Selic?from=2026-09-14&to=2026-09-15");

        // Assert
        using var json = await ReadJsonAsync(response);
        var root = json.RootElement;
        Assert.Equal(400, root.GetProperty("statusCode").GetInt32());
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.False(root.TryGetProperty("type", out _));
        Assert.False(root.TryGetProperty("title", out _));
    }

    private async Task SemearAsync(params IndexRate[] rates)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FixedIncomeDbContext>();
        context.IndexRates.AddRange(rates);
        await context.SaveChangesAsync();
    }
}
