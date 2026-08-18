using FixedIncome.Domain.Entities;

namespace FixedIncome.Application.DTOs;

public record PositionResponse(
    Guid Id,
    Guid AssetId,
    decimal InvestedAmount,
    DateOnly ApplicationDate)
{
    public static PositionResponse FromEntity(Position position) => new(
        position.Id,
        position.AssetId,
        position.InvestedAmount,
        position.ApplicationDate);
}
