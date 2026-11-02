using System.Net;
using System.Text;
using FixedIncome.Application.Providers;
using FixedIncome.Domain.Enums;
using FixedIncome.Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace FixedIncome.Infrastructure.Tests.Providers;

public class SgsNonJsonResponseHandlerTests
{
    private const string Html = "<html><body>Bad gateway</body></html>";
    private const string Json = """[{"data":"26/10/2026","valor":"0.050788"}]""";

    [Fact]
    public async Task Resposta_com_corpo_html_do_sgs_e_tratada_como_falha_transitoria()
    {
        // Arrange
        // A primeira tentativa recebe HTML no lugar do JSON; a segunda, a cotação.
        var primario = new RespostasEmSequenciaHandler((HttpStatusCode.OK, Html), (HttpStatusCode.OK, Json));
        using var services = CriarServicosComPipelineReal(primario);
        var provider = services.GetRequiredService<IIndexRateProvider>();

        // Act
        var rates = (await provider.FetchAsync(IndexType.Cdi, new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 27))).ToList();

        // Assert
        var rate = Assert.Single(rates);
        Assert.Equal(new DateOnly(2026, 10, 26), rate.ReferenceDate);
        Assert.Equal(2, primario.Requisicoes);
    }

    [Fact]
    public async Task Corpo_html_em_todas_as_tentativas_propaga_a_falha_depois_de_esgotar_o_retry()
    {
        // Arrange
        var primario = new RespostasEmSequenciaHandler((HttpStatusCode.OK, Html));
        using var services = CriarServicosComPipelineReal(primario);
        var provider = services.GetRequiredService<IIndexRateProvider>();

        // Act
        var act = () => provider.FetchAsync(IndexType.Cdi, new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 27));

        // Assert
        // 1 tentativa inicial + 3 repetições do retry padrão.
        var exception = await Assert.ThrowsAsync<HttpRequestException>(act);
        Assert.Contains("não é JSON", exception.Message);
        Assert.Equal(4, primario.Requisicoes);
    }

    [Fact]
    public async Task Json_malformado_nao_e_repetido_porque_nao_e_corpo_de_outro_formato()
    {
        // Arrange
        var primario = new RespostasEmSequenciaHandler((HttpStatusCode.OK, """[{"data":"26/10/2026","valor":"""));
        using var services = CriarServicosComPipelineReal(primario);
        var provider = services.GetRequiredService<IIndexRateProvider>();

        // Act
        var act = () => provider.FetchAsync(IndexType.Cdi, new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 27));

        // Assert
        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(act);
        Assert.Equal(1, primario.Requisicoes);
    }

    [Theory]
    [InlineData("""[{"data":"26/10/2026","valor":"0.050788"}]""")]
    [InlineData("""{"erro":{"statusCode":404}}""")]
    [InlineData("""   [] """)]
    public async Task Corpo_json_passa_sem_alteracao_e_continua_legivel(string corpo)
    {
        // Arrange
        using var client = CriarClienteComHandler(new RespostasEmSequenciaHandler((HttpStatusCode.OK, corpo)));

        // Act
        using var response = await client.GetAsync("dados");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(corpo, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Service Unavailable")]
    [InlineData("<!DOCTYPE html><html></html>")]
    public async Task Corpo_que_nao_e_json_com_status_de_sucesso_lanca_http_request_exception(string corpo)
    {
        // Arrange
        using var client = CriarClienteComHandler(new RespostasEmSequenciaHandler((HttpStatusCode.OK, corpo)));

        // Act
        var act = () => client.GetAsync("dados");

        // Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(act);
        Assert.Equal(HttpStatusCode.OK, exception.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Status_de_erro_com_corpo_html_passa_sem_alteracao_para_o_provedor_decidir(HttpStatusCode status)
    {
        // Arrange
        using var client = CriarClienteComHandler(new RespostasEmSequenciaHandler((status, Html)));

        // Act
        using var response = await client.GetAsync("dados");

        // Assert
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(Html, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Mensagem_da_excecao_traz_o_inicio_do_corpo_limitado()
    {
        // Arrange
        var corpoLongo = "<html>" + new string('x', 1000);
        using var client = CriarClienteComHandler(new RespostasEmSequenciaHandler((HttpStatusCode.OK, corpoLongo)));

        // Act
        var act = () => client.GetAsync("dados");

        // Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(act);
        Assert.Contains("<html>", exception.Message);
        Assert.True(exception.Message.Length < 300, $"Mensagem com {exception.Message.Length} caracteres.");
    }

    private static HttpClient CriarClienteComHandler(HttpMessageHandler interno)
    {
        var handler = new SgsNonJsonResponseHandler { InnerHandler = interno };
        return new HttpClient(handler) { BaseAddress = SgsIndexRateProvider.BaseAddress };
    }

    // Monta o cliente do SGS pelo registro de produção (AddInfrastructure), trocando só o handler
    // primário pelo stub e zerando o intervalo do retry para o teste não esperar.
    private static ServiceProvider CriarServicosComPipelineReal(HttpMessageHandler primario)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure("Host=sem-banco;Database=teste;Username=teste;Password=teste");
        services.AddHttpClient<IIndexRateProvider, SgsIndexRateProvider>()
            .ConfigurePrimaryHttpMessageHandler(() => primario);
        services.Configure<HttpStandardResilienceOptions>("IIndexRateProvider-standard", options =>
        {
            options.Retry.Delay = TimeSpan.Zero;
            options.Retry.UseJitter = false;
        });

        return services.BuildServiceProvider();
    }

    // Responde na ordem as respostas informadas; a última se repete se houver mais requisições.
    private sealed class RespostasEmSequenciaHandler : HttpMessageHandler
    {
        private readonly (HttpStatusCode Status, string Body)[] _responses;
        private int _requisicoes;

        public RespostasEmSequenciaHandler(params (HttpStatusCode Status, string Body)[] responses)
        {
            _responses = responses;
        }

        public int Requisicoes => _requisicoes;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var index = Math.Min(Interlocked.Increment(ref _requisicoes) - 1, _responses.Length - 1);
            var (status, body) = _responses[index];

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
