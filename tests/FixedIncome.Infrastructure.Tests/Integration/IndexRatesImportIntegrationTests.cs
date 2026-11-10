using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using FixedIncome.Application;
using FixedIncome.Application.Providers;
using FixedIncome.Application.UseCases;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Infrastructure.Context;
using FixedIncome.Infrastructure.Jobs;
using FixedIncome.Infrastructure.Providers;
using FixedIncome.Infrastructure.Repositories;
using FixedIncome.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FixedIncome.Infrastructure.Tests.Integration;

// Importação de cotações de ponta a ponta: SgsIndexRateProvider, repositórios e caso de uso de verdade
// contra o PostgreSQL do teste (com as migrations), e só o Banco Central simulado, na camada HTTP.
// Quem grava (Arrange), quem executa o caso de uso (Act) e quem lê (Assert) usam contextos diferentes.
[Collection("PostgreSQL")]
public class IndexRatesImportIntegrationTests : IDisposable
{
    // 01/05/2025 01:30 UTC ainda é 30/04/2025 22:30 em Brasília. A data de "hoje" do caso de uso é a
    // de Brasília (R-01): se ele usasse a UTC, as requisições terminariam em 01/05/2025.
    private static readonly DateTimeOffset UtcNow = new(2025, 5, 1, 1, 30, 0, TimeSpan.Zero);

    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(30);

    private readonly PostgreSqlFixture _fixture;
    private readonly string _connectionString;
    private readonly DbContextOptions<FixedIncomeDbContext> _options;
    private readonly List<IDisposable> _disposables = [];

