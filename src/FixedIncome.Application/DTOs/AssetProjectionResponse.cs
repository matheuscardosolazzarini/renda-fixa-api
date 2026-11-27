using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Application.DTOs;

// Projeção consolidada de um título (RN-14). Os totais têm os nomes do resumo da carteira e são a
// soma dos itens já arredondados (RN-07). Sem IsProjected na raiz: o front detecta pelos itens.
public record AssetProjectionResponse(
    Guid AssetId,
    decimal TotalInvestedAmount,
    decimal TotalGrossAmount,
    decimal TotalGrossYield,
    decimal TotalTaxAmount,
    decimal TotalIofAmount,
    decimal TotalNetAmount,
    int PositionCount,
    DateOnly ReferenceDate,
    IReadOnlyList<PositionProjectionResponse> Positions)
{
    public static AssetProjectionResponse FromSummary(
        Guid assetId,
        PortfolioSummary summary,
        DateOnly referenceDate,
        IReadOnlyList<PositionProjectionResponse> positions) => new(
        assetId,
        summary.TotalInvestedAmount,
        summary.TotalGrossAmount,
        summary.TotalGrossYield,
        summary.TotalTaxAmount,
        summary.TotalIofAmount,
        summary.TotalNetAmount,
        summary.PositionCount,
        referenceDate,
        positions);
}
