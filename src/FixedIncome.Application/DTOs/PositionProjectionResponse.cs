using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Application.DTOs;

public record PositionProjectionResponse(
    Guid PositionId,
    Guid AssetId,
    decimal InvestedAmount,
    decimal GrossAmount,
    decimal GrossYield,
    decimal TaxRate,
    decimal TaxAmount,
    decimal NetAmount,
    int ElapsedDays,
    int BusinessDays,
    decimal IofRate,
    decimal IofAmount,
    bool IsEstimated)
{
    public static PositionProjectionResponse FromProjection(
        Guid positionId, Guid assetId, PositionProjection projection) => new(
        positionId,
        assetId,
        projection.InvestedAmount,
        projection.GrossAmount,
        projection.GrossYield,
        projection.TaxRate,
        projection.TaxAmount,
        projection.NetAmount,
        projection.ElapsedDays,
        projection.BusinessDays,
        projection.IofRate,
        projection.IofAmount,
        projection.IsEstimated);
}
