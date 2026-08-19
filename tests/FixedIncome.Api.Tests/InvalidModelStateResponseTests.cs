using System.Net;
using System.Net.Http.Json;

namespace FixedIncome.Api.Tests;

public class InvalidModelStateResponseTests : ApiTestBase
{
    [Fact]
    public async Task Json_malformado_retorna_mensagem_de_corpo_invalido_em_portugues()
    {
        // Arrange
        using var content = new StringContent("{name:\"CDB\"}");
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        // Act
        var response = await Client.PostAsync("/api/assets", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(400, json.RootElement.GetProperty("statusCode").GetInt32());
        Assert.Equal(
            "O corpo da requisição está ausente ou malformado.",
            json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Corpo_vazio_retorna_mensagem_de_corpo_invalido_em_portugues()
    {
        // Arrange
        using var content = new StringContent(string.Empty);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        // Act
        var response = await Client.PostAsync("/api/assets", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(
            "O corpo da requisição está ausente ou malformado.",
            json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Campo_obrigatorio_ausente_preserva_o_nome_do_campo_em_portugues()
    {
        // Arrange
        var request = new
        {
            issuer = "Banco X",
            assetType = "Cdb",
            indexType = "PreFixed",
            rate = 12,
            issueDate = "2025-01-01",
            maturityDate = "2027-01-01"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/assets", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal("O campo 'Name' é obrigatório.", json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Guid_invalido_na_rota_preserva_o_nome_do_parametro_em_portugues()
    {
        // Act
        var response = await Client.GetAsync("/api/assets/nao-e-um-guid");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal("O valor do campo 'id' é inválido.", json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Guid_invalido_dentro_do_corpo_preserva_o_nome_do_campo_sem_vazar_tipo_dotnet()
    {
        // Arrange
        var request = new { assetId = "nao-e-um-guid", investedAmount = 1000, applicationDate = "2025-06-01" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/positions", request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal("O valor do campo 'assetId' é inválido.", json.RootElement.GetProperty("message").GetString());
        Assert.DoesNotContain("CreatePositionRequest", content);
        Assert.DoesNotContain("Path:", content);
    }

    [Fact]
    public async Task Data_de_referencia_em_formato_invalido_preserva_o_nome_do_parametro_em_portugues()
    {
        // Arrange
        var positionId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/api/positions/{positionId}/projection?referenceDate=31/12/2026");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(
            "O valor do campo 'referenceDate' é inválido.", json.RootElement.GetProperty("message").GetString());
    }
}
