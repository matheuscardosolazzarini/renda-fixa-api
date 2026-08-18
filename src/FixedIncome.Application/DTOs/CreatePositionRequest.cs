namespace FixedIncome.Application.DTOs;

public record CreatePositionRequest(
    Guid AssetId,
    decimal InvestedAmount,
    DateOnly ApplicationDate);
