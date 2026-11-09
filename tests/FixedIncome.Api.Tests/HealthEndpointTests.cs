using System.Net;

namespace FixedIncome.Api.Tests;

public class HealthEndpointTests : ApiTestBase
{
    [Fact]
    public async Task Get_health_retorna_200_com_o_texto_Healthy_sem_envelope()
    {
        // Act
        var response = await Client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Head_health_retorna_200()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Head, "/health");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_health_retorna_405_no_formato_api_response()
    {
        // Arrange
        using var content = new StringContent(string.Empty);

        // Act
        var response = await Client.PostAsync("/health", content);

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var root = json.RootElement;
        Assert.Equal(405, root.GetProperty("statusCode").GetInt32());
        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("message").GetString()));
    }
}
