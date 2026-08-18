using FixedIncome.Domain.Entities;

namespace FixedIncome.Application.DTOs;

public record AssetResponse(
    Guid Id,
    string Name,
    string Issuer,
    string AssetType,
    string IndexType,
    decimal Rate,
    DateOnly IssueDate,
    DateOnly MaturityDate)
{
    public static AssetResponse FromEntity(FixedIncomeAsset asset) => new(
        asset.Id,
        asset.Name,
        asset.Issuer,
        asset.AssetType.ToString(),
        asset.IndexType.ToString(),
        asset.Rate,
        asset.IssueDate,
        asset.MaturityDate);
}
