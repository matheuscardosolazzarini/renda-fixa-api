using FixedIncome.Application.Common;
using FixedIncome.Application.Repositories;

namespace FixedIncome.Application.UseCases;

public interface IDeleteAssetUseCase
{
    Task<ApiResponse<object>> ExecuteAsync(Guid id);
}

public class DeleteAssetUseCase : IDeleteAssetUseCase
{
    private readonly IFixedIncomeAssetRepository _assetRepository;
    private readonly IPositionRepository _positionRepository;

    public DeleteAssetUseCase(IFixedIncomeAssetRepository assetRepository, IPositionRepository positionRepository)
    {
        _assetRepository = assetRepository;
        _positionRepository = positionRepository;
    }

    public async Task<ApiResponse<object>> ExecuteAsync(Guid id)
    {
        var asset = await _assetRepository.GetByIdAsync(id);

        if (asset is null)
        {
            return ApiResponse<object>.NotFound($"Título com id {id} não encontrado.");
        }

        var positions = await _positionRepository.GetByAssetIdAsync(id);

        if (positions.Any())
        {
            return ApiResponse<object>.BadRequest("Título possui aportes e não pode ser removido.");
        }

        await _assetRepository.DeleteAsync(asset);

        return ApiResponse<object>.NoContent();
    }
}
