using FixedIncome.Application.Common;
using FixedIncome.Application.DTOs;
using FixedIncome.Application.Repositories;
using FixedIncome.Application.Services;
using FixedIncome.Domain.Common;

namespace FixedIncome.Application.UseCases;

public interface IGetPositionProjectionUseCase
{
    Task<ApiResponse<PositionProjectionResponse>> ExecuteAsync(Guid positionId, DateOnly? referenceDate);
}

public class GetPositionProjectionUseCase : IGetPositionProjectionUseCase
{
    private readonly IPositionRepository _positionRepository;
    private readonly IFixedIncomeAssetRepository _assetRepository;
    private readonly MarketDataLoader _marketDataLoader;
    private readonly TimeProvider _timeProvider;

    public GetPositionProjectionUseCase(
        IPositionRepository positionRepository,
        IFixedIncomeAssetRepository assetRepository,
        MarketDataLoader marketDataLoader,
        TimeProvider timeProvider)
    {
        _positionRepository = positionRepository;
        _assetRepository = assetRepository;
        _marketDataLoader = marketDataLoader;
        _timeProvider = timeProvider;
    }

    public async Task<ApiResponse<PositionProjectionResponse>> ExecuteAsync(Guid positionId, DateOnly? referenceDate)
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
            // DomainException do calendário (data fora da cobertura) e da cotação ausente (RN-09)
            // viram 400, como as demais violações de regra.
            DateOnly date;

            if (referenceDate is not null)
            {
                date = referenceDate.Value;
            }
            else
            {
                var (defaultDate, fromCdi) = await ResolveDefaultReferenceDateAsync();
                date = defaultDate;

                // B-03: a data padrão não foi escolhida pelo usuário, então o erro precisa dizer
                // qual foi e como ela saiu, para ele saber que pode informar referenceDate.
                if (date < position.ApplicationDate)
                {
                    var origin = fromCdi
                        ? "o dia útil seguinte à última cotação de CDI"
                        : "a data de hoje em Brasília, pois não há cotação de CDI importada";

                    return ApiResponse<PositionProjectionResponse>.BadRequest(
                        $"A data de referência padrão ({date:yyyy-MM-dd}, {origin}) "
                        + $"é anterior à data de aporte ({position.ApplicationDate:yyyy-MM-dd}). "
                        + "Informe referenceDate.");
                }
            }

            var market = await _marketDataLoader.LoadAsync(position.ApplicationDate, date);
            var projection = position.Project(asset, date, market);

            var response = PositionProjectionResponse.FromProjection(position.Id, asset.Id, projection);

            return ApiResponse<PositionProjectionResponse>.Ok(response);
        }
        // R-10: a causa mais comum de CDI ausente é a importação desligada ou ainda não executada.
        catch (DomainException ex) when (ex.Message.StartsWith("Cotação de CDI ausente", StringComparison.Ordinal))
        {
            return ApiResponse<PositionProjectionResponse>.BadRequest(
                $"{ex.Message} A importação de cotações pode estar desligada (IndexRatesImport:Enabled) ou ainda não ter rodado.");
        }
        catch (DomainException ex)
        {
            return ApiResponse<PositionProjectionResponse>.BadRequest(ex.Message);
        }
    }

    // Mesma regra do resumo da carteira (achado I2): sem data, vale o dia útil seguinte à última
    // cotação de CDI importada; sem nenhuma cotação, a data de hoje em Brasília (R-01), nunca a
    // data UTC, que entre 21h e 24h já é a do dia seguinte.
    private async Task<(DateOnly Date, bool FromCdi)> ResolveDefaultReferenceDateAsync()
    {
        var fromCdi = await _marketDataLoader.GetDefaultReferenceDateAsync();

        return fromCdi is not null
            ? (fromCdi.Value, true)
            : (DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime), false);
    }
}
