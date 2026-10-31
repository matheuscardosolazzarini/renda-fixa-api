using FixedIncome.Application.Repositories;
using FixedIncome.Domain.Entities;
using FixedIncome.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace FixedIncome.Infrastructure.Repositories;

public class HolidayRepository : IHolidayRepository
{
    private readonly FixedIncomeDbContext _context;

    public HolidayRepository(FixedIncomeDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Holiday>> GetAllAsync()
    {
        return await _context.Holidays
            .AsNoTracking()
            .OrderBy(h => h.Date)
            .ToListAsync();
    }
}
