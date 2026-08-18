using System.Reflection;
using FixedIncome.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FixedIncome.Infrastructure.Context;

public class FixedIncomeDbContext : DbContext
{
    public FixedIncomeDbContext(DbContextOptions<FixedIncomeDbContext> options)
        : base(options)
    {
    }

    public DbSet<FixedIncomeAsset> FixedIncomeAssets => Set<FixedIncomeAsset>();

    public DbSet<Position> Positions => Set<Position>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
