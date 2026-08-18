using FixedIncome.Application.Repositories;
using FixedIncome.Domain.Entities;
using FixedIncome.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace FixedIncome.Infrastructure.Repositories;

public class FixedIncomeAssetRepository : IFixedIncomeAssetRepository
{
    private readonly FixedIncomeDbContext _context;

    public FixedIncomeAssetRepository(FixedIncomeDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<FixedIncomeAsset>> GetAllAsync()
    {
        return await _context.FixedIncomeAssets
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ToListAsync();
    }

    public async Task<FixedIncomeAsset?> GetByIdAsync(Guid id)
    {
        return await _context.FixedIncomeAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task AddAsync(FixedIncomeAsset asset)
    {
        await _context.FixedIncomeAssets.AddAsync(asset);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(FixedIncomeAsset asset)
    {
        _context.FixedIncomeAssets.Update(asset);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(FixedIncomeAsset asset)
    {
        _context.FixedIncomeAssets.Remove(asset);
        await _context.SaveChangesAsync();
    }
}
