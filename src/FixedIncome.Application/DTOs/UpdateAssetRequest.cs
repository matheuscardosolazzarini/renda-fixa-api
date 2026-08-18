namespace FixedIncome.Application.DTOs;

public record UpdateAssetRequest(
    string Name,
    string Issuer,
    decimal Rate,
    DateOnly IssueDate,
    DateOnly MaturityDate);
