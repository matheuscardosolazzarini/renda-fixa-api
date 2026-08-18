namespace FixedIncome.Domain.ValueObjects;

public record PositionProjection(
    decimal InvestedAmount,
    decimal GrossAmount,
    decimal GrossYield,
    decimal TaxRate,
    decimal TaxAmount,
    decimal NetAmount,
    int ElapsedDays);
