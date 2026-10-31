using FixedIncome.Application.Repositories;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace FixedIncome.Infrastructure.Repositories;

public class IndexRateRepository : IIndexRateRepository
{
    private readonly FixedIncomeDbContext _context;

    public IndexRateRepository(FixedIncomeDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<IndexRate>> GetByIndexTypeAndPeriodAsync(IndexType indexType, DateOnly from, DateOnly to)
    {
        return await _context.IndexRates
            .AsNoTracking()
            .Where(r => r.IndexType == indexType && r.ReferenceDate >= from && r.ReferenceDate <= to)
            .OrderBy(r => r.ReferenceDate)
            .ToListAsync();
    }

    public async Task<IndexRate?> GetLastBeforeAsync(IndexType indexType, DateOnly date)
    {
        return await _context.IndexRates
            .AsNoTracking()
            .Where(r => r.IndexType == indexType && r.ReferenceDate < date)
            .OrderByDescending(r => r.ReferenceDate)
            .FirstOrDefaultAsync();
    }

    public async Task<DateOnly?> GetLastReferenceDateAsync(IndexType indexType)
    {
        return await _context.IndexRates
            .Where(r => r.IndexType == indexType)
            .Select(r => (DateOnly?)r.ReferenceDate)
            .MaxAsync();
    }

    public async Task UpsertAsync(IEnumerable<IndexRate> rates)
    {
        var incoming = rates.ToList();
        if (incoming.Count == 0)
        {
            return;
        }

        var minDate = incoming.Min(r => r.ReferenceDate);
        var maxDate = incoming.Max(r => r.ReferenceDate);
        var indexTypes = incoming.Select(r => r.IndexType).Distinct().ToList();

        // Carrega com rastreamento: o valor das linhas existentes é alterado no próprio contexto.
        var existing = await _context.IndexRates
            .Where(r => indexTypes.Contains(r.IndexType) && r.ReferenceDate >= minDate && r.ReferenceDate <= maxDate)
            .ToListAsync();

        var existingByKey = existing.ToDictionary(r => (r.IndexType, r.ReferenceDate));

        foreach (var rate in incoming)
        {
            var key = (rate.IndexType, rate.ReferenceDate);

            if (existingByKey.TryGetValue(key, out var current))
            {
                // Value só tem setter privado na entidade, então a alteração passa pelo EF.
                _context.Entry(current).Property(r => r.Value).CurrentValue = rate.Value;
            }
            else
            {
                await _context.IndexRates.AddAsync(rate);
                existingByKey[key] = rate;
            }
        }

        await _context.SaveChangesAsync();
    }
}
