using FixedIncome.Application.Common;
using FixedIncome.Domain.Common;

namespace FixedIncome.Api.Middleware;

// Rede de segurança para exceção de domínio ou não tratada que escape de um caso de uso.
// Os casos de uso já convertem DomainException em BadRequest; isto não substitui aquele
// tratamento, cobre apenas o que não foi capturado ali.
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Violação de regra de negócio capturada pelo middleware.");
            await WriteResponseAsync(context, ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exceção não tratada capturada pelo middleware.");
            await WriteResponseAsync(
                context, ApiResponse<object>.InternalError("Ocorreu um erro interno. Tente novamente mais tarde."));
        }
    }

    private static Task WriteResponseAsync(HttpContext context, ApiResponse<object> response)
    {
        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        context.Response.StatusCode = response.StatusCode;
        return context.Response.WriteAsJsonAsync(response);
    }
}
