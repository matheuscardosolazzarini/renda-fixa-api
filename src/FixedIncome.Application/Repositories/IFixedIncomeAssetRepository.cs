using FixedIncome.Domain.Entities;

namespace FixedIncome.Application.Repositories;

public interface IFixedIncomeAssetRepository
{
    Task<IEnumerable<FixedIncomeAsset>> GetAllAsync();

    Task<FixedIncomeAsset?> GetByIdAsync(Guid id);

    Task AddAsync(FixedIncomeAsset asset);

    Task UpdateAsync(FixedIncomeAsset asset);

    Task DeleteAsync(FixedIncomeAsset asset);
}
