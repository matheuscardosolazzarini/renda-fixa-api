using FixedIncome.Domain.Entities;

namespace FixedIncome.Application.Repositories;

public interface IHolidayRepository
{
    Task<IEnumerable<Holiday>> GetAllAsync();
}
