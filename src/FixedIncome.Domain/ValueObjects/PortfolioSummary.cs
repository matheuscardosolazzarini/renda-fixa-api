using FixedIncome.Domain.Enums;

namespace FixedIncome.Domain.ValueObjects;

public record PortfolioSummary(
    decimal TotalInvestedAmount,
    decimal TotalGrossAmount,
    decimal TotalGrossYield,
    decimal TotalTaxAmount,
    decimal TotalNetAmount,
    int PositionCount,
    IReadOnlyList<PortfolioSummary.AssetTypeSummary> ByAssetType)
{
    // RN-07: os totais somam os valores de PositionProjection já arredondados por
    // Position.Project, sem arredondar a soma bruta — o consolidado precisa fechar
    // exatamente com as parcelas de projeção individual exibidas ao usuário.
    public static PortfolioSummary Create(IEnumerable<(PositionProjection Projection, AssetType AssetType)> positions)
    {
        var items = positions.ToList();

        var totalNetAmount = items.Sum(item => item.Projection.NetAmount);

        var byAssetType = items
            .GroupBy(item => item.AssetType)
            .Select(group =>
            {
                var netAmount = group.Sum(item => item.Projection.NetAmount);
                var percentage = totalNetAmount == 0
                    ? 0m
                    : Math.Round(netAmount / totalNetAmount * 100, 2, MidpointRounding.AwayFromZero);

                return new AssetTypeSummary(
                    AssetType: group.Key,
                    PositionCount: group.Count(),
                    TotalInvestedAmount: group.Sum(item => item.Projection.InvestedAmount),
                    TotalNetAmount: netAmount,
                    Percentage: percentage);
            })
            .OrderByDescending(item => item.TotalNetAmount)
            .ToList();

        return new PortfolioSummary(
            TotalInvestedAmount: items.Sum(item => item.Projection.InvestedAmount),
            TotalGrossAmount: items.Sum(item => item.Projection.GrossAmount),
            TotalGrossYield: items.Sum(item => item.Projection.GrossYield),
            TotalTaxAmount: items.Sum(item => item.Projection.TaxAmount),
            TotalNetAmount: totalNetAmount,
            PositionCount: items.Count,
            ByAssetType: byAssetType);
    }

    public record AssetTypeSummary(
        AssetType AssetType,
        int PositionCount,
        decimal TotalInvestedAmount,
        decimal TotalNetAmount,
        decimal Percentage);
}
