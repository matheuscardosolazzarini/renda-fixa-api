using FixedIncome.Tests.Shared;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace FixedIncome.Api.Tests;

[Collection("PostgreSQL")]
public class FrameworkErrorResponsesTests(PostgreSqlFixture postgres) : ApiTestBase(postgres)
{
    [Fact]
    public async Task Rota_inexistente_retorna_404_no_formato_api_response()
    {
        // Act
        var response = await Client.GetAsync("/api/foo");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var root = json.RootElement;
        Assert.Equal(404, root.GetProperty("statusCode").GetInt32());
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("message").GetString()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, root.GetProperty("data").ValueKind);
    }

    [Fact]
    public async Task Metodo_nao_permitido_retorna_405_no_formato_api_response_e_mantem_o_cabecalho_allow()
    {
        // Act
        var response = await Client.PatchAsync("/api/assets", new StringContent("{}", Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Contains("POST", response.Content.Headers.Allow);
        using var json = await ReadJsonAsync(response);
        var root = json.RootElement;
        Assert.Equal(405, root.GetProperty("statusCode").GetInt32());
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Requisicao_sem_content_type_retorna_415_no_formato_api_response()
    {
        // Arrange
        using var content = new StringContent("""{"name":"CDB"}""");
        content.Headers.ContentType = null;

        // Act
        var response = await Client.PostAsync("/api/assets", content);

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var root = json.RootElement;
        Assert.Equal(415, root.GetProperty("statusCode").GetInt32());
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("message").GetString()));
        Assert.False(root.TryGetProperty("type", out _));
        Assert.False(root.TryGetProperty("title", out _));
    }

    [Fact]
    public async Task Content_type_nao_suportado_retorna_415_no_formato_api_response()
    {
        // Arrange
        using var content = new StringContent("name=CDB", Encoding.UTF8, "text/plain");

        // Act
        var response = await Client.PostAsync("/api/assets", content);

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(415, json.RootElement.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task Erros_do_framework_usam_o_mesmo_content_type_dos_demais_erros()
    {
        // Arrange
        var referencia = await Client.GetAsync("/api/assets/nao-e-um-guid");
        using var semContentType = new StringContent("{}");
        semContentType.Headers.ContentType = null;

        // Act
        var naoEncontrada = await Client.GetAsync("/api/foo");
        var naoPermitido = await Client.PatchAsync("/api/assets", new StringContent("{}", Encoding.UTF8, "application/json"));
        var naoSuportado = await Client.PostAsync("/api/assets", semContentType);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, referencia.StatusCode);
        var esperado = referencia.Content.Headers.ContentType;
        Assert.Equal(MediaTypeHeaderValue.Parse("application/json; charset=utf-8"), esperado);
        Assert.Equal(esperado, naoEncontrada.Content.Headers.ContentType);
        Assert.Equal(esperado, naoPermitido.Content.Headers.ContentType);
        Assert.Equal(esperado, naoSuportado.Content.Headers.ContentType);
    }

    [Fact]
    public async Task Titulo_inexistente_mantem_a_mensagem_do_caso_de_uso_e_nao_a_generica()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/api/assets/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal($"Título com id {id} não encontrado.", json.RootElement.GetProperty("message").GetString());
    }
}
