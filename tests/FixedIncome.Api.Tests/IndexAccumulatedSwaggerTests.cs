using System.Net;
using FixedIncome.Tests.Shared;

namespace FixedIncome.Api.Tests;

// O front gera o schema.ts a partir de /swagger/v1/swagger.json: a rota e o schema do acumulado
// (RN-13) precisam estar lá, e não só na resposta.
[Collection("PostgreSQL")]
public class IndexAccumulatedSwaggerTests(PostgreSqlFixture postgres) : ApiTestBase(postgres)
{
    [Fact]
    public async Task Swagger_descreve_a_rota_do_acumulado_com_from_e_to_obrigatorios_e_o_schema_da_resposta()
    {
        // Arrange: a API de teste no ar, sem dados cadastrados (o swagger independe deles).
        // Act
        var response = await Client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);

        var operation = json.RootElement.GetProperty("paths")
            .GetProperty("/api/Indexes/{indexType}/accumulated")
            .GetProperty("get");
        var parameters = operation.GetProperty("parameters").EnumerateArray().ToList();
        foreach (var name in new[] { "from", "to" })
        {
            var parameter = Assert.Single(parameters, p => p.GetProperty("name").GetString() == name);
            Assert.Equal("query", parameter.GetProperty("in").GetString());
            Assert.True(parameter.GetProperty("required").GetBoolean());
            Assert.Equal("date", parameter.GetProperty("schema").GetProperty("format").GetString());
        }

        var responses = operation.GetProperty("responses");
        Assert.True(responses.TryGetProperty("200", out _));
        Assert.True(responses.TryGetProperty("400", out _));

        var schema = json.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty("IndexAccumulationResponse").GetProperty("properties");
        Assert.Equal(
            ["indexType", "from", "to", "effectiveTo", "accumulatedPercentage"],
            schema.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal("date", schema.GetProperty("from").GetProperty("format").GetString());
        Assert.Equal("date", schema.GetProperty("to").GetProperty("format").GetString());
        Assert.Equal("date", schema.GetProperty("effectiveTo").GetProperty("format").GetString());
        Assert.Equal("number", schema.GetProperty("accumulatedPercentage").GetProperty("type").GetString());
        Assert.Equal("double", schema.GetProperty("accumulatedPercentage").GetProperty("format").GetString());
    }
}
