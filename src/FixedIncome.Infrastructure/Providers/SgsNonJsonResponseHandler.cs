using System.Text.Json;

namespace FixedIncome.Infrastructure.Providers;

// O SGS às vezes responde 200 com uma página HTML no lugar do JSON, ou com o JSON cortado no meio.
// O Polly só enxerga o que passa pelo pipeline do HttpClient: uma JsonException lançada pelo
// provedor, depois da resposta lida, nunca dispara retry. Por isso este handler é registrado depois
// do handler de resiliência (dentro dele) e converte o corpo que não começa com { ou [ ou que não é
// JSON bem formado por inteiro em HttpRequestException, que o retry trata como transitória.
// A validação usa JsonDocument.Parse com as opções padrão, as mesmas que o provedor aplica ao ler o corpo.
public sealed class SgsNonJsonResponseHandler : DelegatingHandler
{
    private const int MaxSnippetLength = 100;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        // Status de erro segue para o provedor, que já distingue 404 (sem dado) das demais falhas.
        if (!response.IsSuccessStatusCode)
        {
            return response;
        }

        // Bufferiza para o provedor ainda conseguir ler o corpo depois desta checagem.
        await response.Content.LoadIntoBufferAsync();
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        var start = body.TrimStart();
        if ((start.StartsWith('{') || start.StartsWith('[')) && IsWellFormedJson(body))
        {
            return response;
        }

        var status = response.StatusCode;
        response.Dispose();

        var snippet = body.Length > MaxSnippetLength ? body[..MaxSnippetLength] : body;
        throw new HttpRequestException(
            $"SGS respondeu {(int)status} com corpo que não é JSON: {snippet}",
            null,
            status);
    }

    private static bool IsWellFormedJson(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
