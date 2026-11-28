using FixedIncome.Domain.Enums;

namespace FixedIncome.Domain.ValueObjects;

public record PortfolioSummary(
    decimal TotalInvestedAmount,
    decimal TotalGrossAmount,
    decimal TotalGrossYield,
    decimal TotalTaxAmount,
    decimal TotalIofAmount,
    decimal TotalNetAmount,
    int PositionCount,
    IReadOnlyList<PortfolioSummary.AssetTypeSummary> ByAssetType,
    bool IsProjected,
    IReadOnlyList<PortfolioSummary.AssetSummary> ByAsset)
{
    // RN-15: igual à soma dos NetYield dos itens, pois todos os valores já estão arredondados.
    public decimal TotalNetYield => TotalNetAmount - TotalInvestedAmount;

    // RN-07: os totais somam os valores de PositionProjection já arredondados por
    // Position.Project, sem arredondar a soma bruta — o consolidado precisa fechar
    // exatamente com as parcelas de projeção individual exibidas ao usuário. Como cada
    // projeção fecha em bruto - IOF - IR, o consolidado fecha na mesma identidade.
    public static PortfolioSummary Create(
        IEnumerable<(PositionProjection Projection, Guid AssetId, AssetType AssetType)> positions)
    {
        var items = positions.ToList();

        var totals = SumTotals(items.Select(item => item.Projection));

        var byAssetType = items
            .GroupBy(item => item.AssetType)
            .Select(group =>
            {
                var netAmount = group.Sum(item => item.Projection.NetAmount);
                var percentage = totals.NetAmount == 0
                    ? 0m
                    : Math.Round(netAmount / totals.NetAmount * 100, 2, MidpointRounding.AwayFromZero);

                return new AssetTypeSummary(
                    AssetType: group.Key,
                    PositionCount: group.Count(),
                    TotalInvestedAmount: group.Sum(item => item.Projection.InvestedAmount),
                    TotalNetAmount: netAmount,
                    Percentage: percentage);
            })
            .OrderByDescending(item => item.TotalNetAmount)
            .ToList();

        // RN-18: GroupBy preserva a ordem da primeira aparição do título em positions. A soma é a
        // mesma da raiz, para os totais do título e os da carteira não divergirem.
        var byAsset = items
            .GroupBy(item => item.AssetId)
            .Select(group =>
            {
                var assetTotals = SumTotals(group.Select(item => item.Projection));

                return new AssetSummary(
                    AssetId: group.Key,
                    PositionCount: assetTotals.PositionCount,
                    TotalInvestedAmount: assetTotals.InvestedAmount,
                    TotalGrossAmount: assetTotals.GrossAmount,
                    TotalGrossYield: assetTotals.GrossYield,
                    TotalTaxAmount: assetTotals.TaxAmount,
                    TotalIofAmount: assetTotals.IofAmount,
                    TotalNetAmount: assetTotals.NetAmount);
            })
            .ToList();

        return new PortfolioSummary(
            TotalInvestedAmount: totals.InvestedAmount,
            TotalGrossAmount: totals.GrossAmount,
            TotalGrossYield: totals.GrossYield,
            TotalTaxAmount: totals.TaxAmount,
            TotalIofAmount: totals.IofAmount,
            TotalNetAmount: totals.NetAmount,
            PositionCount: totals.PositionCount,
            ByAssetType: byAssetType,
            IsProjected: items.Any(item => item.Projection.IsProjected),
            ByAsset: byAsset);
    }

    private static (
        decimal InvestedAmount,
        decimal GrossAmount,
        decimal GrossYield,
        decimal TaxAmount,
        decimal IofAmount,
        decimal NetAmount,
        int PositionCount) SumTotals(IEnumerable<PositionProjection> projections)
    {
        var list = projections.ToList();

        return (
            InvestedAmount: list.Sum(projection => projection.InvestedAmount),
            GrossAmount: list.Sum(projection => projection.GrossAmount),
            GrossYield: list.Sum(projection => projection.GrossYield),
            TaxAmount: list.Sum(projection => projection.TaxAmount),
            IofAmount: list.Sum(projection => projection.IofAmount),
            NetAmount: list.Sum(projection => projection.NetAmount),
            PositionCount: list.Count);
    }

    public record AssetTypeSummary(
        AssetType AssetType,
        int PositionCount,
        decimal TotalInvestedAmount,
        decimal TotalNetAmount,
        decimal Percentage);

    public record AssetSummary(
        Guid AssetId,
        int PositionCount,
        decimal TotalInvestedAmount,
        decimal TotalGrossAmount,
        decimal TotalGrossYield,
        decimal TotalTaxAmount,
        decimal TotalIofAmount,
        decimal TotalNetAmount)
    {
        // RN-15, como no consolidado.
        public decimal TotalNetYield => TotalNetAmount - TotalInvestedAmount;
    }
}
