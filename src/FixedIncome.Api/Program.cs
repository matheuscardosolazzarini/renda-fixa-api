using FixedIncome.Api.Middleware;
using FixedIncome.Application;
using FixedIncome.Application.Common;
using FixedIncome.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection não configurada.");

// IndexRatesOptions é resolvida aqui, no ponto de composição, a partir de IOptions e
// registrada como instância pura — os casos de uso continuam recebendo a classe pura,
// conforme decidido na F4a, sem depender de Microsoft.Extensions.Options.
builder.Services.Configure<IndexRatesOptions>(builder.Configuration.GetSection("IndexRates"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<IndexRatesOptions>>().Value);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

builder.Services.AddControllers();

// Sem isso, o contrato ApiResponse<T> só vale para o que passa pelo model binding: falha de
// desserialização, GUID/data inválidos na rota ou query e campo obrigatório ausente cairiam
// no ProblemDetails padrão do ASP.NET Core, quebrando o formato no caso de erro mais comum
// de qualquer integração — exatamente o que o [ApiController] devolve antes de a requisição
// chegar ao controller.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var fieldMessages = new List<string>();
        var hasBodyLevelFailure = false;

        foreach (var (key, entry) in context.ModelState)
        {
            if (entry is null || entry.Errors.Count == 0)
            {
                continue;
            }

            // "", "request" e "$" descrevem o corpo como um todo — parâmetro [FromBody] não
            // preenchido ou JSON estruturalmente ilegível, sem um campo específico a apontar
            // — não um campo do DTO. Tratados à parte, sem usar a mensagem original do
            // ASP.NET Core.
            if (key is "" or "request" or "$")
            {
                hasBodyLevelFailure = true;
                continue;
            }

            // Chaves "$.campo" vêm de falha de conversão de tipo dentro do corpo JSON; as
            // demais são nome de propriedade (validação) ou de parâmetro de rota/query.
            var fieldName = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key;

            foreach (var error in entry.Errors)
            {
                // A mensagem original do ASP.NET Core não é reaproveitada em nenhum ramo:
                // para "required" ela já é previsível, e para os demais casos (GUID/data
                // inválidos, falha de conversão de tipo) ela é a mensagem bruta do parser —
                // pode incluir path interno e nome completo do tipo .NET do DTO, que não
                // pode vazar. Em ambos os ramos, o nome do campo (a informação realmente
                // útil) já foi extraído da chave do ModelState acima.
                var fieldMessage = error.ErrorMessage.Contains("is required", StringComparison.OrdinalIgnoreCase)
                    ? $"O campo '{fieldName}' é obrigatório."
                    : $"O valor do campo '{fieldName}' é inválido.";

                fieldMessages.Add(fieldMessage);
            }
        }

        fieldMessages = fieldMessages.Distinct().ToList();

        // Uma mensagem específica por campo é sempre mais útil que a genérica de corpo
        // malformado, então a genérica só aparece quando nenhum campo específico pôde ser
        // identificado — evita repetir a mesma causa de duas formas diferentes (ex.: "corpo
        // malformado" e "campo assetId inválido" para a mesma requisição).
        List<string> messages = fieldMessages.Count > 0
            ? fieldMessages
            : hasBodyLevelFailure
                ? ["O corpo da requisição está ausente ou malformado."]
                : ["Requisição inválida."];

        var message = string.Join(" ", messages);

        var response = ApiResponse<object>.BadRequest(message);

        return new ObjectResult(response) { StatusCode = response.StatusCode };
    };
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "FixedIncome API", Version = "v1" });
});

var app = builder.Build();

// Não é chamado Database.Migrate() na inicialização: em ambiente com múltiplas instâncias
// duas poderiam migrar simultaneamente, e uma falha de migration impediria a aplicação de
// subir. A execução é manual:
// dotnet ef database update --project src/FixedIncome.Infrastructure --startup-project src/FixedIncome.Api

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program
{
}
