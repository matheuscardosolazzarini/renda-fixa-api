using FixedIncome.Application.Common;
using FixedIncome.Application.DTOs;
using FixedIncome.Application.Repositories;
using FixedIncome.Application.Services;
using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Application.UseCases;

public interface IGetAssetProjectionUseCase
{
    Task<ApiResponse<AssetProjectionResponse>> ExecuteAsync(Guid id, DateOnly? referenceDate);
}

public class GetAssetProjectionUseCase : IGetAssetProjectionUseCase
{
    private readonly IFixedIncomeAssetRepository _assetRepository;
    private readonly IPositionRepository _positionRepository;
    private readonly PositionProjector _projector;

    public GetAssetProjectionUseCase(
        IFixedIncomeAssetRepository assetRepository,
        IPositionRepository positionRepository,
        PositionProjector projector)
    {
        _assetRepository = assetRepository;
        _positionRepository = positionRepository;
        _projector = projector;
    }

    public async Task<ApiResponse<AssetProjectionResponse>> ExecuteAsync(Guid id, DateOnly? referenceDate)
    {
        // O 404 vem antes de qualquer carregamento de calendário ou de mercado: um erro de calendário
        // não pode encobrir a inexistência do título.
        var asset = await _assetRepository.GetByIdAsync(id);

        if (asset is null)
        {
            return ApiResponse<AssetProjectionResponse>.NotFound($"Título com id {id} não encontrado.");
        }

        var positions = await _positionRepository.GetByAssetIdAsync(id);
        var assets = new Dictionary<Guid, FixedIncomeAsset> { [asset.Id] = asset };

        ProjectionResult projected;

        try
        {
            projected = await _projector.ProjectAsync(positions, assets, referenceDate);
        }
        catch (DomainException ex)
        {
            return ApiResponse<AssetProjectionResponse>.BadRequest(ex.Message);
        }

        // Mesmos totais do resumo (RN-07), somados dos itens já arredondados; o ByAssetType do
        // PortfolioSummary não entra na resposta do título.
        var summary = PortfolioSummary.Create(projected.Items.Select(p => (p.Projection, p.Asset.AssetType)));

        // Ordem do repositório (D-12) e a mesma projeção que alimentou os totais.
        var items = projected.Items
            .Select(p => PositionProjectionResponse.FromProjection(
                p.Position.Id, p.Asset.Id, p.Projection, p.Date, p.Position.ApplicationDate))
            .ToList();

        return ApiResponse<AssetProjectionResponse>.Ok(
            AssetProjectionResponse.FromSummary(asset.Id, summary, projected.ReferenceDate, items));
    }
}
