using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Infrastructure.Context;
using FixedIncome.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace FixedIncome.Api.Tests;

[Collection("PostgreSQL")]
public class PortfolioSummaryFlowTests(PostgreSqlFixture postgres) : ApiTestBase(postgres)
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
        Assert.Equal(0m, summaryData.GetProperty("totalIofAmount").GetDecimal());
        var byAssetType = summaryData.GetProperty("byAssetType").EnumerateArray().ToList();
        Assert.Equal(2, byAssetType.Count);
        Assert.Contains(byAssetType, item => item.GetProperty("assetType").GetString() == "Cdb");
        Assert.Contains(byAssetType, item => item.GetProperty("assetType").GetString() == "Lci");
    }

    [Fact]
    public async Task Resumo_com_data_informada_devolve_a_data_de_referencia_no_formato_iso()
    {
        // Arrange
        await CriarTituloEAporteAsync("CDB Banco X", 1000, "2025-04-01");

        // Act
        var response = await Client.GetAsync("/api/portfolio/summary?referenceDate=2025-04-22");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal("2025-04-22", json.RootElement.GetProperty("data").GetProperty("referenceDate").GetString());
    }

    [Fact]
    public async Task Resumo_sem_data_com_cdi_gravado_devolve_a_data_padrao_na_raiz_e_o_aporte_posterior_entra_com_rendimento_zero_na_data_do_proprio_aporte()
    {
        // Arrange: CDI até quinta, 2025-04-17; a data padrão é terça, 2025-04-22. O segundo aporte
        // (2025-05-05) é posterior a ela e rende zero na própria data do aporte (D-3).
        using (var scope = Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<FixedIncomeDbContext>();
            context.IndexRates.Add(new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));
            await context.SaveChangesAsync();
        }

        var oldAssetId = await CriarTituloAsync("CDB Banco X", "Banco X", "Cdb", 10);
        var recentAssetId = await CriarTituloAsync("CDB Banco Z", "Banco Z", "Cdb", 10);
        var oldPosition = await CriarAporteAsync(oldAssetId, 1000, "2025-04-01");
        var recentPosition = await CriarAporteAsync(recentAssetId, 500, "2025-05-05");

        // Act
        var response = await Client.GetAsync("/api/portfolio/summary");

        // Assert: a raiz segue sendo a data padrão, não a do aporte posterior.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var data = json.RootElement.GetProperty("data");
        Assert.Equal("2025-04-22", data.GetProperty("referenceDate").GetString());
        Assert.Equal(2, data.GetProperty("positionCount").GetInt32());
        Assert.Equal(1_502.67m, data.GetProperty("totalNetAmount").GetDecimal());

        var items = data.GetProperty("positions").EnumerateArray().ToList();
        var recent = Assert.Single(items, i =>
            i.GetProperty("projection").GetProperty("positionId").GetGuid() == recentPosition)
            .GetProperty("projection");
        Assert.Equal("2025-05-05", recent.GetProperty("referenceDate").GetString());
        Assert.Equal("2025-05-05", recent.GetProperty("applicationDate").GetString());
        Assert.Equal(500m, recent.GetProperty("netAmount").GetDecimal());

        var old = Assert.Single(items, i =>
            i.GetProperty("projection").GetProperty("positionId").GetGuid() == oldPosition)
            .GetProperty("projection");
        Assert.Equal("2025-04-22", old.GetProperty("referenceDate").GetString());
    }

    [Fact]
    public async Task Resumo_de_carteira_vazia_devolve_a_data_de_referencia_em_formato_iso_e_positions_como_array_vazio_com_e_sem_data_informada()
    {
        // Arrange: nenhuma posição cadastrada.
        // Act
        var informed = await Client.GetAsync("/api/portfolio/summary?referenceDate=2025-04-22");
        var omitted = await Client.GetAsync("/api/portfolio/summary");

        // Assert: sem posições, a data devolvida é a informada.
        Assert.Equal(HttpStatusCode.OK, informed.StatusCode);
        using var informedJson = await ReadJsonAsync(informed);
        var informedData = informedJson.RootElement.GetProperty("data");
        Assert.Equal(0, informedData.GetProperty("positionCount").GetInt32());
        Assert.Equal("2025-04-22", informedData.GetProperty("referenceDate").GetString());

        // Sem data e sem CDI, vale hoje em Brasília: o teste só exige o campo presente e no formato ISO.
        Assert.Equal(HttpStatusCode.OK, omitted.StatusCode);
        using var omittedJson = await ReadJsonAsync(omitted);
        var omittedDate = omittedJson.RootElement.GetProperty("data").GetProperty("referenceDate").GetString();
        Assert.True(
            DateOnly.TryParseExact(omittedDate, "yyyy-MM-dd", out _),
            $"referenceDate fora do formato yyyy-MM-dd: {omittedDate}");

        foreach (var data in new[] { informedData, omittedJson.RootElement.GetProperty("data") })
        {
            var positions = data.GetProperty("positions");
            Assert.Equal(System.Text.Json.JsonValueKind.Array, positions.ValueKind);
            Assert.Equal(0, positions.GetArrayLength());
            Assert.True(data.TryGetProperty("referenceDate", out _));
        }
    }

    [Fact]
    public async Task Resumo_devolve_exatamente_as_propriedades_do_contrato_no_resumo_no_byAssetType_e_no_item_de_positions()
    {
        // Arrange: um título e um aporte cadastrados.
        await CriarTituloEAporteAsync("CDB Banco X", 1000, "2025-04-01");

        // Act
        var response = await Client.GetAsync("/api/portfolio/summary?referenceDate=2025-04-22");

        // Assert: as 8 propriedades antigas mais referenceDate e positions.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var data = json.RootElement.GetProperty("data");
        var names = data.EnumerateObject().Select(p => p.Name).ToHashSet();
        var expected = new HashSet<string>
        {
            "totalInvestedAmount", "totalGrossAmount", "totalGrossYield", "totalTaxAmount",
            "totalIofAmount", "totalNetAmount", "positionCount", "byAssetType", "referenceDate", "positions"
        };
        Assert.True(names.SetEquals(expected), $"Propriedades de data: {string.Join(", ", names)}");

        var byAssetTypeNames = data.GetProperty("byAssetType")[0].EnumerateObject().Select(p => p.Name).ToHashSet();
        var expectedByAssetType = new HashSet<string>
        {
            "assetType", "positionCount", "totalInvestedAmount", "totalNetAmount", "percentage"
        };
        Assert.True(
            byAssetTypeNames.SetEquals(expectedByAssetType),
            $"Propriedades de byAssetType: {string.Join(", ", byAssetTypeNames)}");

        var itemNames = data.GetProperty("positions")[0].EnumerateObject().Select(p => p.Name).ToHashSet();
        var expectedItem = new HashSet<string> { "projection", "asset" };
        Assert.True(
            itemNames.SetEquals(expectedItem),
            $"Propriedades do item de positions: {string.Join(", ", itemNames)}");
    }

    [Fact]
    public async Task Resumo_com_data_devolve_positions_com_projection_e_asset_e_cada_projection_e_igual_a_do_endpoint_de_projecao()
    {
        // Arrange: três posições PreFixed (duas no mesmo CDB e uma na LCI), sem depender de série de IPCA.
        var cdbId = await CriarTituloAsync("CDB Banco X", "Banco X", "Cdb", 10);
        var lciId = await CriarTituloAsync("LCI Banco Y", "Banco Y", "Lci", 8);
        var cdbOne = await CriarAporteAsync(cdbId, 1000, "2025-04-01");
        var cdbTwo = await CriarAporteAsync(cdbId, 1000, "2025-04-01");
        var lci = await CriarAporteAsync(lciId, 500, "2025-04-10");
        var applicationDates = new Dictionary<Guid, string>
        {
            [cdbOne] = "2025-04-01", [cdbTwo] = "2025-04-01", [lci] = "2025-04-10"
        };

        // Act
        var response = await Client.GetAsync("/api/portfolio/summary?referenceDate=2025-04-22");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var data = json.RootElement.GetProperty("data");
        Assert.Equal("2025-04-22", data.GetProperty("referenceDate").GetString());
        var items = data.GetProperty("positions").EnumerateArray().ToList();
        Assert.Equal(3, items.Count);
        Assert.Equal(data.GetProperty("positionCount").GetInt32(), items.Count);

        foreach (var positionId in new[] { cdbOne, cdbTwo, lci })
        {
            // Casado por positionId: a ordem dos itens não é afirmada aqui.
            var item = Assert.Single(items, i =>
                i.GetProperty("projection").GetProperty("positionId").GetGuid() == positionId);
            Assert.True(item.TryGetProperty("projection", out var projection));
            Assert.True(item.TryGetProperty("asset", out var asset));

            Assert.Equal(applicationDates[positionId], projection.GetProperty("applicationDate").GetString());
            Assert.Equal("2025-04-22", projection.GetProperty("referenceDate").GetString());
            Assert.Equal(positionId == lci ? "Lci" : "Cdb", asset.GetProperty("assetType").GetString());
            Assert.Equal("PreFixed", asset.GetProperty("indexType").GetString());
            Assert.Equal(positionId == lci ? lciId : cdbId, asset.GetProperty("id").GetGuid());
            Assert.Equal(asset.GetProperty("id").GetGuid(), projection.GetProperty("assetId").GetGuid());

            // A projection do item é a do endpoint de projeção chamado com a mesma data de referência.
            var itemReferenceDate = projection.GetProperty("referenceDate").GetString();
            var projectionResponse = await Client.GetAsync(
                $"/api/positions/{positionId}/projection?referenceDate={itemReferenceDate}");
            Assert.Equal(HttpStatusCode.OK, projectionResponse.StatusCode);
            using var projectionJson = await ReadJsonAsync(projectionResponse);
            var endpointData = projectionJson.RootElement.GetProperty("data");

            var itemFields = projection.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetRawText());
            var endpointFields = endpointData.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetRawText());
            Assert.Equal(endpointFields, itemFields);
        }

        var projections = items.Select(i => i.GetProperty("projection")).ToList();
        Assert.Equal(data.GetProperty("totalInvestedAmount").GetDecimal(), projections.Sum(p => p.GetProperty("investedAmount").GetDecimal()));
        Assert.Equal(data.GetProperty("totalGrossAmount").GetDecimal(), projections.Sum(p => p.GetProperty("grossAmount").GetDecimal()));
        Assert.Equal(data.GetProperty("totalGrossYield").GetDecimal(), projections.Sum(p => p.GetProperty("grossYield").GetDecimal()));
        Assert.Equal(data.GetProperty("totalTaxAmount").GetDecimal(), projections.Sum(p => p.GetProperty("taxAmount").GetDecimal()));
        Assert.Equal(data.GetProperty("totalIofAmount").GetDecimal(), projections.Sum(p => p.GetProperty("iofAmount").GetDecimal()));
        Assert.Equal(data.GetProperty("totalNetAmount").GetDecimal(), projections.Sum(p => p.GetProperty("netAmount").GetDecimal()));
    }

    private async Task<Guid> CriarTituloAsync(string name, string issuer, string assetType, decimal rate)
    {
        var assetResponse = await Client.PostAsJsonAsync("/api/assets", new
        {
            name,
            issuer,
            assetType,
            indexType = "PreFixed",
            rate,
            issueDate = "2025-01-01",
            maturityDate = "2027-01-01"
        });
        Assert.Equal(HttpStatusCode.Created, assetResponse.StatusCode);
        using var assetJson = await ReadJsonAsync(assetResponse);
        return assetJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    private async Task<Guid> CriarAporteAsync(Guid assetId, decimal investedAmount, string applicationDate)
    {
        var positionResponse = await Client.PostAsJsonAsync(
            "/api/positions", new { assetId, investedAmount, applicationDate });
        Assert.Equal(HttpStatusCode.Created, positionResponse.StatusCode);
        using var positionJson = await ReadJsonAsync(positionResponse);
        return positionJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    private async Task CriarTituloEAporteAsync(string name, decimal investedAmount, string applicationDate)
    {
        var assetResponse = await Client.PostAsJsonAsync("/api/assets", new
        {
            name,
            issuer = "Banco X",
            assetType = "Cdb",
            indexType = "PreFixed",
            rate = 10,
            issueDate = "2025-01-01",
            maturityDate = "2027-01-01"
        });
        Assert.Equal(HttpStatusCode.Created, assetResponse.StatusCode);
        using var assetJson = await ReadJsonAsync(assetResponse);
        var assetId = assetJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();

        var positionResponse = await Client.PostAsJsonAsync(
            "/api/positions", new { assetId, investedAmount, applicationDate });
        Assert.Equal(HttpStatusCode.Created, positionResponse.StatusCode);
    }
}
