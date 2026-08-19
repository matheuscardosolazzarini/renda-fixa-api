using FixedIncome.Api.Middleware;
using FixedIncome.Application;
using FixedIncome.Application.Common;
using FixedIncome.Infrastructure;
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
