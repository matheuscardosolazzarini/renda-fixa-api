using System.Net;
using System.Text.Json;
using FixedIncome.Tests.Shared;

namespace FixedIncome.Api.Tests;

// O front gera o schema.ts a partir de /swagger/v1/swagger.json: os campos novos do contrato
// precisam estar lá, e não só na resposta.
[Collection("PostgreSQL")]
public class SwaggerContractTests(PostgreSqlFixture postgres) : ApiTestBase(postgres)
{
    [Fact]
    public async Task Swagger_descreve_os_campos_novos_da_projecao_e_do_resumo_com_o_item_de_positions_apontando_para_projecao_e_titulo()
    {
        // Arrange: a API de teste no ar, sem dados cadastrados (o swagger independe deles).
        // Act
        var response = await Client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var schemas = json.RootElement.GetProperty("components").GetProperty("schemas");

        var projection = schemas.GetProperty("PositionProjectionResponse").GetProperty("properties");
        Assert.Equal("date", projection.GetProperty("referenceDate").GetProperty("format").GetString());
        Assert.Equal("date", projection.GetProperty("applicationDate").GetProperty("format").GetString());

        var summary = schemas.GetProperty("PortfolioSummaryResponse").GetProperty("properties");
        Assert.Equal("date", summary.GetProperty("referenceDate").GetProperty("format").GetString());
        var positions = summary.GetProperty("positions");
        Assert.Equal("array", positions.GetProperty("type").GetString());
        var itemSchemaName = SchemaName(positions.GetProperty("items"));
        Assert.Equal("PortfolioPositionResponse", itemSchemaName);

        var item = schemas.GetProperty(itemSchemaName).GetProperty("properties");
        Assert.Equal("PositionProjectionResponse", SchemaName(item.GetProperty("projection")));
        Assert.Equal("AssetResponse", SchemaName(item.GetProperty("asset")));
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
