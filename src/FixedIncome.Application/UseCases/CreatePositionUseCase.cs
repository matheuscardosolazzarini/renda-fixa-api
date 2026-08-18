using FixedIncome.Application.Common;
using FixedIncome.Application.DTOs;
using FixedIncome.Application.Repositories;
using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;

namespace FixedIncome.Application.UseCases;

public interface ICreatePositionUseCase
{
    Task<ApiResponse<PositionResponse>> ExecuteAsync(CreatePositionRequest request);
}

public class CreatePositionUseCase : ICreatePositionUseCase
{
    private readonly IPositionRepository _positionRepository;
    private readonly IFixedIncomeAssetRepository _assetRepository;

    public CreatePositionUseCase(IPositionRepository positionRepository, IFixedIncomeAssetRepository assetRepository)
    {
        _positionRepository = positionRepository;
        _assetRepository = assetRepository;
    }

    public async Task<ApiResponse<PositionResponse>> ExecuteAsync(CreatePositionRequest request)
    {
        var asset = await _assetRepository.GetByIdAsync(request.AssetId);

        if (asset is null)
        {
            return ApiResponse<PositionResponse>.NotFound($"Título com id {request.AssetId} não encontrado.");
        }

        try
        {
            var position = new Position(asset, request.InvestedAmount, request.ApplicationDate);

            await _positionRepository.AddAsync(position);

            return ApiResponse<PositionResponse>.Created(PositionResponse.FromEntity(position));
        }
        catch (DomainException ex)
        {
            return ApiResponse<PositionResponse>.BadRequest(ex.Message);
        }
    }
}
