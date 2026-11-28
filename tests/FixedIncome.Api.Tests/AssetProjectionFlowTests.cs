using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FixedIncome.Tests.Shared;

namespace FixedIncome.Api.Tests;

// F12, RN-14: GET /api/assets/{id}/projection contra o PostgreSQL do Testcontainers, só com PreFixed.
[Collection("PostgreSQL")]
public class AssetProjectionFlowTests(PostgreSqlFixture postgres) : ApiTestBase(postgres)
{
    // Caso 29: dois aportes do mesmo CDB PreFixed 10% a.a., R$ 1.000 cada, de 2025-04-01 a 2025-04-22.
    [Fact]
    public async Task Projecao_do_titulo_devolve_o_contrato_com_os_totais_do_caso_de_referencia()
    {
        // Arrange
        var assetId = await CriarTituloAsync("2027-01-01");
        var firstId = await CriarAporteAsync(assetId, "2025-04-01");
        var secondId = await CriarAporteAsync(assetId, "2025-04-01");

        // Act
        var response = await Client.GetAsync($"/api/assets/{assetId}/projection?referenceDate=2025-04-22");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(
            [
                "assetId", "totalInvestedAmount", "totalGrossAmount", "totalGrossYield", "totalTaxAmount",
                "totalIofAmount", "totalNetAmount", "positionCount", "referenceDate", "positions", "totalNetYield"
            ],
            data.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(assetId, data.GetProperty("assetId").GetGuid());
        Assert.Equal(2_000m, data.GetProperty("totalInvestedAmount").GetDecimal());
        Assert.Equal(2_009.86m, data.GetProperty("totalGrossAmount").GetDecimal());
        Assert.Equal(9.86m, data.GetProperty("totalGrossYield").GetDecimal());
        Assert.Equal(1.56m, data.GetProperty("totalTaxAmount").GetDecimal());
        Assert.Equal(2.96m, data.GetProperty("totalIofAmount").GetDecimal());
        Assert.Equal(2_005.34m, data.GetProperty("totalNetAmount").GetDecimal());
        Assert.Equal("5.34", data.GetProperty("totalNetYield").GetRawText());
        Assert.Equal(2, data.GetProperty("positionCount").GetInt32());
        Assert.Equal("2025-04-22", data.GetProperty("referenceDate").GetString());

        var items = data.GetProperty("positions").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equivalent(
            new[] { firstId, secondId }, items.Select(i => i.GetProperty("positionId").GetGuid()).ToArray());
        Assert.All(items, item =>
        {
            Assert.Equal(assetId, item.GetProperty("assetId").GetGuid());
            Assert.Equal(1_004.93m, item.GetProperty("grossAmount").GetDecimal());
            Assert.Equal(1_002.67m, item.GetProperty("netAmount").GetDecimal());
            Assert.Equal("2.67", item.GetProperty("netYield").GetRawText());
            Assert.False(item.GetProperty("isProjected").GetBoolean());
            Assert.Equal("2025-04-22", item.GetProperty("referenceDate").GetString());
            Assert.Equal("2025-04-01", item.GetProperty("applicationDate").GetString());
        });
    }

    [Fact]
    public async Task Projecao_do_titulo_devolve_os_aportes_do_mais_recente_para_o_mais_antigo()
    {
        // Arrange
        var assetId = await CriarTituloAsync("2027-01-01");
        var oldId = await CriarAporteAsync(assetId, "2025-04-01");
        var recentId = await CriarAporteAsync(assetId, "2025-04-10");

        // Act
        var response = await Client.GetAsync($"/api/assets/{assetId}/projection?referenceDate=2025-04-22");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var ids = json.RootElement.GetProperty("data").GetProperty("positions").EnumerateArray()
            .Select(i => i.GetProperty("positionId").GetGuid()).ToArray();
        Assert.Equal(new[] { recentId, oldId }, ids);
    }

    [Fact]
    public async Task Projecao_de_titulo_inexistente_retorna_404_com_data_nulo()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/api/assets/{id}/projection?referenceDate=2025-04-22");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal($"Título com id {id} não encontrado.", json.RootElement.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("data").ValueKind);
    }

    // O vencimento é depois de 2050 de propósito: a data de cálculo é limitada ao vencimento (RN-02),
    // então um título que vence dentro da cobertura nunca consulta o calendário na data de 2051.
    [Fact]
    public async Task Projecao_com_data_posterior_a_cobertura_do_calendario_retorna_400_com_data_nulo()
    {
        // Arrange
        var assetId = await CriarTituloAsync("2060-01-01");
        await CriarAporteAsync(assetId, "2026-10-01");

        // Act
        var response = await Client.GetAsync($"/api/assets/{assetId}/projection?referenceDate=2051-06-01");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(
            "Data 2051-06-01 fora do período coberto pelo calendário (2020-01-01 a 2050-12-31).",
            json.RootElement.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("data").ValueKind);
    }

    private async Task<Guid> CriarTituloAsync(string maturityDate)
    {
        var response = await Client.PostAsJsonAsync("/api/assets", new
        {
            name = "CDB Banco X",
            issuer = "Banco X",
            assetType = "Cdb",
            indexType = "PreFixed",
            rate = 10,
            issueDate = "2025-01-01",
            maturityDate
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await ReadJsonAsync(response);

        return json.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    private async Task<Guid> CriarAporteAsync(Guid assetId, string applicationDate)
    {
        var response = await Client.PostAsJsonAsync(
            "/api/positions", new { assetId, investedAmount = 1000, applicationDate });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await ReadJsonAsync(response);

        return json.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }
}
