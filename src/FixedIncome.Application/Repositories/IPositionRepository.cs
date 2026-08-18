using FixedIncome.Domain.Entities;

namespace FixedIncome.Application.Repositories;

public interface IPositionRepository
{
    Task<IEnumerable<Position>> GetAllAsync();

    Task<Position?> GetByIdAsync(Guid id);

    // Usado para impedir a remoção de um título com aportes e para a consolidação da carteira na F5.
    Task<IEnumerable<Position>> GetByAssetIdAsync(Guid assetId);

    Task AddAsync(Position position);
}
