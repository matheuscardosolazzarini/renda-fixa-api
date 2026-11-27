using System.Net;
using System.Text.Json;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Infrastructure.Context;
using FixedIncome.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace FixedIncome.Api.Tests;

// F12, RN-13: variação acumulada de um indexador. Só CDI: o IPCA acumulado não depende de "hoje",
// mas o cenário do caso de referência 14 já cobre o contrato JSON.
[Collection("PostgreSQL")]
public class IndexAccumulatedEndpointTests(PostgreSqlFixture postgres) : ApiTestBase(postgres)
{
    // Caso 14 (caso 4 da SPEC-F7): 0,051660 de 01/09 a 16/09 e 0,050788 de 17/09 a 30/09/2026; 07/09
    // é feriado. Acumulado 1,01081673, percentual 1,081673.
    [Fact]
    public async Task Acumulado_do_cdi_retorna_200_com_o_contrato_e_o_percentual_com_6_casas()
    {
        // Arrange
        await SemearCdiDeSetembroAsync();

        // Act
        var response = await Client.GetAsync("/api/indexes/Cdi/accumulated?from=2026-09-01&to=2026-10-01");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        var data = json.RootElement.GetProperty("data");
        Assert.Equal("Cdi", data.GetProperty("indexType").GetString());
        Assert.Equal("2026-09-01", data.GetProperty("from").GetString());
        Assert.Equal("2026-10-01", data.GetProperty("to").GetString());
        Assert.Equal("2026-10-01", data.GetProperty("effectiveTo").GetString());
        Assert.Equal("1.081673", data.GetProperty("accumulatedPercentage").GetRawText());
    }

    [Fact]
    public async Task Acumulado_do_cdi_com_to_depois_da_ultima_cotacao_devolve_effective_to_em_vez_de_erro()
    {
        // Arrange: a última cotação é 30/09; o dia útil seguinte é 01/10.
        await SemearCdiDeSetembroAsync();

        // Act
        var response = await Client.GetAsync("/api/indexes/Cdi/accumulated?from=2026-09-01&to=2026-10-15");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var data = json.RootElement.GetProperty("data");
        Assert.Equal("2026-10-15", data.GetProperty("to").GetString());
        Assert.Equal("2026-10-01", data.GetProperty("effectiveTo").GetString());
        Assert.Equal("1.081673", data.GetProperty("accumulatedPercentage").GetRawText());
    }

    [Fact]
    public async Task Acumulado_sem_from_retorna_400_informando_o_campo_obrigatorio()
    {
        // Act
        var response = await Client.GetAsync("/api/indexes/Cdi/accumulated?to=2026-10-01");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal("O campo 'from' é obrigatório.", json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Acumulado_sem_to_retorna_400_informando_o_campo_obrigatorio()
    {
        // Act
        var response = await Client.GetAsync("/api/indexes/Cdi/accumulated?from=2026-09-01");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal("O campo 'to' é obrigatório.", json.RootElement.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("99")]
    public async Task Acumulado_com_indexador_numerico_na_rota_retorna_400(string numero)
    {
        // Arrange
        await SemearCdiDeSetembroAsync();

        // Act
        var response = await Client.GetAsync($"/api/indexes/{numero}/accumulated?from=2026-09-01&to=2026-10-01");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal("O valor do campo 'indexType' é inválido.", json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Acumulado_de_periodo_sem_cotacao_publicada_retorna_400()
    {
        // Act
        var response = await Client.GetAsync("/api/indexes/Cdi/accumulated?from=2026-09-01&to=2026-10-01");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(
            "Não há cotação de CDI publicada no período de 2026-09-01 a 2026-10-01.",
            json.RootElement.GetProperty("message").GetString());
    }

    // Caso A04 do QA da F12: 24/09 tem cotação, mas com o fim exclusivo [24/09, 24/09) é vazio.
    [Fact]
    public async Task Acumulado_do_cdi_com_from_igual_a_to_num_dia_com_cotacao_retorna_400_de_periodo_vazio()
    {
        // Arrange
        await SemearCdiDeSetembroAsync();

        // Act
        var response = await Client.GetAsync("/api/indexes/Cdi/accumulated?from=2026-09-24&to=2026-09-24");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(
            "O período de 2026-09-24 a 2026-09-24 está vazio: no CDI a data final não entra no "
            + "acumulado, então to precisa ser posterior a from.",
            json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Acumulado_do_indexador_pre_fixado_retorna_400()
    {
        // Act
        var response = await Client.GetAsync("/api/indexes/PreFixed/accumulated?from=2026-09-01&to=2026-10-01");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal("Só há cotação para CDI e IPCA.", json.RootElement.GetProperty("message").GetString());
    }

    private async Task SemearCdiDeSetembroAsync()
    {
        var days = new (int Day, decimal Value)[]
        {
            (1, 0.051660m), (2, 0.051660m), (3, 0.051660m), (4, 0.051660m), (8, 0.051660m),
            (9, 0.051660m), (10, 0.051660m), (11, 0.051660m), (14, 0.051660m), (15, 0.051660m),
            (16, 0.051660m), (17, 0.050788m), (18, 0.050788m), (21, 0.050788m), (22, 0.050788m),
            (23, 0.050788m), (24, 0.050788m), (25, 0.050788m), (28, 0.050788m), (29, 0.050788m),
            (30, 0.050788m)
        };

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FixedIncomeDbContext>();
        context.IndexRates.AddRange(
            days.Select(d => new IndexRate(IndexType.Cdi, new DateOnly(2026, 9, d.Day), d.Value)));
        await context.SaveChangesAsync();
    }
}
