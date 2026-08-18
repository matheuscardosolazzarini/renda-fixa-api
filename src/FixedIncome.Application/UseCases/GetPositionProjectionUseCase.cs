using FixedIncome.Application.Common;
using FixedIncome.Application.DTOs;
using FixedIncome.Application.Repositories;
using FixedIncome.Domain.Common;

namespace FixedIncome.Application.UseCases;

public interface IGetPositionProjectionUseCase
{
    Task<ApiResponse<PositionProjectionResponse>> ExecuteAsync(Guid positionId, DateOnly referenceDate);
}

public class GetPositionProjectionUseCase : IGetPositionProjectionUseCase
{
    private readonly IPositionRepository _positionRepository;
    private readonly IFixedIncomeAssetRepository _assetRepository;
    private readonly IndexRatesOptions _indexRatesOptions;

    public GetPositionProjectionUseCase(
        IPositionRepository positionRepository,
        IFixedIncomeAssetRepository assetRepository,
        IndexRatesOptions indexRatesOptions)
    {
        _positionRepository = positionRepository;
        _assetRepository = assetRepository;
        _indexRatesOptions = indexRatesOptions;
    }

    public async Task<ApiResponse<PositionProjectionResponse>> ExecuteAsync(Guid positionId, DateOnly referenceDate)
    {
        var position = await _positionRepository.GetByIdAsync(positionId);

        if (position is null)
        {
            return ApiResponse<PositionProjectionResponse>.NotFound($"Posição com id {positionId} não encontrada.");
        }

        var asset = await _assetRepository.GetByIdAsync(position.AssetId);

        if (asset is null)
        {
            return ApiResponse<PositionProjectionResponse>.NotFound(
                $"Título vinculado à posição {positionId} não encontrado.");
        }

        try
        {
            var projection = position.Project(asset, referenceDate, _indexRatesOptions.ToIndexRates());

            var response = PositionProjectionResponse.FromProjection(position.Id, asset.Id, projection);

            return ApiResponse<PositionProjectionResponse>.Ok(response);
        }
        catch (DomainException ex)
        {
            return ApiResponse<PositionProjectionResponse>.BadRequest(ex.Message);
        }
    }
}
