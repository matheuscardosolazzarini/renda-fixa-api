using System.Net;
using System.Net.Http.Json;

namespace FixedIncome.Api.Tests;

public class AssetsControllerTests : ApiTestBase
{
    private static object CriarRequestValido() => new
    {
        name = "CDB Banco X",
        issuer = "Banco X",
        assetType = "Cdb",
        indexType = "PreFixed",
        rate = 12,
        issueDate = "2025-01-01",
        maturityDate = "2027-01-01"
    };

    [Fact]
    public async Task Titulo_valido_retorna_201_com_id_gerado()
    {
        // Arrange
        var request = CriarRequestValido();

        // Act
        var response = await Client.PostAsJsonAsync("/api/assets", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var id = json.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task Titulo_valido_retorna_asset_type_e_index_type_como_texto_legivel()
    {
        // Arrange
        var request = CriarRequestValido();

        // Act
        var response = await Client.PostAsJsonAsync("/api/assets", request);

        // Assert
        using var json = await ReadJsonAsync(response);
        var data = json.RootElement.GetProperty("data");
        Assert.Equal("Cdb", data.GetProperty("assetType").GetString());
        Assert.Equal("PreFixed", data.GetProperty("indexType").GetString());
    }

    [Fact]
    public async Task Titulo_com_taxa_negativa_retorna_400()
    {
        // Arrange
        var request = new
        {
            name = "CDB Banco X",
            issuer = "Banco X",
            assetType = "Cdb",
            indexType = "PreFixed",
            rate = -1,
            issueDate = "2025-01-01",
            maturityDate = "2027-01-01"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/assets", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Titulo_com_asset_type_inexistente_retorna_400()
    {
        // Arrange
        var request = new
        {
            name = "CDB Banco X",
            issuer = "Banco X",
            assetType = "Inexistente",
            indexType = "PreFixed",
            rate = 12,
            issueDate = "2025-01-01",
            maturityDate = "2027-01-01"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/assets", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Titulo_inexistente_retorna_404()
    {
        // Act
        var response = await Client.GetAsync($"/api/assets/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Titulo_com_aportes_nao_pode_ser_removido()
    {
        // Arrange
        var assetId = await CriarTituloAsync();
        await Client.PostAsJsonAsync("/api/positions", new
        {
            assetId,
            investedAmount = 1000,
            applicationDate = "2025-06-01"
        });

        // Act
        var response = await Client.DeleteAsync($"/api/assets/{assetId}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<Guid> CriarTituloAsync()
    {
        var response = await Client.PostAsJsonAsync("/api/assets", CriarRequestValido());
        using var json = await ReadJsonAsync(response);
        return json.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }
}
