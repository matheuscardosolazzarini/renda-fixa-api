using FixedIncome.Application.Common;
using FixedIncome.Application.DTOs;
using FixedIncome.Application.Repositories;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Application.UseCases;

public interface IGetIndexRatesUseCase
{
    Task<ApiResponse<IEnumerable<IndexRateResponse>>> ExecuteAsync(IndexType indexType, DateOnly from, DateOnly to);
}

public class GetIndexRatesUseCase : IGetIndexRatesUseCase
{
    private readonly IIndexRateRepository _repository;

    public GetIndexRatesUseCase(IIndexRateRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<IEnumerable<IndexRateResponse>>> ExecuteAsync(
        IndexType indexType, DateOnly from, DateOnly to)
    {
        // Só CDI e IPCA têm cotação; o pré-fixado não tem série.
        if (indexType is not (IndexType.Cdi or IndexType.Ipca))
        {
            return ApiResponse<IEnumerable<IndexRateResponse>>.BadRequest("Só há cotação para CDI e IPCA.");
        }

        if (from > to)
        {
            return ApiResponse<IEnumerable<IndexRateResponse>>.BadRequest(
                "A data inicial não pode ser posterior à data final.");
        }

        var rates = await _repository.GetByIndexTypeAndPeriodAsync(indexType, from, to);

        return ApiResponse<IEnumerable<IndexRateResponse>>.Ok(rates.Select(IndexRateResponse.FromEntity));
    }
}
