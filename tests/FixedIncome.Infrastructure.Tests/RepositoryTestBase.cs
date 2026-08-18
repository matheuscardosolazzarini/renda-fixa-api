using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace FixedIncome.Infrastructure.Tests;

public abstract class RepositoryTestBase : IDisposable
{
    protected FixedIncomeDbContext Context { get; }

    protected RepositoryTestBase()
    {
        var options = new DbContextOptionsBuilder<FixedIncomeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        Context = new FixedIncomeDbContext(options);
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
        Context.Dispose();
        GC.SuppressFinalize(this);
    }
}
