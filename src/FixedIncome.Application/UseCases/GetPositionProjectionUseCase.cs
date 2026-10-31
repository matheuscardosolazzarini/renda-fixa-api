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
            var date = referenceDate ?? await ResolveDefaultReferenceDateAsync();

            var market = await _marketDataLoader.LoadAsync(position.ApplicationDate, date);
            var projection = position.Project(asset, date, market);

            var response = PositionProjectionResponse.FromProjection(position.Id, asset.Id, projection);

            return ApiResponse<PositionProjectionResponse>.Ok(response);
        }
        catch (DomainException ex)
        {
            return ApiResponse<PositionProjectionResponse>.BadRequest(ex.Message);
        }
    }

    // Mesma regra do resumo da carteira (achado I2): sem data, vale o dia útil seguinte à última
    // cotação de CDI importada; sem nenhuma cotação, a data de hoje do TimeProvider.
    private async Task<DateOnly> ResolveDefaultReferenceDateAsync()
    {
        return await _marketDataLoader.GetDefaultReferenceDateAsync()
            ?? DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
    }
}
