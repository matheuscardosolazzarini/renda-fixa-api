using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Infrastructure.Context;
using FixedIncome.Tests.Shared;
using Microsoft.EntityFrameworkCore;

namespace FixedIncome.Infrastructure.Tests;

public abstract class RepositoryTestBase : IDisposable
{
    private readonly PostgreSqlFixture _fixture;
    private readonly string _connectionString;
    private readonly DbContextOptions<FixedIncomeDbContext> _options;
    private readonly List<FixedIncomeDbContext> _contexts = [];

    // Contexto "padrão" do teste. Para provar persistência, grave com ele e leia com CriarContexto().
    protected FixedIncomeDbContext Context { get; }

    // Cria um banco próprio para o teste, copiado do modelo com as migrations aplicadas.
    protected RepositoryTestBase(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        _connectionString = fixture.CreateDatabase();
        _options = new DbContextOptionsBuilder<FixedIncomeDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        Context = CriarContexto();
    }

    // Abre um contexto novo (sem change tracker) sobre o mesmo banco do teste.
    protected FixedIncomeDbContext CriarContexto()
    {
        var context = new FixedIncomeDbContext(_options);
        _contexts.Add(context);
        return context;
    }

    protected static FixedIncomeAsset CriarTitulo(string name = "CDB Banco X")
    {
        return new FixedIncomeAsset(
            name,
            "Banco X",
            AssetType.Cdb,
            IndexType.PreFixed,
            12m,
            new DateOnly(2025, 1, 1),
            new DateOnly(2027, 1, 1));
    }

    public void Dispose()
    {
        foreach (var context in _contexts)
        {
            context.Dispose();
        }

        _fixture.DropDatabase(_connectionString);

        GC.SuppressFinalize(this);
    }
}
