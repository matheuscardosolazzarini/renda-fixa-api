using System.Net;
using System.Net.Http.Json;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Infrastructure.Context;
using Microsoft.Extensions.DependencyInjection;

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
        Assert.Equal(252, data.GetProperty("businessDays").GetInt32());
        Assert.Equal(0m, data.GetProperty("iofRate").GetDecimal());
        Assert.Equal(0m, data.GetProperty("iofAmount").GetDecimal());
        Assert.False(data.GetProperty("isEstimated").GetBoolean());
        Assert.Equal(1_100.00m, data.GetProperty("grossAmount").GetDecimal());
        Assert.Equal(1_082.50m, data.GetProperty("netAmount").GetDecimal());
    }

    [Fact]
    public async Task Projecao_sem_data_de_referencia_usa_o_dia_util_seguinte_a_ultima_cotacao_de_cdi()
    {
        // Arrange: CDI importado até quinta, 2025-04-17; 18/04, 21/04 (feriados) e o fim de semana
        // levam a data padrão a terça, 2025-04-22. Sem depender do relógio nem de rede.
        using (var scope = Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<FixedIncomeDbContext>();
            context.IndexRates.Add(new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));
            await context.SaveChangesAsync();
        }

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

        var positionResponse = await Client.PostAsJsonAsync(
            "/api/positions", new { assetId, investedAmount = 1000, applicationDate = "2025-04-01" });
        using var positionJson = await ReadJsonAsync(positionResponse);
        var positionId = positionJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();

        // Act
        var projectionResponse = await Client.GetAsync($"/api/positions/{positionId}/projection");

        // Assert: 21 dias corridos e 13 úteis, bruto 1.004,93, IOF 1,48, IR 0,78, líquido 1.002,67.
        Assert.Equal(HttpStatusCode.OK, projectionResponse.StatusCode);

        using var projectionJson = await ReadJsonAsync(projectionResponse);
        var data = projectionJson.RootElement.GetProperty("data");
        Assert.Equal(21, data.GetProperty("elapsedDays").GetInt32());
        Assert.Equal(13, data.GetProperty("businessDays").GetInt32());
        Assert.Equal(1_004.93m, data.GetProperty("grossAmount").GetDecimal());
        Assert.Equal(1_002.67m, data.GetProperty("netAmount").GetDecimal());
    }
}
