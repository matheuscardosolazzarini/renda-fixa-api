using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Application.DTOs;

public record IndexRateResponse(
    IndexType IndexType,
    DateOnly ReferenceDate,
    decimal Value)
{
    public static IndexRateResponse FromEntity(IndexRate rate) => new(
        rate.IndexType,
        rate.ReferenceDate,
        rate.Value);
}