    public IndexRatesImportIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        _connectionString = fixture.CreateDatabase();
        _options = new DbContextOptionsBuilder<FixedIncomeDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
    }

    [Fact]
    public async Task Primeira_execucao_grava_o_cdi_e_o_ipca_buscados_desde_2020_01_01_ate_hoje()
    {
        // Arrange
        // Dias úteis de 2025: terça 15, quarta 16 e quinta 17/04, e terça 22/04 (depois da Paixão, 18/04,
        // e de Tiradentes, 21/04). O IPCA mensal é datado no dia 1; 01/04 é só massa de teste
        // (no mercado, o IPCA de abril só sai em maio).
        var stub = new SgsHandlerStub(
            cdi: [("15/04/2025", "0.055131"), ("16/04/2025", "0.055132"), ("17/04/2025", "0.055133"), ("22/04/2025", "0.055134")],
            ipca: [("01/02/2025", "1.31"), ("01/03/2025", "0.56"), ("01/04/2025", "0.43")]);
        var useCase = CreateUseCase(stub);

        // Act
        var response = await useCase.ExecuteAsync();

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(4, response.Data!.CdiImported);
        Assert.Equal(0, response.Data.CdiDiscarded);
        Assert.Equal(3, response.Data.IpcaImported);
        Assert.Equal(
            new[]
            {
                "/dados/serie/bcdata.sgs.12/dados?formato=json&dataInicial=01/01/2020&dataFinal=30/04/2025",
                "/dados/serie/bcdata.sgs.433/dados?formato=json&dataInicial=01/01/2020&dataFinal=30/04/2025",
            },
            stub.RequestedPaths);
        Assert.Equal(
            new[]
            {
                (new DateOnly(2025, 4, 15), 0.055131m),
                (new DateOnly(2025, 4, 16), 0.055132m),
                (new DateOnly(2025, 4, 17), 0.055133m),
                (new DateOnly(2025, 4, 22), 0.055134m),
            },
            await ReadRatesAsync(IndexType.Cdi));
        Assert.Equal(
            new[]
            {
                (new DateOnly(2025, 2, 1), 1.31m),
                (new DateOnly(2025, 3, 1), 0.56m),
                (new DateOnly(2025, 4, 1), 0.43m),
            },
            await ReadRatesAsync(IndexType.Ipca));
    }

    [Fact]
    public async Task Segunda_execucao_no_mesmo_dia_nao_duplica_nem_altera_os_registros()
    {
        // Arrange
        var stub = new SgsHandlerStub(
            cdi: [("16/04/2025", "0.055132"), ("17/04/2025", "0.055133"), ("22/04/2025", "0.055134")],
            ipca: [("01/03/2025", "0.56"), ("01/04/2025", "0.43")]);
        var first = await CreateUseCase(stub).ExecuteAsync();
        var afterFirst = await ReadAllAsync();
        stub.ClearRequests();

        // Act
        // Um caso de uso novo, com contexto novo, como a próxima execução do job.
        var second = await CreateUseCase(stub).ExecuteAsync();

        // Assert
        Assert.Equal(3, first.Data!.CdiImported);
        Assert.Equal(2, first.Data.IpcaImported);
        Assert.Equal(200, second.StatusCode);
        Assert.Equal(0, second.Data!.CdiImported);
        Assert.Equal(0, second.Data.IpcaImported);
        Assert.Equal(5, afterFirst.Count);
        // Mesmos registros, com os mesmos Ids e valores: nada foi duplicado nem reinserido.
        Assert.Equal(afterFirst, await ReadAllAsync());
    }

    [Fact]
    public async Task Execucao_incremental_do_cdi_pula_paixao_fim_de_semana_e_tiradentes_ate_o_proximo_dia_util()
    {
        // Arrange
        // Última cotação gravada: quinta 17/04/2025. Depois vêm Paixão (sex 18/04), sábado, domingo e
        // Tiradentes (seg 21/04): o próximo dia útil é terça 22/04, não o dia seguinte (18/04).
        await SeedAsync(
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 17), 0.055133m),
            new IndexRate(IndexType.Ipca, new DateOnly(2025, 4, 1), 0.43m));
        var stub = new SgsHandlerStub(
            cdi: [("17/04/2025", "0.055133"), ("22/04/2025", "0.055134"), ("23/04/2025", "0.055135")],
            ipca: []);

        // Act
        var response = await CreateUseCase(stub).ExecuteAsync();

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(
            new[] { "/dados/serie/bcdata.sgs.12/dados?formato=json&dataInicial=22/04/2025&dataFinal=30/04/2025" },
            stub.RequestedPaths);
        Assert.Equal(2, response.Data!.CdiImported);
        Assert.Equal(
            new[]
            {
                (new DateOnly(2025, 4, 17), 0.055133m),
                (new DateOnly(2025, 4, 22), 0.055134m),
                (new DateOnly(2025, 4, 23), 0.055135m),
            },
            await ReadRatesAsync(IndexType.Cdi));
    }

    [Fact]
    public async Task Cotacao_de_cdi_em_dia_nao_util_e_descartada_e_nao_grava_linha()
    {
        // Arrange
        // 19/04/2025 é sábado e 21/04/2025 é Tiradentes (feriado da tabela semeada pelas migrations).
        var stub = new SgsHandlerStub(
            cdi: [("17/04/2025", "0.055133"), ("19/04/2025", "0.055140"), ("21/04/2025", "0.055141"), ("22/04/2025", "0.055134")],
            ipca: []);

        // Act
        var response = await CreateUseCase(stub).ExecuteAsync();

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(2, response.Data!.CdiImported);
        Assert.Equal(2, response.Data.CdiDiscarded);
        Assert.Equal(
            new[]
            {
                (new DateOnly(2025, 4, 17), 0.055133m),
                (new DateOnly(2025, 4, 22), 0.055134m),
            },
            await ReadRatesAsync(IndexType.Cdi));
    }

    [Fact]
    public async Task Ipca_ja_importado_ate_o_mes_corrente_nao_e_buscado_de_novo()
    {
        // Arrange
        // Hoje é 30/04/2025. O cenário semeia o IPCA de abril (01/04) só para exercitar o corte from > hoje:
        // a busca recomeçaria em 01/05, depois de hoje, e por isso não existe. No mercado, o IPCA de abril
        // só sai em maio; aqui a data serve apenas de massa.
        // O CDI está atrasado um dia (29/04), então a requisição de CDI existe e a de IPCA não.
        await SeedAsync(
            new IndexRate(IndexType.Ipca, new DateOnly(2025, 4, 1), 0.43m),
            new IndexRate(IndexType.Cdi, new DateOnly(2025, 4, 29), 0.055133m));
        var stub = new SgsHandlerStub(
            cdi: [("30/04/2025", "0.055134")],
            ipca: [("01/04/2025", "0.43"), ("01/05/2025", "0.99")]);

        // Act
        var response = await CreateUseCase(stub).ExecuteAsync();

        // Assert
        Assert.Equal(200, response.StatusCode);
        Assert.Equal(0, response.Data!.IpcaImported);
        Assert.DoesNotContain(stub.RequestedPaths, p => p.Contains("bcdata.sgs.433"));
        Assert.Contains(stub.RequestedPaths, p => p.Contains("bcdata.sgs.12"));
        Assert.Equal(
            new[] { (new DateOnly(2025, 4, 1), 0.43m) },
            await ReadRatesAsync(IndexType.Ipca));
    }

    [Fact]
    public async Task Job_de_importacao_inteiro_grava_as_cotacoes_no_banco_via_conteiner_de_di()
    {
        // Arrange
        var stub = new SgsHandlerStub(
            cdi: [("16/04/2025", "0.055132"), ("17/04/2025", "0.055133"), ("22/04/2025", "0.055134")],
            ipca: [("01/03/2025", "0.56"), ("01/04/2025", "0.43")]);
        var services = new ServiceCollection();
        services.AddLogging();
        // Antes do AddApplication, que registra o relógio de produção com TryAdd.
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(UtcNow));
        services.AddApplication();
        services.AddInfrastructure(_connectionString);
        // Troca o handler de rede do provedor pelo Banco Central simulado; a resiliência e a checagem
        // do corpo de AddInfrastructure continuam no pipeline.
        services.AddHttpClient<IIndexRateProvider, SgsIndexRateProvider>()
            .ConfigurePrimaryHttpMessageHandler(() => stub);
        services.AddIndexRatesImportJob(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["IndexRatesImport:Enabled"] = "true" })
            .Build());
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var job = provider.GetRequiredService<IndexRatesImportJob>();

        // Act
        await job.StartAsync(CancellationToken.None);
        try
        {
            await WaitUntilAsync(
                async () => (await ReadRatesAsync(IndexType.Cdi)).Count == 3
                            && (await ReadRatesAsync(IndexType.Ipca)).Count == 2);
        }
        finally
        {
            await job.StopAsync(CancellationToken.None);
        }

        // Assert
        Assert.Equal(
            new[]
            {
                (new DateOnly(2025, 4, 16), 0.055132m),
                (new DateOnly(2025, 4, 17), 0.055133m),
                (new DateOnly(2025, 4, 22), 0.055134m),
            },
            await ReadRatesAsync(IndexType.Cdi));
        Assert.Equal(
            new[]
            {
                (new DateOnly(2025, 3, 1), 0.56m),
                (new DateOnly(2025, 4, 1), 0.43m),
            },
            await ReadRatesAsync(IndexType.Ipca));
        Assert.Equal(TaskStatus.RanToCompletion, job.ExecuteTask!.Status);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        _fixture.DropDatabase(_connectionString);

        GC.SuppressFinalize(this);
    }

    private FixedIncomeDbContext NewContext()
    {
        var context = new FixedIncomeDbContext(_options);
        _disposables.Add(context);
        return context;
    }

    // Caso de uso real sobre um contexto só dele, diferente dos usados para gravar e ler no teste.
    private ImportIndexRatesUseCase CreateUseCase(SgsHandlerStub stub)
    {
        var context = NewContext();
        var client = new HttpClient(stub) { BaseAddress = SgsIndexRateProvider.BaseAddress };
        _disposables.Add(client);

        return new ImportIndexRatesUseCase(
            new SgsIndexRateProvider(client, NullLogger<SgsIndexRateProvider>.Instance),
            new IndexRateRepository(context),
            new HolidayRepository(context),
            new FixedTimeProvider(UtcNow),
            NullLogger<ImportIndexRatesUseCase>.Instance);
    }

    private async Task SeedAsync(params IndexRate[] rates)
    {
        await using var context = new FixedIncomeDbContext(_options);
        context.IndexRates.AddRange(rates);
        await context.SaveChangesAsync();
    }

    private async Task<List<(DateOnly Date, decimal Value)>> ReadRatesAsync(IndexType type)
    {
        await using var context = new FixedIncomeDbContext(_options);
        var rates = await context.IndexRates
            .AsNoTracking()
            .Where(r => r.IndexType == type)
            .OrderBy(r => r.ReferenceDate)
            .ToListAsync();

        return rates.Select(r => (r.ReferenceDate, r.Value)).ToList();
    }

    private async Task<List<(Guid Id, IndexType Type, DateOnly Date, decimal Value)>> ReadAllAsync()
    {
        await using var context = new FixedIncomeDbContext(_options);
        var rates = await context.IndexRates
            .AsNoTracking()
            .OrderBy(r => r.IndexType)
            .ThenBy(r => r.ReferenceDate)
            .ToListAsync();

        return rates.Select(r => (r.Id, r.IndexType, r.ReferenceDate, r.Value)).ToList();
    }

    // Espera por condição com prazo fixo, sem pausa fixa: a primeira checagem é imediata.
    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var stopwatch = Stopwatch.StartNew();

        while (!await condition())
        {
            if (stopwatch.Elapsed > WaitLimit)
            {
                Assert.Fail($"As cotações não apareceram no banco em {WaitLimit.TotalSeconds:0} s.");
            }

            await Task.Delay(50);
        }
    }

    // Relógio fixo: o instante é o informado e o fuso local é o de Brasília (UTC-3, sem horário de
    // verão desde 2019), para GetLocalNow() não depender do fuso nem do tzdata da máquina.
    private sealed class FixedTimeProvider : TimeProvider
    {
        private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.CreateCustomTimeZone(
            "America/Sao_Paulo", TimeSpan.FromHours(-3), "Brasília", "Brasília");

        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override TimeZoneInfo LocalTimeZone => SaoPaulo;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    // Banco Central simulado: responde pela série da URL (12 = CDI, 433 = IPCA) como o SGS, só com as
    // cotações dentro de dataInicial..dataFinal; período sem cotação vira 404 "Value(s) not found";
    // dataInicial igual a dataFinal vira 400. Registra as URLs recebidas.
    private sealed class SgsHandlerStub : HttpMessageHandler
    {
        private static readonly Regex SeriesPattern = new(@"bcdata\.sgs\.(\d+)", RegexOptions.Compiled);

        private readonly Dictionary<int, List<(DateOnly Date, string Raw, string Value)>> _series;
        private readonly ConcurrentQueue<string> _requests = new();

        public SgsHandlerStub((string Date, string Value)[] cdi, (string Date, string Value)[] ipca)
        {
            _series = new Dictionary<int, List<(DateOnly, string, string)>>
            {
                [12] = Parse(cdi),
                [433] = Parse(ipca),
            };
        }

        public IReadOnlyList<string> RequestedPaths => _requests.ToList();

        public void ClearRequests() => _requests.Clear();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            _requests.Enqueue(uri.PathAndQuery);

            var seriesMatch = SeriesPattern.Match(uri.AbsolutePath);
            if (!seriesMatch.Success || !_series.TryGetValue(int.Parse(seriesMatch.Groups[1].Value), out var quotes))
            {
                throw new InvalidOperationException($"Série desconhecida na URL: {uri}");
            }

            var query = HttpUtility.ParseQueryString(uri.Query);
            if (query["dataInicial"] == query["dataFinal"])
            {
                return Task.FromResult(Respond(
                    HttpStatusCode.BadRequest, """{"erro":"DataFinal deve ser maior que dataInicial."}"""));
            }

            var from = DateOnly.ParseExact(query["dataInicial"]!, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            var to = DateOnly.ParseExact(query["dataFinal"]!, "dd/MM/yyyy", CultureInfo.InvariantCulture);
            var inPeriod = quotes.Where(q => q.Date >= from && q.Date <= to).ToList();

            if (inPeriod.Count == 0)
            {
                return Task.FromResult(Respond(HttpStatusCode.NotFound, "Value(s) not found"));
            }

            var items = inPeriod.Select(q => $$"""{"data":"{{q.Raw}}","valor":"{{q.Value}}"}""");
            return Task.FromResult(Respond(HttpStatusCode.OK, $"[{string.Join(",", items)}]"));
        }

        private static List<(DateOnly, string, string)> Parse((string Date, string Value)[] quotes) =>
            quotes
                .Select(q => (DateOnly.ParseExact(q.Date, "dd/MM/yyyy", CultureInfo.InvariantCulture), q.Date, q.Value))
                .ToList();

        private static HttpResponseMessage Respond(HttpStatusCode status, string body) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
