using FixedIncome.Domain.Common;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Domain.ValueObjects;

public static class IncomeTaxTable
{
    public static decimal RateFor(AssetType assetType, int elapsedDays)
    {
        if (elapsedDays < 0)
        {
            throw new DomainException("O prazo decorrido não pode ser negativo.");
        }

        // RN-04: LCI e LCA são isentos de IR para pessoa física, independentemente do prazo.
        if (assetType == AssetType.Lci || assetType == AssetType.Lca)
        {
            return 0m;
        }

        return elapsedDays switch
        {
            <= 180 => 22.5m,
            <= 360 => 20.0m,
            <= 720 => 17.5m,
            _ => 15.0m
        };
    }
}
