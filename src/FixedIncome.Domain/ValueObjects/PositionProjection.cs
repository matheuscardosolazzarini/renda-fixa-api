namespace FixedIncome.Domain.ValueObjects;

public record PositionProjection(
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
    bool IsEstimated,
    bool IsProjected,
    decimal? NextTaxRate,
    DateOnly? NextTaxRateDate,
    DateOnly? IofFreeDate)
{
    // RN-15: calculado, não posicional, para que o ganho nunca divirja do líquido e do investido.
    public decimal NetYield => NetAmount - InvestedAmount;
}
