using FixedIncome.Application.Common;
using FixedIncome.Application.DTOs;
using FixedIncome.Application.Repositories;
using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Application.UseCases;

public interface ICreateAssetUseCase
{
    Task<ApiResponse<AssetResponse>> ExecuteAsync(CreateAssetRequest request);
}

public class CreateAssetUseCase : ICreateAssetUseCase
{
    private readonly IFixedIncomeAssetRepository _repository;

    public CreateAssetUseCase(IFixedIncomeAssetRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<AssetResponse>> ExecuteAsync(CreateAssetRequest request)
    {
        if (!Enum.TryParse<AssetType>(request.AssetType, ignoreCase: true, out var assetType)
            || !Enum.IsDefined(assetType))
        {
            return ApiResponse<AssetResponse>.BadRequest($"AssetType inválido: {request.AssetType}.");
        }

        if (!Enum.TryParse<IndexType>(request.IndexType, ignoreCase: true, out var indexType)
            || !Enum.IsDefined(indexType))
        {
            return ApiResponse<AssetResponse>.BadRequest($"IndexType inválido: {request.IndexType}.");
        }

        try
        {
            var asset = new FixedIncomeAsset(
                request.Name,
                request.Issuer,
                assetType,
                indexType,
                request.Rate,
                request.IssueDate,
                request.MaturityDate);

            await _repository.AddAsync(asset);

            return ApiResponse<AssetResponse>.Created(AssetResponse.FromEntity(asset));
        }
        catch (DomainException ex)
        {
            return ApiResponse<AssetResponse>.BadRequest(ex.Message);
        }
    }
}
