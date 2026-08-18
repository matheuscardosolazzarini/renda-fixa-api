using FixedIncome.Domain.Common;
using FixedIncome.Domain.Enums;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Domain.Entities;

public class FixedIncomeAsset : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Issuer { get; private set; } = string.Empty;
    public AssetType AssetType { get; private set; }
    public IndexType IndexType { get; private set; }
    public decimal Rate { get; private set; }
    public DateOnly IssueDate { get; private set; }
    public DateOnly MaturityDate { get; private set; }

    public FixedIncomeAsset(
        string name,
        string issuer,
        AssetType assetType,
        IndexType indexType,
        decimal rate,
        DateOnly issueDate,
        DateOnly maturityDate)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
        {
            throw new DomainException("Name deve ser não vazio e ter até 120 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(issuer) || issuer.Length > 120)
        {
            throw new DomainException("Issuer deve ser não vazio e ter até 120 caracteres.");
        }

        if (rate <= 0)
        {
            throw new DomainException("Rate deve ser maior que zero.");
        }

        if (maturityDate <= issueDate)
        {
            throw new DomainException("MaturityDate deve ser posterior a IssueDate.");
        }

        Name = name;
        Issuer = issuer;
        AssetType = assetType;
        IndexType = indexType;
        Rate = rate;
        IssueDate = issueDate;
        MaturityDate = maturityDate;
    }

    // Construtor privado sem parâmetros, para materialização pelo EF Core.
    private FixedIncomeAsset()
    {
    }

    public decimal ResolveAnnualRate(IndexRates rates)
    {
        return IndexType switch
        {
            IndexType.PreFixed => Rate,
            IndexType.Cdi => rates.Cdi * (Rate / 100),
            IndexType.Ipca => rates.Ipca + Rate,
            _ => throw new DomainException($"IndexType não suportado: {IndexType}.")
        };
    }

    public bool IsTaxExempt()
    {
        return AssetType == AssetType.Lci || AssetType == AssetType.Lca;
    }
}
