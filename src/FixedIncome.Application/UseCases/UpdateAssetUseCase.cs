using FixedIncome.Application.Common;
using FixedIncome.Application.DTOs;
using FixedIncome.Application.Repositories;
using FixedIncome.Domain.Common;

namespace FixedIncome.Application.UseCases;

public interface IUpdateAssetUseCase
{
    Task<ApiResponse<AssetResponse>> ExecuteAsync(Guid id, UpdateAssetRequest request);
}

public class UpdateAssetUseCase : IUpdateAssetUseCase
{
    private readonly IFixedIncomeAssetRepository _assetRepository;
    private readonly IPositionRepository _positionRepository;

    public UpdateAssetUseCase(IFixedIncomeAssetRepository assetRepository, IPositionRepository positionRepository)
    {
        _assetRepository = assetRepository;
        _positionRepository = positionRepository;
    }

    public async Task<ApiResponse<AssetResponse>> ExecuteAsync(Guid id, UpdateAssetRequest request)
    {
        var asset = await _assetRepository.GetByIdAsync(id);

        if (asset is null)
        {
            return ApiResponse<AssetResponse>.NotFound($"Título com id {id} não encontrado.");
        }

        try
        {
            var positions = await _positionRepository.GetByAssetIdAsync(id);
            var hasPositions = positions.Any();

            asset.Update(
                request.Name,
                request.Issuer,
                request.Rate,
                request.IssueDate,
                request.MaturityDate,
                hasPositions);

            await _assetRepository.UpdateAsync(asset);

            return ApiResponse<AssetResponse>.Ok(AssetResponse.FromEntity(asset));
        }
        catch (DomainException ex)
        {
            return ApiResponse<AssetResponse>.BadRequest(ex.Message);
        }
    }
}
