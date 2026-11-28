using FixedIncome.Application.Common;
using FixedIncome.Application.DTOs;
using FixedIncome.Application.Repositories;
using FixedIncome.Application.Services;
using FixedIncome.Domain.Common;
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
    private readonly PositionProjector _projector;

    public GetPortfolioSummaryUseCase(
        IPositionRepository positionRepository,
        IFixedIncomeAssetRepository assetRepository,
        PositionProjector projector)
    {
        _positionRepository = positionRepository;
        _assetRepository = assetRepository;
        _projector = projector;
    }

    public async Task<ApiResponse<PortfolioSummaryResponse>> ExecuteAsync(DateOnly? referenceDate)
    {
        var positions = (await _positionRepository.GetAllAsync()).ToList();

        // Títulos carregados uma única vez e resolvidos em memória por AssetId: várias
        // posições costumam apontar para o mesmo título, e consultar o repositório dentro
        // do laço de projeção multiplicaria o número de idas ao banco por posição.
        var assets = (await _assetRepository.GetAllAsync()).ToDictionary(asset => asset.Id);

        // Existência dos títulos antes de carregar o mercado: o 404 não pode ser encoberto
        // por um erro de calendário do carregamento.
        foreach (var position in positions)
        {
            if (!assets.ContainsKey(position.AssetId))
            {
                return ApiResponse<PortfolioSummaryResponse>.NotFound(
                    $"Título vinculado à posição {position.Id} não encontrado.");
            }
        }

        ProjectionResult projected;

        // A data do resumo é resolvida dentro do projetor, depois do 404 e do filtro por data, e
        // dentro deste tratamento de DomainException, para uma falha do calendário ou da cotação
        // virar 400.
        try
        {
            projected = await _projector.ProjectAsync(positions, assets, referenceDate);
        }
        catch (DomainException ex)
        {
            return ApiResponse<PortfolioSummaryResponse>.BadRequest(ex.Message);
        }

        // Lista única: posição, título, projeção e data usada. Alimenta os totais (RN-07) e os itens de
        // Positions, para a soma dos itens fechar exatamente com os totais, sem recalcular.
        var summary = PortfolioSummary.Create(projected.Items.Select(p => (p.Projection, p.Asset.Id, p.Asset.AssetType)));

        // Mesma ordem do repositório (D-6) e a mesma projeção que alimentou os totais.
        var items = projected.Items
            .Select(p => new PortfolioSummaryResponse.PortfolioPositionResponse(
                PositionProjectionResponse.FromProjection(
                    p.Position.Id, p.Asset.Id, p.Projection, p.Date, p.Position.ApplicationDate),
                AssetResponse.FromEntity(p.Asset)))
            .ToList();

        var response = PortfolioSummaryResponse.FromSummary(summary, projected.ReferenceDate, items);

        return ApiResponse<PortfolioSummaryResponse>.Ok(response);
    }
}
