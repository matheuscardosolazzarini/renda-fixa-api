using System.Net;
using System.Net.Http.Json;

namespace FixedIncome.Api.Tests;

public class PortfolioSummaryFlowTests : ApiTestBase
{
    [Fact]
    public async Task Resumo_da_carteira_fecha_com_a_soma_das_projecoes_individuais()
    {
        // Arrange
        var cdbRequest = new
        {
            name = "CDB Banco X",
            issuer = "Banco X",
            assetType = "Cdb",
            indexType = "PreFixed",
            rate = 10,
            issueDate = "2025-01-01",
            maturityDate = "2027-01-01"
        };
        var lciRequest = new
        {
            name = "LCI Banco Y",
            issuer = "Banco Y",
            assetType = "Lci",
            indexType = "PreFixed",
            rate = 8,
            issueDate = "2025-01-01",
            maturityDate = "2027-01-01"
        };

        var cdbResponse = await Client.PostAsJsonAsync("/api/assets", cdbRequest);
        using var cdbJson = await ReadJsonAsync(cdbResponse);
        var cdbId = cdbJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();

        var lciResponse = await Client.PostAsJsonAsync("/api/assets", lciRequest);
        using var lciJson = await ReadJsonAsync(lciResponse);
        var lciId = lciJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();

        var cdbPositionResponse = await Client.PostAsJsonAsync(
            "/api/positions", new { assetId = cdbId, investedAmount = 1000, applicationDate = "2025-01-01" });
        using var cdbPositionJson = await ReadJsonAsync(cdbPositionResponse);
        var cdbPositionId = cdbPositionJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();

        var lciPositionResponse = await Client.PostAsJsonAsync(
            "/api/positions", new { assetId = lciId, investedAmount = 500, applicationDate = "2025-01-01" });
        using var lciPositionJson = await ReadJsonAsync(lciPositionResponse);
        var lciPositionId = lciPositionJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();

        // Act
        var summaryResponse = await Client.GetAsync("/api/portfolio/summary?referenceDate=2026-01-01");
        var cdbProjectionResponse = await Client.GetAsync(
            $"/api/positions/{cdbPositionId}/projection?referenceDate=2026-01-01");
        var lciProjectionResponse = await Client.GetAsync(
            $"/api/positions/{lciPositionId}/projection?referenceDate=2026-01-01");

        // Assert
        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);

        using var summaryJson = await ReadJsonAsync(summaryResponse);
        var summaryData = summaryJson.RootElement.GetProperty("data");

        using var cdbProjectionJson = await ReadJsonAsync(cdbProjectionResponse);
        using var lciProjectionJson = await ReadJsonAsync(lciProjectionResponse);
        var cdbNetAmount = cdbProjectionJson.RootElement.GetProperty("data").GetProperty("netAmount").GetDecimal();
        var lciNetAmount = lciProjectionJson.RootElement.GetProperty("data").GetProperty("netAmount").GetDecimal();

        Assert.Equal(2, summaryData.GetProperty("positionCount").GetInt32());
        Assert.Equal(cdbNetAmount + lciNetAmount, summaryData.GetProperty("totalNetAmount").GetDecimal());
        var byAssetType = summaryData.GetProperty("byAssetType").EnumerateArray().ToList();
        Assert.Equal(2, byAssetType.Count);
        Assert.Contains(byAssetType, item => item.GetProperty("assetType").GetString() == "Cdb");
        Assert.Contains(byAssetType, item => item.GetProperty("assetType").GetString() == "Lci");
    }
}
