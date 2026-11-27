using System.Net;
using System.Net.Http.Json;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Infrastructure.Context;
using FixedIncome.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FixedIncome.Api.Tests;

// F12, RN-12: a projeção de CDI com data futura depende de "hoje" pela fronteira do dia útil anterior
// a hoje. O relógio é fixo em 2026-11-24 (terça) e as cotações são gravadas relativas a ele: a última
// é a do dia útil anterior (23/11), com 20/11 feriado. Só CDI e PreFixed: o IPCA projetado dependeria
// do mês em que o teste roda e fica nos testes de Domain e de Application.
[Collection("PostgreSQL")]
public class FutureDateProjectionFlowTests(PostgreSqlFixture postgres) : ApiTestBase(postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 15, 0, 0, TimeSpan.Zero);

    // O registro da Application é TryAddSingleton: o dublê entra por cima dele.
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
    }

    // Caso 1 da spec: 0,050788% ao dia em 16, 17, 18, 19 e 23/11; 24 e 25/11 são projetados com esse
    // valor (7 dias úteis, 10 corridos). Bruto 1.003,56, IOF 2,35, IR 0,27, líquido 1.000,94.
    [Fact]
    public async Task Projecao_de_cdi_com_data_futura_devolve_is_projected_verdadeiro_com_os_valores_do_caso_de_referencia()
    {
        // Arrange
        await GravarCdiAteODiaUtilAnteriorAsync();
        var positionId = await CriarAporteAsync("Cdi", 100, "2026-11-16");

        // Act
        var response = await Client.GetAsync($"/api/positions/{positionId}/projection?referenceDate=2026-11-26");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var data = json.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("isProjected").GetBoolean());
        Assert.False(data.GetProperty("isEstimated").GetBoolean());
        Assert.Equal(7, data.GetProperty("businessDays").GetInt32());
        Assert.Equal(1_003.56m, data.GetProperty("grossAmount").GetDecimal());
        Assert.Equal(1_000.94m, data.GetProperty("netAmount").GetDecimal());
    }

    [Fact]
    public async Task
        Resumo_com_data_futura_devolve_is_projected_verdadeiro_na_raiz_e_no_item_de_cdi_e_falso_no_item_prefixado()
    {
        // Arrange
        await GravarCdiAteODiaUtilAnteriorAsync();
        var cdiPositionId = await CriarAporteAsync("Cdi", 100, "2026-11-16");
        var preFixedPositionId = await CriarAporteAsync("PreFixed", 10, "2026-11-16");

        // Act
        var response = await Client.GetAsync("/api/portfolio/summary?referenceDate=2026-11-26");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var data = json.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("isProjected").GetBoolean());

        var items = data.GetProperty("positions").EnumerateArray().ToList();
        var cdiItem = Assert.Single(items, i =>
            i.GetProperty("projection").GetProperty("positionId").GetGuid() == cdiPositionId)
            .GetProperty("projection");
        Assert.True(cdiItem.GetProperty("isProjected").GetBoolean());
        Assert.Equal(1_003.56m, cdiItem.GetProperty("grossAmount").GetDecimal());

        var preFixedItem = Assert.Single(items, i =>
            i.GetProperty("projection").GetProperty("positionId").GetGuid() == preFixedPositionId)
            .GetProperty("projection");
        Assert.False(preFixedItem.GetProperty("isProjected").GetBoolean());
    }

    // Cotações de 16, 17, 18, 19 e 23/11/2026 (20/11 é feriado): a última é a do dia útil anterior ao
    // "hoje" fixo do teste.
    private async Task GravarCdiAteODiaUtilAnteriorAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FixedIncomeDbContext>();

        foreach (var day in new[] { 16, 17, 18, 19, 23 })
        {
            context.IndexRates.Add(new IndexRate(IndexType.Cdi, new DateOnly(2026, 11, day), 0.050788m));
        }

        await context.SaveChangesAsync();
    }

    private async Task<Guid> CriarAporteAsync(string indexType, decimal rate, string applicationDate)
    {
        var assetResponse = await Client.PostAsJsonAsync("/api/assets", new
        {
            name = $"CDB {indexType}",
            issuer = "Banco X",
            assetType = "Cdb",
            indexType,
            rate,
            issueDate = "2026-01-01",
            maturityDate = "2028-01-01"
        });
        Assert.Equal(HttpStatusCode.Created, assetResponse.StatusCode);
        using var assetJson = await ReadJsonAsync(assetResponse);
        var assetId = assetJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();

        var positionResponse = await Client.PostAsJsonAsync(
            "/api/positions", new { assetId, investedAmount = 1000, applicationDate });
        Assert.Equal(HttpStatusCode.Created, positionResponse.StatusCode);
        using var positionJson = await ReadJsonAsync(positionResponse);

        return positionJson.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;

        // Brasília fixo em UTC-3 (sem horário de verão desde 2019), para o teste não depender do tzdata.
        public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.CreateCustomTimeZone(
            "America/Sao_Paulo", TimeSpan.FromHours(-3), "Brasília", "Brasília");
    }
}
