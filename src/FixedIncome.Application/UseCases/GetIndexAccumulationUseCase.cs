using FixedIncome.Application.Common;
using FixedIncome.Application.DTOs;
using FixedIncome.Application.Services;
using FixedIncome.Domain.Common;
using FixedIncome.Domain.Enums;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Application.UseCases;

public interface IGetIndexAccumulationUseCase
{
    Task<ApiResponse<IndexAccumulationResponse>> ExecuteAsync(IndexType indexType, DateOnly from, DateOnly to);
}

public class GetIndexAccumulationUseCase : IGetIndexAccumulationUseCase
{
    private readonly MarketDataLoader _marketDataLoader;

    public GetIndexAccumulationUseCase(MarketDataLoader marketDataLoader)
    {
        _marketDataLoader = marketDataLoader;
    }

    public async Task<ApiResponse<IndexAccumulationResponse>> ExecuteAsync(
        IndexType indexType, DateOnly from, DateOnly to)
    {
        // Só CDI e IPCA têm cotação; o pré-fixado não tem série.
        if (indexType is not (IndexType.Cdi or IndexType.Ipca))
        {
            return ApiResponse<IndexAccumulationResponse>.BadRequest("Só há cotação para CDI e IPCA.");
        }

        // O domínio valida de novo; a checagem aqui evita carregar o mercado à toa.
        if (from > to)
        {
            return ApiResponse<IndexAccumulationResponse>.BadRequest(
                "A data inicial não pode ser posterior à data final.");
        }

        try
        {
            // DomainException do calendário, da lacuna de cotação e do período sem cotação (RN-13)
            // viram 400, como as demais violações de regra.
            var market = await _marketDataLoader.LoadAsync(from, to);

            var result = indexType == IndexType.Cdi
                ? IndexAccumulation.Cdi(market, from, to)
                : IndexAccumulation.Ipca(market, from, to);

            return ApiResponse<IndexAccumulationResponse>.Ok(new IndexAccumulationResponse(
                indexType, from, to, result.EffectiveTo, result.AccumulatedPercentage));
        }
        catch (DomainException ex)
        {
            return ApiResponse<IndexAccumulationResponse>.BadRequest(ex.Message);
        }
    }
}
