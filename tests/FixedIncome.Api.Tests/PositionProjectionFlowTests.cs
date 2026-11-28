using FixedIncome.Tests.Shared;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Infrastructure.Context;
using Microsoft.Extensions.DependencyInjection;

namespace FixedIncome.Api.Tests;

[Collection("PostgreSQL")]
public class PositionProjectionFlowTests(PostgreSqlFixture postgres) : ApiTestBase(postgres)
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

    // O seed de feriados vai até 2050: a projeção de um título longo com referência em 2035 não pode
    // mais cair na mensagem de fora do período coberto.
    [Fact]
    public async Task Projecao_com_data_de_referencia_em_2035_retorna_200()
    {
        // Arrange
        var positionId = await CriarTituloEAporteAsync(maturityDate: "2040-01-01", applicationDate: "2026-10-01");

        // Act
        var response = await Client.GetAsync($"/api/positions/{positionId}/projection?referenceDate=2035-06-01");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(3165, json.RootElement.GetProperty("data").GetProperty("elapsedDays").GetInt32());
    }

    // O vencimento é depois de 2050 de propósito: a data de cálculo é limitada ao vencimento (RN-02),
    // então um título que vence dentro da cobertura nunca consulta o calendário na data de 2051.
    [Fact]
    public async Task Projecao_com_data_de_referencia_apos_2050_retorna_400_com_o_periodo_coberto()
    {
        // Arrange
        var positionId = await CriarTituloEAporteAsync(maturityDate: "2060-01-01", applicationDate: "2026-10-01");

        // Act
        var response = await Client.GetAsync($"/api/positions/{positionId}/projection?referenceDate=2051-06-01");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(
            "Data 2051-06-01 fora do período coberto pelo calendário (2020-01-01 a 2050-12-31).",
            json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Projecao_com_data_informada_devolve_a_data_de_referencia_e_a_data_do_aporte_no_formato_iso()
    {
        // Arrange
        var positionId = await CriarTituloEAporteAsync(maturityDate: "2027-01-01", applicationDate: "2025-04-01");

        // Act
        var response = await Client.GetAsync($"/api/positions/{positionId}/projection?referenceDate=2025-04-22");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var data = json.RootElement.GetProperty("data");
        Assert.Equal("2025-04-22", data.GetProperty("referenceDate").GetString());
        Assert.Equal("2025-04-01", data.GetProperty("applicationDate").GetString());
    }

    [Fact]
    public async Task Projecao_sem_data_com_cdi_gravado_devolve_a_data_padrao_como_data_de_referencia()
    {
        // Arrange: CDI até quinta, 2025-04-17; a data padrão é terça, 2025-04-22.
        using (var scope = Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<FixedIncomeDbContext>();
            context.IndexRates.Add(new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.05m));
            await context.SaveChangesAsync();
        }

        var positionId = await CriarTituloEAporteAsync(maturityDate: "2027-01-01", applicationDate: "2025-04-01");

        // Act
        var response = await Client.GetAsync($"/api/positions/{positionId}/projection");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var data = json.RootElement.GetProperty("data");
        Assert.Equal("2025-04-22", data.GetProperty("referenceDate").GetString());
        Assert.Equal("2025-04-01", data.GetProperty("applicationDate").GetString());
    }

    [Fact]
    public async Task Projecao_mantem_os_campos_antigos_com_os_mesmos_nomes_e_acrescenta_so_is_projected_net_yield_proxima_faixa_de_ir_e_fim_do_iof()
    {
        // Arrange
        var positionId = await CriarTituloEAporteAsync(maturityDate: "2027-01-01", applicationDate: "2025-04-01");

        // Act
        var response = await Client.GetAsync($"/api/positions/{positionId}/projection?referenceDate=2025-04-22");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var names = json.RootElement.GetProperty("data").EnumerateObject().Select(p => p.Name).ToHashSet();
        var expected = new[]
        {
            "positionId", "assetId", "investedAmount", "grossAmount", "grossYield", "taxRate", "taxAmount",
            "netAmount", "elapsedDays", "businessDays", "iofRate", "iofAmount", "isEstimated",
            "referenceDate", "applicationDate", "isProjected", "netYield", "nextTaxRate", "nextTaxRateDate",
            "iofFreeDate"
        };
        Assert.True(names.SetEquals(expected), $"Propriedades de data: {string.Join(", ", names)}");
    }

    [Fact]
    public async Task Projecao_devolve_net_yield_como_liquido_menos_investido_no_caso_de_referencia()
    {
        // Arrange: caso 1a da spec da F13. Aporte de R$ 1.000 em 2025-04-01, líquido 1.002,67 em 2025-04-22.
        var positionId = await CriarTituloEAporteAsync(maturityDate: "2027-01-01", applicationDate: "2025-04-01");

        // Act
        var response = await Client.GetAsync($"/api/positions/{positionId}/projection?referenceDate=2025-04-22");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var data = json.RootElement.GetProperty("data");
        Assert.Equal("1002.67", data.GetProperty("netAmount").GetRawText());
        Assert.Equal("2.67", data.GetProperty("netYield").GetRawText());
    }

    [Fact]
    public async Task Projecao_devolve_a_proxima_faixa_de_ir_e_o_fim_do_iof_em_literais_e_datas_iso()
    {
        // Arrange: caso 1a da spec da F13. Aporte em 2025-04-01 (d = 21): próxima faixa 20,0 em
        // aporte + 181 (2025-09-29) e fim do IOF em aporte + 30 (2025-05-01, feriado, sem ajuste).
        var positionId = await CriarTituloEAporteAsync(maturityDate: "2027-01-01", applicationDate: "2025-04-01");

        // Act
        var response = await Client.GetAsync($"/api/positions/{positionId}/projection?referenceDate=2025-04-22");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var data = json.RootElement.GetProperty("data");
        Assert.Equal("22.5", data.GetProperty("taxRate").GetRawText());
        Assert.Equal("20.0", data.GetProperty("nextTaxRate").GetRawText());
        Assert.Equal("2025-09-29", data.GetProperty("nextTaxRateDate").GetString());
        Assert.Equal("2025-05-01", data.GetProperty("iofFreeDate").GetString());
    }

    [Fact]
    public async Task Projecao_de_lca_devolve_os_tres_campos_presentes_e_nulos()
    {
        // Arrange: caso 5 da spec da F13. LCA é isenta de IR (RN-04) e de IOF (RN-11).
        var positionId = await CriarTituloEAporteAsync(
            maturityDate: "2027-04-01", applicationDate: "2025-04-01", assetType: "Lca");

        // Act
        var response = await Client.GetAsync($"/api/positions/{positionId}/projection?referenceDate=2025-04-22");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(JsonValueKind.Null, data.GetProperty("nextTaxRate").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("nextTaxRateDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("iofFreeDate").ValueKind);
    }

    private async Task<Guid> CriarTituloEAporteAsync(
        string maturityDate, string applicationDate, string assetType = "Cdb")
    {
        var assetResponse = await Client.PostAsJsonAsync("/api/assets", new
        {
            name = "CDB Longo",
            issuer = "Banco X",
            assetType,
            indexType = "PreFixed",
            rate = 10,
            issueDate = "2025-01-01",
            maturityDate
        });
        using var assetJson = await ReadJsonAsync(assetResponse);
        var assetId = assetJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();

        var positionResponse = await Client.PostAsJsonAsync(
            "/api/positions", new { assetId, investedAmount = 1000, applicationDate });
        using var positionJson = await ReadJsonAsync(positionResponse);

        return positionJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }
}
