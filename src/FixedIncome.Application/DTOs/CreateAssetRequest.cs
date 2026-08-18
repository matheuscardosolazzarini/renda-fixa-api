namespace FixedIncome.Application.DTOs;

public record CreateAssetRequest(
    string Name,
    string Issuer,
    string AssetType,
    string IndexType,
    decimal Rate,
    DateOnly IssueDate,
    DateOnly MaturityDate);
