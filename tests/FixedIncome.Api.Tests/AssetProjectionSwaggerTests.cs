using System.Net;
using System.Text.Json;
using FixedIncome.Tests.Shared;

namespace FixedIncome.Api.Tests;

// O front gera o schema.ts a partir de /swagger/v1/swagger.json: a rota e o schema da projeção por
// título (RN-14) precisam estar lá, e não só na resposta.
[Collection("PostgreSQL")]
public class AssetProjectionSwaggerTests(PostgreSqlFixture postgres) : ApiTestBase(postgres)
{
    [Fact]
    public async Task Swagger_descreve_a_rota_da_projecao_por_titulo_e_o_schema_sem_is_projected_na_raiz()
    {
        // Arrange: a API de teste no ar, sem dados cadastrados (o swagger independe deles).
        // Act
        var response = await Client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);

        var operation = json.RootElement.GetProperty("paths")
            .GetProperty("/api/Assets/{id}/projection")
            .GetProperty("get");
        var parameters = operation.GetProperty("parameters").EnumerateArray().ToList();
        var referenceDate = Assert.Single(parameters, p => p.GetProperty("name").GetString() == "referenceDate");
        Assert.Equal("query", referenceDate.GetProperty("in").GetString());
        Assert.False(referenceDate.TryGetProperty("required", out var required) && required.GetBoolean());
        Assert.Equal("date", referenceDate.GetProperty("schema").GetProperty("format").GetString());

        var responses = operation.GetProperty("responses");
        Assert.True(responses.TryGetProperty("200", out _));
        Assert.True(responses.TryGetProperty("400", out _));
        Assert.True(responses.TryGetProperty("404", out _));

        var schemas = json.RootElement.GetProperty("components").GetProperty("schemas");
        var properties = schemas.GetProperty("AssetProjectionResponse").GetProperty("properties");
        Assert.Equal(
            [
                "assetId", "totalInvestedAmount", "totalGrossAmount", "totalGrossYield", "totalTaxAmount",
                "totalIofAmount", "totalNetAmount", "positionCount", "referenceDate", "positions", "totalNetYield"
            ],
            properties.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.False(properties.TryGetProperty("isProjected", out _));
        Assert.Equal("date", properties.GetProperty("referenceDate").GetProperty("format").GetString());
        Assert.Equal("integer", properties.GetProperty("positionCount").GetProperty("type").GetString());
        foreach (var name in new[]
        {
            "totalInvestedAmount", "totalGrossAmount", "totalGrossYield", "totalTaxAmount", "totalIofAmount",
            "totalNetAmount", "totalNetYield"
        })
        {
            Assert.Equal("number", properties.GetProperty(name).GetProperty("type").GetString());
            Assert.Equal("double", properties.GetProperty(name).GetProperty("format").GetString());
        }

        var positions = properties.GetProperty("positions");
        Assert.Equal("array", positions.GetProperty("type").GetString());
        Assert.Equal("PositionProjectionResponse", SchemaName(positions.GetProperty("items")));
    }

    // Nome do schema referenciado, direto ($ref) ou embrulhado em allOf.
    private static string SchemaName(JsonElement schema)
    {
        if (!schema.TryGetProperty("$ref", out var reference))
        {
            reference = schema.GetProperty("allOf")[0].GetProperty("$ref");
        }

        return reference.GetString()!.Split('/').Last();
    }
}
