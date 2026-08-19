using FixedIncome.Domain.Enums;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Application.DTOs;

public record PortfolioSummaryResponse(
    decimal TotalInvestedAmount,
    decimal TotalGrossAmount,
    decimal TotalGrossYield,
    decimal TotalTaxAmount,
    decimal TotalNetAmount,
    int PositionCount,
    IReadOnlyList<PortfolioSummaryResponse.AssetTypeSummaryResponse> ByAssetType)
{
    public static PortfolioSummaryResponse FromSummary(PortfolioSummary summary) => new(
        summary.TotalInvestedAmount,
        summary.TotalGrossAmount,
        summary.TotalGrossYield,
        summary.TotalTaxAmount,
        summary.TotalNetAmount,
        summary.PositionCount,
        summary.ByAssetType.Select(AssetTypeSummaryResponse.FromSummary).ToList());

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
}
