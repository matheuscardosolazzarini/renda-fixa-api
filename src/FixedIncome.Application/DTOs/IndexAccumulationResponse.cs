using FixedIncome.Domain.Enums;

namespace FixedIncome.Application.DTOs;

// EffectiveTo é o fim realmente usado: igual a To, ou anterior a ele quando To passa da última
// cotação publicada (RN-13).
public record IndexAccumulationResponse(
    IndexType IndexType,
    DateOnly From,
    DateOnly To,
    DateOnly EffectiveTo,
    decimal AccumulatedPercentage);
