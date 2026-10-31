using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using FixedIncome.Application.Providers;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FixedIncome.Infrastructure.Providers;

// Cliente do SGS (Sistema Gerenciador de Séries Temporais) do Banco Central.
// Séries: 12 = CDI (% ao dia), 433 = IPCA (% no mês, datado no dia 1).
public class SgsIndexRateProvider : IIndexRateProvider
{
    public static readonly Uri BaseAddress = new("https://api.bcb.gov.br/");

    private const string DateFormat = "dd/MM/yyyy";

    // A API limita o período das consultas de séries diárias a 10 anos.
    private const int MaxWindowYears = 10;

    private const int MaxErrorBodyLength = 500;

    private readonly HttpClient _httpClient;
    private readonly ILogger<SgsIndexRateProvider> _logger;

    public SgsIndexRateProvider(HttpClient httpClient, ILogger<SgsIndexRateProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IEnumerable<IndexRate>> FetchAsync(IndexType type, DateOnly from, DateOnly to)
    {
        var series = type switch
        {
            IndexType.Cdi => 12,
            IndexType.Ipca => 433,
            _ => throw new ArgumentException("Só há série no SGS para CDI e IPCA.", nameof(type))
        };

        // Período invertido não tem cotação: lista vazia e nenhuma chamada. Cobre a importação
        // incremental quando a última data importada já é a de hoje.
        var rates = new List<IndexRate>();

        // Janelas consecutivas de até 10 anos, sem sobreposição nem lacuna.
        for (var windowStart = from; windowStart <= to;)
        {
            var windowEnd = windowStart.AddYears(MaxWindowYears).AddDays(-1);
            if (windowEnd > to)
            {
                windowEnd = to;
            }

            rates.AddRange(await FetchWindowAsync(type, series, windowStart, windowEnd));

            windowStart = windowEnd.AddDays(1);
        }

        return rates;
    }

    private async Task<IEnumerable<IndexRate>> FetchWindowAsync(IndexType type, int series, DateOnly from, DateOnly to)
    {
        // O SGS responde 400 quando dataInicial == dataFinal. Para um período de um dia, a consulta
        // começa na véspera e o resultado é filtrado abaixo para manter só o período pedido.
        var queryFrom = from == to ? from.AddDays(-1) : from;

        var url = $"dados/serie/bcdata.sgs.{series}/dados?formato=json" +
                  $"&dataInicial={Format(queryFrom)}&dataFinal={Format(to)}";

        using var response = await _httpClient.GetAsync(url);

        // Só o 404 significa período sem dado publicado: o BC responde 404 com "Value(s) not found".
        // Qualquer outro erro (400, 406 por janela acima de 10 anos, 408, 429, 5xx...) é falha
        // real e propaga com status e corpo, para não virar "sem cotações" em silêncio.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning(
                "SGS série {Series} respondeu 404 para {From} a {To}; tratado como sem cotações.",
                series, FormatForLog(queryFrom), FormatForLog(to));
            return Array.Empty<IndexRate>();
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            if (body.Length > MaxErrorBodyLength)
            {
                body = body[..MaxErrorBodyLength];
            }

            throw new HttpRequestException(
                $"SGS série {series} respondeu {(int)response.StatusCode} para {FormatForLog(queryFrom)} a {FormatForLog(to)}: {body}",
                null,
                response.StatusCode);
        }

        var items = await response.Content.ReadFromJsonAsync<List<SgsItem>>() ?? new List<SgsItem>();

        // Violação de invariante (CDI negativo, IPCA fora do dia 1) sobe como DomainException:
        // dado inválido da fonte não deve ser gravado nem silenciado.
        // O filtro descarta o que a consulta trouxe fora do período pedido: a cotação da véspera
        // (período de um dia) e, na série mensal do IPCA, o mês anterior que o SGS devolve junto.
        return items
            .Select(item => (Date: DateOnly.ParseExact(item.Data, DateFormat, CultureInfo.InvariantCulture), item.Valor))
            .Where(item => item.Date >= from && item.Date <= to)
            .Select(item => new IndexRate(
                type,
                item.Date,
                decimal.Parse(item.Valor, NumberStyles.Float, CultureInfo.InvariantCulture)))
            .ToList();
    }

    private static string Format(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    // Datas em log e mensagens de erro em ano-mês-dia, sem depender da cultura do processo.
    private static string FormatForLog(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed record SgsItem(string Data, string Valor);
}
