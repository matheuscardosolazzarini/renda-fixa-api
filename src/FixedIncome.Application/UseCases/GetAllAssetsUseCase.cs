using FixedIncome.Application.Common;
using FixedIncome.Application.DTOs;
using FixedIncome.Application.Repositories;

namespace FixedIncome.Application.UseCases;

public interface IGetAllAssetsUseCase
{
    Task<ApiResponse<IEnumerable<AssetResponse>>> ExecuteAsync();
}

public class GetAllAssetsUseCase : IGetAllAssetsUseCase
{
    private readonly IFixedIncomeAssetRepository _repository;

    public GetAllAssetsUseCase(IFixedIncomeAssetRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<IEnumerable<AssetResponse>>> ExecuteAsync()
    {
        var assets = await _repository.GetAllAsync();
        var response = assets.Select(AssetResponse.FromEntity);

        return ApiResponse<IEnumerable<AssetResponse>>.Ok(response);
    }
}
