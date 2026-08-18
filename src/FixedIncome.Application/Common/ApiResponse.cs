namespace FixedIncome.Application.Common;

public class ApiResponse<T>
{
    public int StatusCode { get; }
    public bool Success { get; }
    public string? Message { get; }
    public T? Data { get; }

    private ApiResponse(int statusCode, bool success, string? message, T? data)
    {
        StatusCode = statusCode;
        Success = success;
        Message = message;
        Data = data;
    }

    // Nomeada Ok em vez de Success: colide com a propriedade de instância Success (bool)
    // exigida pelo envelope de resposta — C# não permite propriedade e método com o
    // mesmo nome na mesma classe.
    public static ApiResponse<T> Ok(T data) => new(200, true, null, data);

    public static ApiResponse<T> Created(T data) => new(201, true, null, data);

    public static ApiResponse<T> NoContent() => new(204, true, null, default);

    public static ApiResponse<T> BadRequest(string message) => new(400, false, message, default);

    public static ApiResponse<T> NotFound(string message) => new(404, false, message, default);

    public static ApiResponse<T> InternalError(string message) => new(500, false, message, default);
}
