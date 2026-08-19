using System.Net;
using System.Net.Http.Json;

namespace FixedIncome.Api.Tests;

public class PositionProjectionFlowTests : ApiTestBase
{
    [Fact]
    public async Task Fluxo_completo_cria_titulo_aporte_e_obtem_projecao_coerente()
    {
        // Arrange
        var assetRequest = new
        {
            name = "CDB Banco X",
            issuer = "Banco X",
            assetType = "Cdb",
            indexType = "PreFixed",
            rate = 10,
            issueDate = "2025-01-01",
            maturityDate = "2027-01-01"
        };
        var assetResponse = await Client.PostAsJsonAsync("/api/assets", assetRequest);
        using var assetJson = await ReadJsonAsync(assetResponse);
        var assetId = assetJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();

        var positionRequest = new
        {
            assetId,
            investedAmount = 1000,
            applicationDate = "2025-01-01"
        };

        // Act
        var positionResponse = await Client.PostAsJsonAsync("/api/positions", positionRequest);
        using var positionJson = await ReadJsonAsync(positionResponse);
        var positionId = positionJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();

        var projectionResponse = await Client.GetAsync(
            $"/api/positions/{positionId}/projection?referenceDate=2026-01-01");

        // Assert
        Assert.Equal(HttpStatusCode.Created, positionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, projectionResponse.StatusCode);

        using var projectionJson = await ReadJsonAsync(projectionResponse);
        var data = projectionJson.RootElement.GetProperty("data");
        Assert.Equal(365, data.GetProperty("elapsedDays").GetInt32());
        Assert.Equal(1_100.00m, data.GetProperty("grossAmount").GetDecimal());
        Assert.Equal(1_082.50m, data.GetProperty("netAmount").GetDecimal());
    }
}
