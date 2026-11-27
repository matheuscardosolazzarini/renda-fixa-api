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
        Assert.Equal("boolean", projection.GetProperty("isProjected").GetProperty("type").GetString());

        var summary = schemas.GetProperty("PortfolioSummaryResponse").GetProperty("properties");
        Assert.Equal("date", summary.GetProperty("referenceDate").GetProperty("format").GetString());
        Assert.Equal("boolean", summary.GetProperty("isProjected").GetProperty("type").GetString());
        var positions = summary.GetProperty("positions");
        Assert.Equal("array", positions.GetProperty("type").GetString());
        var itemSchemaName = SchemaName(positions.GetProperty("items"));
        Assert.Equal("PortfolioPositionResponse", itemSchemaName);

        var item = schemas.GetProperty(itemSchemaName).GetProperty("properties");
        Assert.Equal("PositionProjectionResponse", SchemaName(item.GetProperty("projection")));
        Assert.Equal("AssetResponse", SchemaName(item.GetProperty("asset")));
    }

    // Prova "só aditivo": a lista fechada de propriedades, com tipo e formato, dos dois DTOs que já
    // existiam. Qualquer campo renomeado, removido ou com tipo diferente quebra o contrato do front.
    [Fact]
    public async Task Swagger_mantem_a_lista_fechada_de_propriedades_da_projecao_e_do_resumo_com_tipo_e_formato()
    {
        // Arrange: a API de teste no ar, sem dados cadastrados (o swagger independe deles).
        // Act
        var response = await Client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var schemas = json.RootElement.GetProperty("components").GetProperty("schemas");

        var projection = schemas.GetProperty("PositionProjectionResponse").GetProperty("properties");
        Assert.Equal(
            [
                "positionId: string/uuid", "assetId: string/uuid", "investedAmount: number/double",
                "grossAmount: number/double", "grossYield: number/double", "taxRate: number/double",
                "taxAmount: number/double", "netAmount: number/double", "elapsedDays: integer/int32",
                "businessDays: integer/int32", "iofRate: number/double", "iofAmount: number/double",
                "isEstimated: boolean", "referenceDate: string/date", "applicationDate: string/date",
                "isProjected: boolean"
            ],
            Describe(projection));

        var summary = schemas.GetProperty("PortfolioSummaryResponse").GetProperty("properties");
        Assert.Equal(
            [
                "totalInvestedAmount: number/double", "totalGrossAmount: number/double",
                "totalGrossYield: number/double", "totalTaxAmount: number/double",
                "totalIofAmount: number/double", "totalNetAmount: number/double",
                "positionCount: integer/int32", "byAssetType: array<AssetTypeSummaryResponse>",
                "referenceDate: string/date", "positions: array<PortfolioPositionResponse>",
                "isProjected: boolean"
            ],
            Describe(summary));
    }

    // "nome: tipo/formato" de cada propriedade, na ordem do schema; array vira "array<schema do item>".
    private static string[] Describe(JsonElement properties) =>
        properties.EnumerateObject().Select(p =>
        {
            var type = p.Value.GetProperty("type").GetString();
            if (type == "array")
            {
                return $"{p.Name}: array<{SchemaName(p.Value.GetProperty("items"))}>";
            }

            return p.Value.TryGetProperty("format", out var format)
                ? $"{p.Name}: {type}/{format.GetString()}"
                : $"{p.Name}: {type}";
        }).ToArray();

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
