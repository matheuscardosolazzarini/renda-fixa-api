using FixedIncome.Application.Repositories;
using FixedIncome.Domain.Entities;
using FixedIncome.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace FixedIncome.Infrastructure.Repositories;

public class PositionRepository : IPositionRepository
{
    private readonly FixedIncomeDbContext _context;

    public PositionRepository(FixedIncomeDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Position>> GetAllAsync()
    {
        return await _context.Positions
            .AsNoTracking()
            .OrderByDescending(p => p.ApplicationDate)
            .ToListAsync();
    }

    public async Task<Position?> GetByIdAsync(Guid id)
    {
        return await _context.Positions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IEnumerable<Position>> GetByAssetIdAsync(Guid assetId)
    {
        return await _context.Positions
            .AsNoTracking()
            .Where(p => p.AssetId == assetId)
            .ToListAsync();
    }

    public async Task AddAsync(Position position)
    {
        await _context.Positions.AddAsync(position);
        await _context.SaveChangesAsync();
    }
}
