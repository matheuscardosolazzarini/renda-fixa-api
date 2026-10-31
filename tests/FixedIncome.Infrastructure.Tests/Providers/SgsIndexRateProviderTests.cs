using System.Globalization;
using System.Net;
using System.Text;
using System.Web;
using FixedIncome.Domain.Common;
using FixedIncome.Domain.Enums;
using FixedIncome.Infrastructure.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FixedIncome.Infrastructure.Tests.Providers;

public class SgsIndexRateProviderTests
{
    [Fact]
    public async Task Cdi_le_data_em_dia_mes_ano_e_valor_com_ponto_decimal()
    {
        // Arrange
        var handler = new HttpMessageHandlerStub(HttpStatusCode.OK, """[{"data":"14/09/2026","valor":"0.051660"}]""");
        var provider = CreateProvider(handler);

        // Act
        var rates = (await provider.FetchAsync(IndexType.Cdi, new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 14))).ToList();

        // Assert
        var rate = Assert.Single(rates);
        Assert.Equal(IndexType.Cdi, rate.IndexType);
        Assert.Equal(new DateOnly(2026, 9, 14), rate.ReferenceDate);
        Assert.Equal(0.051660m, rate.Value);
    }

    [Fact]
    public async Task Cdi_com_varias_cotacoes_preserva_a_ordem_da_resposta()
    {
        // Arrange
        var handler = new HttpMessageHandlerStub(HttpStatusCode.OK,
            """[{"data":"11/09/2026","valor":"0.051660"},{"data":"14/09/2026","valor":"0.051661"}]""");
        var provider = CreateProvider(handler);

        // Act
        var rates = (await provider.FetchAsync(IndexType.Cdi, new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 14))).ToList();

        // Assert
        Assert.Equal(2, rates.Count);
        Assert.Equal(new DateOnly(2026, 9, 11), rates[0].ReferenceDate);
        Assert.Equal(0.051660m, rates[0].Value);
        Assert.Equal(new DateOnly(2026, 9, 14), rates[1].ReferenceDate);
        Assert.Equal(0.051661m, rates[1].Value);
    }

    [Fact]
    public async Task Ipca_de_agosto_de_2026_e_deflacao_com_data_no_dia_primeiro()
    {
        // Arrange
        var handler = new HttpMessageHandlerStub(HttpStatusCode.OK, """[{"data":"01/08/2026","valor":"-0.32"}]""");
        var provider = CreateProvider(handler);

        // Act
        var rates = (await provider.FetchAsync(IndexType.Ipca, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31))).ToList();

        // Assert
        var rate = Assert.Single(rates);
        Assert.Equal(IndexType.Ipca, rate.IndexType);
        Assert.Equal(new DateOnly(2026, 8, 1), rate.ReferenceDate);
        Assert.Equal(-0.32m, rate.Value);
    }

    [Fact]
    public async Task Ipca_de_setembro_de_2026_vem_datado_em_01_09_2026()
    {
        // Arrange
        var handler = new HttpMessageHandlerStub(HttpStatusCode.OK, """[{"data":"01/09/2026","valor":"0.82"}]""");
        var provider = CreateProvider(handler);

        // Act
        var rates = (await provider.FetchAsync(IndexType.Ipca, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30))).ToList();

        // Assert
        var rate = Assert.Single(rates);
        Assert.Equal(new DateOnly(2026, 9, 1), rate.ReferenceDate);
        Assert.Equal(0.82m, rate.Value);
    }

    [Fact]
    public async Task Ipca_com_data_fora_do_dia_primeiro_propaga_a_excecao_de_dominio()
    {
        // Arrange
        var handler = new HttpMessageHandlerStub(HttpStatusCode.OK, """[{"data":"15/09/2026","valor":"0.48"}]""");
        var provider = CreateProvider(handler);

        // Act
        var act = () => provider.FetchAsync(IndexType.Ipca, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        // Assert
        await Assert.ThrowsAsync<DomainException>(act);
    }

    [Fact]
    public async Task Cdi_negativo_na_resposta_propaga_a_excecao_de_dominio()
    {
        // Arrange
        var handler = new HttpMessageHandlerStub(HttpStatusCode.OK, """[{"data":"14/09/2026","valor":"-0.051660"}]""");
        var provider = CreateProvider(handler);

        // Act
        var act = () => provider.FetchAsync(IndexType.Cdi, new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 14));

        // Assert
        await Assert.ThrowsAsync<DomainException>(act);
    }

    [Fact]
    public async Task Cdi_monta_a_url_com_serie_12_e_datas_em_dia_mes_ano()
    {
        // Arrange
        var handler = new HttpMessageHandlerStub(HttpStatusCode.OK, "[]");
        var provider = CreateProvider(handler);

        // Act
        await provider.FetchAsync(IndexType.Cdi, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 14));

        // Assert
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.bcb.gov.br", request.Uri.GetLeftPart(UriPartial.Authority));
        Assert.Equal(
            "/dados/serie/bcdata.sgs.12/dados?formato=json&dataInicial=01/09/2026&dataFinal=14/09/2026",
            request.Uri.PathAndQuery);
    }

    [Fact]
    public async Task Ipca_monta_a_url_com_serie_433()
    {
        // Arrange
        var handler = new HttpMessageHandlerStub(HttpStatusCode.OK, "[]");
        var provider = CreateProvider(handler);

        // Act
        await provider.FetchAsync(IndexType.Ipca, new DateOnly(2026, 1, 1), new DateOnly(2026, 8, 31));

        // Assert
        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            "/dados/serie/bcdata.sgs.433/dados?formato=json&dataInicial=01/01/2026&dataFinal=31/08/2026",
            request.Uri.PathAndQuery);
    }

    [Fact]
    public async Task Resposta_vazia_devolve_lista_vazia()
    {
        // Arrange
        var handler = new HttpMessageHandlerStub(HttpStatusCode.OK, "[]");
        var provider = CreateProvider(handler);

        // Act
        var rates = await provider.FetchAsync(IndexType.Cdi, new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13));

        // Assert
        Assert.Empty(rates);
    }

    [Fact]
    public async Task Status_404_do_sgs_e_periodo_sem_dado_e_vira_lista_vazia()
    {
        // Arrange
        // O BC responde 404 com "Value(s) not found" quando o período não tem cotação.
        var handler = new HttpMessageHandlerStub(HttpStatusCode.NotFound, "Value(s) not found");
        var provider = CreateProvider(handler);

        // Act
        var rates = await provider.FetchAsync(IndexType.Cdi, new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13));

        // Assert
        Assert.Empty(rates);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotAcceptable)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Erro_4xx_diferente_de_404_nao_e_ausencia_de_dado_e_propaga(HttpStatusCode status)
    {
        // Arrange
        var handler = new HttpMessageHandlerStub(status, "");
        var provider = CreateProvider(handler);

        // Act
        var act = () => provider.FetchAsync(IndexType.Cdi, new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13));

        // Assert
        await Assert.ThrowsAsync<HttpRequestException>(act);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Erro_5xx_propaga_a_excecao_de_http(HttpStatusCode status)
    {
        // Arrange
        var handler = new HttpMessageHandlerStub(status, "");
        var provider = CreateProvider(handler);

        // Act
        var act = () => provider.FetchAsync(IndexType.Cdi, new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13));

        // Assert
        await Assert.ThrowsAsync<HttpRequestException>(act);
    }

    [Fact]
    public async Task Erro_do_sgs_propaga_com_status_e_corpo_da_resposta_na_mensagem()
    {
        // Arrange
        // Janela acima de 10 anos: o BC responde 406 explicando o motivo no corpo.
        var handler = new HttpMessageHandlerStub(HttpStatusCode.NotAcceptable, "Intervalo maior que 10 anos nao permitido");
        var provider = CreateProvider(handler);

        // Act
        var act = () => provider.FetchAsync(IndexType.Cdi, new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13));

        // Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(act);
        Assert.Equal(HttpStatusCode.NotAcceptable, exception.StatusCode);
        Assert.Contains("406", exception.Message);
        Assert.Contains("Intervalo maior que 10 anos nao permitido", exception.Message);
        Assert.Contains("2026-09-12 a 2026-09-13", exception.Message);
    }

    [Fact]
    public async Task Log_do_404_formata_as_datas_em_ano_mes_dia_independente_da_cultura()
    {
        // Arrange
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("en-US");
        try
        {
            var handler = new HttpMessageHandlerStub(HttpStatusCode.NotFound, "Value(s) not found");
            var logger = new LoggerStub();
            var client = new HttpClient(handler) { BaseAddress = SgsIndexRateProvider.BaseAddress };
            var provider = new SgsIndexRateProvider(client, logger);

            // Act
            await provider.FetchAsync(IndexType.Ipca, new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 30));

            // Assert
            var message = Assert.Single(logger.Messages);
            Assert.Contains("2026-01-10 a 2026-01-30", message);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task Corpo_longo_do_erro_e_truncado_na_mensagem()
    {
        // Arrange
        var handler = new HttpMessageHandlerStub(HttpStatusCode.InternalServerError, new string('x', 2000));
        var provider = CreateProvider(handler);

        // Act
        var act = () => provider.FetchAsync(IndexType.Cdi, new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13));

        // Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(act);
        Assert.Contains(new string('x', 500), exception.Message);
        Assert.DoesNotContain(new string('x', 501), exception.Message);
    }

    [Fact]
    public async Task PreFixado_nao_tem_serie_e_e_rejeitado_sem_chamar_o_sgs()
    {
        // Arrange
        var handler = new HttpMessageHandlerStub(HttpStatusCode.OK, "[]");
        var provider = CreateProvider(handler);

        // Act
        var act = () => provider.FetchAsync(IndexType.PreFixed, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        // Assert
        await Assert.ThrowsAsync<ArgumentException>(act);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Periodo_invertido_devolve_lista_vazia_sem_chamar_o_sgs()
    {
        // Arrange
        var handler = new HttpMessageHandlerStub(HttpStatusCode.OK, "[]");
        var provider = CreateProvider(handler);

        // Act
        var rates = await provider.FetchAsync(IndexType.Cdi, new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 14));

        // Assert
        Assert.Empty(rates);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Periodo_de_exatamente_10_anos_e_uma_unica_requisicao()
    {
        // Arrange
        var handler = new HttpMessageHandlerStub(HttpStatusCode.OK, "[]");
        var provider = CreateProvider(handler);

        // Act
        await provider.FetchAsync(IndexType.Cdi, new DateOnly(2016, 1, 1), new DateOnly(2025, 12, 31));

        // Assert
        var request = Assert.Single(handler.Requests);
        Assert.EndsWith("dataInicial=01/01/2016&dataFinal=31/12/2025", request.Uri.Query);
    }

    [Fact]
    public async Task Periodo_de_um_dia_consulta_a_partir_da_vespera_e_devolve_so_a_cotacao_do_dia()
    {
        // Arrange
        // O SGS responde 400 quando dataInicial == dataFinal; a consulta de 28/10 a 29/10 traz as duas cotações.
        var handler = new HttpMessageHandlerStub(HttpStatusCode.OK,
            """[{"data":"28/10/2026","valor":"0.050700"},{"data":"29/10/2026","valor":"0.050788"}]""");
        var provider = CreateProvider(handler);

        // Act
        var rates = (await provider.FetchAsync(IndexType.Cdi, new DateOnly(2026, 10, 29), new DateOnly(2026, 10, 29))).ToList();

        // Assert
        var request = Assert.Single(handler.Requests);
        Assert.EndsWith("dataInicial=28/10/2026&dataFinal=29/10/2026", request.Uri.Query);
        var rate = Assert.Single(rates);
        Assert.Equal(new DateOnly(2026, 10, 29), rate.ReferenceDate);
        Assert.Equal(0.050788m, rate.Value);
    }

    [Fact]
    public async Task Periodo_de_um_dia_na_virada_de_mes_consulta_a_partir_do_ultimo_dia_do_mes_anterior()
    {
        // Arrange
        var handler = new HttpMessageHandlerStub(HttpStatusCode.OK,
            """[{"data":"30/09/2026","valor":"0.050788"},{"data":"01/10/2026","valor":"0.050790"}]""");
        var provider = CreateProvider(handler);

        // Act
        var rates = (await provider.FetchAsync(IndexType.Cdi, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1))).ToList();

        // Assert
        var request = Assert.Single(handler.Requests);
        Assert.EndsWith("dataInicial=30/09/2026&dataFinal=01/10/2026", request.Uri.Query);
        var rate = Assert.Single(rates);
        Assert.Equal(new DateOnly(2026, 10, 1), rate.ReferenceDate);
    }

    [Fact]
    public async Task Periodo_de_um_dia_sem_cotacao_com_404_devolve_lista_vazia()
    {
        // Arrange
        // Domingo 25/10/2026: a consulta de 24/10 a 25/10 não tem dado e o SGS responde 404.
        var handler = new HttpMessageHandlerStub(HttpStatusCode.NotFound, "Value(s) not found");
        var provider = CreateProvider(handler);

        // Act
        var rates = await provider.FetchAsync(IndexType.Cdi, new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 25));

        // Assert
        var request = Assert.Single(handler.Requests);
        Assert.EndsWith("dataInicial=24/10/2026&dataFinal=25/10/2026", request.Uri.Query);
        Assert.Empty(rates);
    }

    [Fact]
    public async Task Periodo_de_um_dia_sem_cotacao_mas_com_a_da_vespera_na_resposta_devolve_lista_vazia()
    {
        // Arrange
        // Sábado 24/10/2026: a consulta de 23/10 a 24/10 devolve só a cotação de sexta, fora do período pedido.
        var handler = new HttpMessageHandlerStub(HttpStatusCode.OK, """[{"data":"23/10/2026","valor":"0.050788"}]""");
        var provider = CreateProvider(handler);

        // Act
        var rates = await provider.FetchAsync(IndexType.Cdi, new DateOnly(2026, 10, 24), new DateOnly(2026, 10, 24));

        // Assert
        Assert.Empty(rates);
    }

    [Fact]
    public async Task Ipca_de_um_dia_no_dia_primeiro_consulta_a_partir_da_vespera_e_filtra_o_mes_anterior()
    {
        // Arrange
        // Confirmado no SGS real: a consulta de 31/08 a 01/09 na série 433 devolve também a cotação de 01/08.
        var handler = new HttpMessageHandlerStub(HttpStatusCode.OK,
            """[{"data":"01/08/2026","valor":"-0.32"},{"data":"01/09/2026","valor":"0.82"}]""");
        var provider = CreateProvider(handler);

        // Act
        var rates = (await provider.FetchAsync(IndexType.Ipca, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1))).ToList();

        // Assert
        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            "/dados/serie/bcdata.sgs.433/dados?formato=json&dataInicial=31/08/2026&dataFinal=01/09/2026",
            request.Uri.PathAndQuery);
        var rate = Assert.Single(rates);
        Assert.Equal(new DateOnly(2026, 9, 1), rate.ReferenceDate);
        Assert.Equal(0.82m, rate.Value);
    }

    [Fact]
    public async Task Periodo_de_10_anos_e_1_dia_e_dividido_em_duas_janelas()
    {
        // Arrange
        var handler = new HttpMessageHandlerStub(HttpStatusCode.OK, "[]");
        var provider = CreateProvider(handler);

        // Act
        await provider.FetchAsync(IndexType.Cdi, new DateOnly(2016, 1, 1), new DateOnly(2026, 1, 1));

        // Assert
        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("dataInicial=01/01/2016&dataFinal=31/12/2025", handler.Requests[0].Uri.Query);
        // A segunda janela tem um só dia: o SGS rejeita dataInicial == dataFinal, então a consulta começa na véspera.
        Assert.EndsWith("dataInicial=31/12/2025&dataFinal=01/01/2026", handler.Requests[1].Uri.Query);
    }

    [Fact]
    public async Task Periodo_de_12_anos_gera_duas_requisicoes_sem_sobreposicao_nem_lacuna()
    {
        // Arrange
        var handler = new HttpMessageHandlerStub(HttpStatusCode.OK, "[]");
        var provider = CreateProvider(handler);

        // Act
        await provider.FetchAsync(IndexType.Cdi, new DateOnly(2010, 1, 1), new DateOnly(2021, 12, 31));

        // Assert
        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("dataInicial=01/01/2010&dataFinal=31/12/2019", handler.Requests[0].Uri.Query);
        Assert.EndsWith("dataInicial=01/01/2020&dataFinal=31/12/2021", handler.Requests[1].Uri.Query);
    }

    [Fact]
    public async Task Periodo_de_25_anos_gera_tres_requisicoes_encadeadas()
    {
        // Arrange
        var handler = new HttpMessageHandlerStub(HttpStatusCode.OK, "[]");
        var provider = CreateProvider(handler);

        // Act
        await provider.FetchAsync(IndexType.Cdi, new DateOnly(2000, 1, 1), new DateOnly(2024, 12, 31));

        // Assert
        Assert.Equal(3, handler.Requests.Count);
        Assert.EndsWith("dataInicial=01/01/2000&dataFinal=31/12/2009", handler.Requests[0].Uri.Query);
        Assert.EndsWith("dataInicial=01/01/2010&dataFinal=31/12/2019", handler.Requests[1].Uri.Query);
        Assert.EndsWith("dataInicial=01/01/2020&dataFinal=31/12/2024", handler.Requests[2].Uri.Query);
    }

    [Fact]
    public async Task Janelas_comecando_em_29_de_fevereiro_continuam_sem_lacuna()
    {
        // Arrange
        var handler = new HttpMessageHandlerStub(HttpStatusCode.OK, "[]");
        var provider = CreateProvider(handler);

        // Act
        await provider.FetchAsync(IndexType.Cdi, new DateOnly(2020, 2, 29), new DateOnly(2031, 1, 1));

        // Assert
        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("dataInicial=29/02/2020&dataFinal=27/02/2030", handler.Requests[0].Uri.Query);
        Assert.EndsWith("dataInicial=28/02/2030&dataFinal=01/01/2031", handler.Requests[1].Uri.Query);
    }

    [Fact]
    public async Task Resultado_das_janelas_e_concatenado_na_ordem_sem_duplicar_datas()
    {
        // Arrange
        var handler = new HttpMessageHandlerStub(
            (HttpStatusCode.OK, """[{"data":"31/12/2019","valor":"0.019000"}]"""),
            (HttpStatusCode.OK, """[{"data":"02/01/2020","valor":"0.029000"},{"data":"03/01/2020","valor":"0.030000"}]"""));
        var provider = CreateProvider(handler);

        // Act
        var rates = (await provider.FetchAsync(IndexType.Cdi, new DateOnly(2010, 1, 1), new DateOnly(2021, 12, 31))).ToList();

        // Assert
        Assert.Equal(
            new[] { new DateOnly(2019, 12, 31), new DateOnly(2020, 1, 2), new DateOnly(2020, 1, 3) },
            rates.Select(r => r.ReferenceDate).ToArray());
        Assert.Equal(
            new[] { 0.019000m, 0.029000m, 0.030000m },
            rates.Select(r => r.Value).ToArray());
    }

    [Fact]
    public async Task Janela_sem_dado_com_404_nao_descarta_as_cotacoes_das_outras_janelas()
    {
        // Arrange
        var handler = new HttpMessageHandlerStub(
            (HttpStatusCode.NotFound, ""),
            (HttpStatusCode.OK, """[{"data":"02/01/2020","valor":"0.029000"}]"""));
        var provider = CreateProvider(handler);

        // Act
        var rates = (await provider.FetchAsync(IndexType.Cdi, new DateOnly(2010, 1, 1), new DateOnly(2021, 12, 31))).ToList();

        // Assert
        var rate = Assert.Single(rates);
        Assert.Equal(new DateOnly(2020, 1, 2), rate.ReferenceDate);
    }

    private static SgsIndexRateProvider CreateProvider(HttpMessageHandlerStub handler)
    {
        var client = new HttpClient(handler) { BaseAddress = SgsIndexRateProvider.BaseAddress };
        return new SgsIndexRateProvider(client, NullLogger<SgsIndexRateProvider>.Instance);
    }

    private sealed record RecordedRequest(HttpMethod Method, Uri Uri);

    private sealed class LoggerStub : ILogger<SgsIndexRateProvider>
    {
        public List<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }

    // Responde na ordem as respostas informadas; a última se repete se houver mais requisições.
    // Como o SGS real, rejeita com 400 a requisição cujo dataInicial é igual a dataFinal.
    private sealed class HttpMessageHandlerStub : HttpMessageHandler
    {
        private readonly (HttpStatusCode Status, string Body)[] _responses;

        public HttpMessageHandlerStub(HttpStatusCode status, string body)
            : this((status, body))
        {
        }

        public HttpMessageHandlerStub(params (HttpStatusCode Status, string Body)[] responses)
        {
            _responses = responses;
        }

        public List<RecordedRequest> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!));

            var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
            if (query["dataInicial"] == query["dataFinal"])
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("""{"erro":"DataFinal deve ser maior que dataInicial."}""", Encoding.UTF8, "application/json")
                });
            }

            var (status, body) = _responses[Math.Min(Requests.Count - 1, _responses.Length - 1)];

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
