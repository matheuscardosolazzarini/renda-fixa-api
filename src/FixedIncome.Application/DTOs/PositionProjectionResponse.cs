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
    int ElapsedDays)
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
        projection.ElapsedDays);
}
