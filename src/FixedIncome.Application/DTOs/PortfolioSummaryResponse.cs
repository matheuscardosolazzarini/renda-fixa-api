using FixedIncome.Domain.Enums;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Application.DTOs;

public record PortfolioSummaryResponse(
    decimal TotalInvestedAmount,
    decimal TotalGrossAmount,
    decimal TotalGrossYield,
    decimal TotalTaxAmount,
    decimal TotalIofAmount,
    decimal TotalNetAmount,
    int PositionCount,
    IReadOnlyList<PortfolioSummaryResponse.AssetTypeSummaryResponse> ByAssetType,
    DateOnly ReferenceDate,
    IReadOnlyList<PortfolioSummaryResponse.PortfolioPositionResponse> Positions,
    bool IsProjected,
    decimal TotalNetYield,
    IReadOnlyList<PortfolioSummaryResponse.AssetSummaryResponse> ByAsset)
{
    public static PortfolioSummaryResponse FromSummary(
        PortfolioSummary summary,
        DateOnly referenceDate,
        IReadOnlyList<PortfolioPositionResponse> positions) => new(
        summary.TotalInvestedAmount,
        summary.TotalGrossAmount,
        summary.TotalGrossYield,
        summary.TotalTaxAmount,
        summary.TotalIofAmount,
        summary.TotalNetAmount,
        summary.PositionCount,
        summary.ByAssetType.Select(AssetTypeSummaryResponse.FromSummary).ToList(),
        referenceDate,
        positions,
        summary.IsProjected,
        summary.TotalNetYield,
        summary.ByAsset.Select(AssetSummaryResponse.FromSummary).ToList());

    // Item por composição: a projeção e o título são os mesmos DTOs dos endpoints próprios.
    public record PortfolioPositionResponse(PositionProjectionResponse Projection, AssetResponse Asset);

    public record AssetTypeSummaryResponse(
        AssetType AssetType,
        int PositionCount,
        decimal TotalInvestedAmount,
        decimal TotalNetAmount,
        decimal Percentage)
    {
        public static AssetTypeSummaryResponse FromSummary(PortfolioSummary.AssetTypeSummary item) => new(
            item.AssetType,
            item.PositionCount,
            item.TotalInvestedAmount,
            item.TotalNetAmount,
            item.Percentage);
    }

    // RN-18: totais de um título da carteira, somados dos itens dele em Positions.
    public record AssetSummaryResponse(
        Guid AssetId,
        int PositionCount,
        decimal TotalInvestedAmount,
        decimal TotalGrossAmount,
        decimal TotalGrossYield,
        decimal TotalTaxAmount,
        decimal TotalIofAmount,
        decimal TotalNetAmount,
        decimal TotalNetYield)
    {
        public static AssetSummaryResponse FromSummary(PortfolioSummary.AssetSummary item) => new(
            item.AssetId,
            item.PositionCount,
            item.TotalInvestedAmount,
            item.TotalGrossAmount,
            item.TotalGrossYield,
            item.TotalTaxAmount,
            item.TotalIofAmount,
            item.TotalNetAmount,
            item.TotalNetYield);
    }
}
