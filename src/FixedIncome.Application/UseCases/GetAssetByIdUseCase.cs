using FixedIncome.Application.Common;
using FixedIncome.Application.DTOs;
using FixedIncome.Application.Repositories;

namespace FixedIncome.Application.UseCases;

public interface IGetAssetByIdUseCase
{
    Task<ApiResponse<AssetResponse>> ExecuteAsync(Guid id);
}

public class GetAssetByIdUseCase : IGetAssetByIdUseCase
{
    private readonly IFixedIncomeAssetRepository _repository;

    public GetAssetByIdUseCase(IFixedIncomeAssetRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<AssetResponse>> ExecuteAsync(Guid id)
    {
        var asset = await _repository.GetByIdAsync(id);

        if (asset is null)
        {
            return ApiResponse<AssetResponse>.NotFound($"Título com id {id} não encontrado.");
        }

        return ApiResponse<AssetResponse>.Ok(AssetResponse.FromEntity(asset));
    }
}
