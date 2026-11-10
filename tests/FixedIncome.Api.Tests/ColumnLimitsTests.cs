using FixedIncome.Tests.Shared;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace FixedIncome.Api.Tests;

// B-01 e B-07: valores que o banco não guarda (numeric(18,6) e numeric(18,2)) devem voltar 400
// com mensagem útil, nunca 500. O banco real impõe a precisão da coluna (um valor acima do limite
// estoura ao gravar), então estes testes continuam provando que a recusa vem do domínio, antes de
// tocar o banco.
[Collection("PostgreSQL")]
public class ColumnLimitsTests(PostgreSqlFixture postgres) : ApiTestBase(postgres)
{
    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private async Task<Guid> CriarTituloAsync()
    {
        var response = await Client.PostAsJsonAsync("/api/assets", new
        {
            name = "CDB Banco X",
            issuer = "Banco X",
            assetType = "Cdb",
            indexType = "PreFixed",
            rate = 12,
            issueDate = "2026-01-01",
            maturityDate = "2028-01-01"
        });
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Cadastro_de_titulo_com_taxa_1e15_retorna_400_e_nao_500()
    {
        // Arrange
        var body = """
            {"name":"QA-X bigrate2","issuer":"x","assetType":"Cdb","indexType":"PreFixed","rate":1e15,
             "issueDate":"2026-01-01","maturityDate":"2027-01-01"}
            """;

        // Act
        var response = await Client.PostAsync("/api/assets", Json(body));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Contains("Rate deve ser menor que 1000000000000", json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Atualizacao_de_titulo_com_taxa_1e15_retorna_400_e_nao_500()
    {
        // Arrange
        var id = await CriarTituloAsync();
        var body = """
            {"name":"QA-12 put2","issuer":"y","rate":1e15,"issueDate":"2026-01-01","maturityDate":"2028-01-01"}
            """;

        // Act
        var response = await Client.PutAsync($"/api/assets/{id}", Json(body));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Contains("Rate deve ser menor que 1000000000000", json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Aporte_com_valor_1e16_retorna_400_e_nao_500()
    {
        // Arrange
        var id = await CriarTituloAsync();
        var body = $$"""{"assetId":"{{id}}","investedAmount":1e16,"applicationDate":"2026-10-01"}""";

        // Act
        var response = await Client.PostAsync("/api/positions", Json(body));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Contains(
            "InvestedAmount deve ser menor que 10000000000000000", json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Cadastro_de_titulo_com_taxa_de_mais_de_seis_casas_retorna_400()
    {
        // Arrange
        var body = """
            {"name":"QA-X","issuer":"x","assetType":"Cdb","indexType":"PreFixed","rate":12.123456789,
             "issueDate":"2026-01-01","maturityDate":"2027-01-01"}
            """;

        // Act
        var response = await Client.PostAsync("/api/assets", Json(body));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Aporte_com_mais_de_duas_casas_retorna_400()
    {
        // Arrange
        var id = await CriarTituloAsync();
        var body = $$"""{"assetId":"{{id}}","investedAmount":10.999,"applicationDate":"2026-10-01"}""";

        // Act
        var response = await Client.PostAsync("/api/positions", Json(body));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // B-02 de ponta a ponta: o aporte fora do calendário é recusado já no POST.
    [Fact]
    public async Task Aporte_fora_do_calendario_retorna_400_com_o_periodo_coberto()
    {
        // Arrange
        var assetResponse = await Client.PostAsJsonAsync("/api/assets", new
        {
            name = "QA-03 PRE10 2019-2032",
            issuer = "QA Issuer",
            assetType = "Cdb",
            indexType = "PreFixed",
            rate = 10,
            issueDate = "2019-01-01",
            maturityDate = "2032-01-01"
        });
        using var assetJson = await ReadJsonAsync(assetResponse);
        var assetId = assetJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();

        // Act
        var response = await Client.PostAsJsonAsync("/api/positions", new
        {
            assetId,
            investedAmount = 1000,
            applicationDate = "2019-06-03"
        });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(
            "Data de aporte 2019-06-03 fora do período coberto pelo calendário (2020-01-01 a 2030-12-31).",
            json.RootElement.GetProperty("message").GetString());
    }
}
