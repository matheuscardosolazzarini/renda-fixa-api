using FixedIncome.Application.Common;
using FixedIncome.Application.DTOs;
using FixedIncome.Application.Repositories;

namespace FixedIncome.Application.UseCases;

public interface IGetAllPositionsUseCase
{
    Task<ApiResponse<IEnumerable<PositionResponse>>> ExecuteAsync();
}

public class GetAllPositionsUseCase : IGetAllPositionsUseCase
{
    private readonly IPositionRepository _repository;

    public GetAllPositionsUseCase(IPositionRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<IEnumerable<PositionResponse>>> ExecuteAsync()
    {
        var positions = await _repository.GetAllAsync();
        var response = positions.Select(PositionResponse.FromEntity);

        return ApiResponse<IEnumerable<PositionResponse>>.Ok(response);
    }
}
