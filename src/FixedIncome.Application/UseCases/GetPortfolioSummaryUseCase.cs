using FixedIncome.Application.Common;
using FixedIncome.Application.DTOs;
using FixedIncome.Application.Repositories;
using FixedIncome.Domain.Common;
using FixedIncome.Domain.Enums;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Application.UseCases;

public interface IGetPortfolioSummaryUseCase
{
    Task<ApiResponse<PortfolioSummaryResponse>> ExecuteAsync(DateOnly? referenceDate);
}

public class GetPortfolioSummaryUseCase : IGetPortfolioSummaryUseCase
{
    private readonly IPositionRepository _positionRepository;
    private readonly IFixedIncomeAssetRepository _assetRepository;
    private readonly IndexRatesOptions _indexRatesOptions;

    public GetPortfolioSummaryUseCase(
        IPositionRepository positionRepository,
        IFixedIncomeAssetRepository assetRepository,
        IndexRatesOptions indexRatesOptions)
    {
        _positionRepository = positionRepository;
        _assetRepository = assetRepository;
        _indexRatesOptions = indexRatesOptions;
    }

    public async Task<ApiResponse<PortfolioSummaryResponse>> ExecuteAsync(DateOnly? referenceDate)
    {
        var date = referenceDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var positions = await _positionRepository.GetAllAsync();

        // Títulos carregados uma única vez e resolvidos em memória por AssetId: várias
        // posições costumam apontar para o mesmo título, e consultar o repositório dentro
        // do laço abaixo multiplicaria o número de idas ao banco por posição.
        var assets = (await _assetRepository.GetAllAsync()).ToDictionary(asset => asset.Id);
        var rates = _indexRatesOptions.ToIndexRates();

        var projections = new List<(PositionProjection Projection, AssetType AssetType)>();

        foreach (var position in positions)
        {
            if (!assets.TryGetValue(position.AssetId, out var asset))
            {
                return ApiResponse<PortfolioSummaryResponse>.NotFound(
                    $"Título vinculado à posição {position.Id} não encontrado.");
            }

            try
            {
                var projection = position.Project(asset, date, rates);
                projections.Add((projection, asset.AssetType));
            }
            catch (DomainException ex)
            {
                return ApiResponse<PortfolioSummaryResponse>.BadRequest(ex.Message);
            }
        }

        var summary = PortfolioSummary.Create(projections);
        var response = PortfolioSummaryResponse.FromSummary(summary);

        return ApiResponse<PortfolioSummaryResponse>.Ok(response);
    }
}
