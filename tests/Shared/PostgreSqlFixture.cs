using FixedIncome.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace FixedIncome.Tests.Shared;

// Um contêiner PostgreSQL por coleção de testes. Cada teste recebe um banco próprio, copiado de um
// modelo que já tem as migrations aplicadas (as mesmas que o bundle da F8 aplica em produção).
// Este arquivo é compartilhado por arquivo ligado: cada assembly de teste o compila e sobe o seu contêiner.
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private const string TemplateDatabase = "template_fixedincome";

    // Criado no InitializeAsync: o Build() já consulta o Docker e lança DockerUnavailableException,
    // que, num inicializador de campo, sairia do construtor da fixture sem a mensagem em português.
    private PostgreSqlContainer? _container;

    private string _adminConnectionString = string.Empty;

    // Tempo de subida do contêiner e de criação do modelo, para acompanhar o custo da fixture.
    public TimeSpan StartupDuration { get; private set; }

    public async Task InitializeAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _container = new PostgreSqlBuilder("postgres:16").Build();
            await _container.StartAsync();
        }
        catch (Exception ex)
        {
            // Nada de Skip: um verde que não tocou o banco é pior que um vermelho.
            // Qualquer falha aqui vira a mesma orientação: o Docker parado lança DockerUnavailableException,
            // mas um DOCKER_HOST malformado sai como TypeInitializationException, em inglês e sem pista.
            throw CreateStartupFailure(ex);
        }

        // Conexão administrativa sempre ao banco "postgres": o modelo não pode ter conexão aberta
        // durante o CREATE DATABASE ... TEMPLATE.
        _adminConnectionString = BuildConnectionString("postgres");

        await ExecuteAdminAsync($"CREATE DATABASE \"{TemplateDatabase}\"");

        var options = new DbContextOptionsBuilder<FixedIncomeDbContext>()
            .UseNpgsql(BuildConnectionString(TemplateDatabase))
            .Options;

        await using (var context = new FixedIncomeDbContext(options))
        {
            await context.Database.MigrateAsync();
        }

        // Só aqui, no setup, nenhum teste está em andamento: limpar todos os pools durante os testes
        // derrubaria as conexões dos testes vizinhos.
        NpgsqlConnection.ClearAllPools();

        stopwatch.Stop();
        StartupDuration = stopwatch.Elapsed;
    }

    public static InvalidOperationException CreateStartupFailure(Exception cause) =>
        new(
            "Os testes de integração precisam do Docker em execução. Inicie o Docker (e confira a variável "
            + "DOCKER_HOST, se você a definiu) e rode os testes de novo.",
            cause);

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    // Síncrono de propósito, para poder ser chamado no construtor da classe de teste.
    public string CreateDatabase()
    {
        var name = $"test_{Guid.NewGuid():N}";

        ExecuteAdmin($"CREATE DATABASE \"{name}\" TEMPLATE \"{TemplateDatabase}\"");

        return BuildConnectionString(name);
    }

    public void DropDatabase(string connectionString)
    {
        var name = new NpgsqlConnectionStringBuilder(connectionString).Database!;

        // Limpa só o pool deste teste e então derruba o banco, ainda que sobre alguma conexão.
        using var connection = new NpgsqlConnection(connectionString);
        NpgsqlConnection.ClearPool(connection);
        ExecuteAdmin($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
    }

    private string BuildConnectionString(string database)
    {
        var builder = new NpgsqlConnectionStringBuilder(_container!.GetConnectionString())
        {
            Database = database,
        };

        return builder.ConnectionString;
    }

    private void ExecuteAdmin(string sql)
    {
        using var connection = new NpgsqlConnection(_adminConnectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    private async Task ExecuteAdminAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition("PostgreSQL")]
public class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
}
