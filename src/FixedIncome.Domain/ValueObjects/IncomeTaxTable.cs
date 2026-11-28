using FixedIncome.Domain.Common;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Domain.ValueObjects;

public static class IncomeTaxTable
{
    // Tabela regressiva (Lei 11.033/2004, art. 1º): cada faixa vale a partir do dia FromDay, em dias
    // corridos desde o aporte. A alíquota e a próxima faixa saem desta lista, para o limite
    // 180/181 não divergir entre as duas. Os literais mantêm a escala (20.0, 17.5, 15.0) que a
    // próxima faixa leva ao JSON.
    private static readonly (int FromDay, decimal Rate)[] Brackets =
    {
        (0, 22.5m),
        (181, 20.0m),
        (361, 17.5m),
        (721, 15.0m)
    };

    public static decimal RateFor(AssetType assetType, int elapsedDays)
    {
        EnsureNotNegative(elapsedDays);

        if (IsExempt(assetType))
        {
            return 0m;
        }

        return Brackets.Last(bracket => bracket.FromDay <= elapsedDays).Rate;
    }

    // Primeira faixa que começa depois de elapsedDays; nulo na última faixa e para LCI e LCA.
    // Não conhece vencimento nem datas: o corte no vencimento é de quem calcula a projeção.
    public static (int FromDay, decimal Rate)? NextBracketFor(AssetType assetType, int elapsedDays)
    {
        EnsureNotNegative(elapsedDays);

        if (IsExempt(assetType))
        {
            return null;
        }

        foreach (var bracket in Brackets)
        {
            if (bracket.FromDay > elapsedDays)
            {
                return bracket;
            }
        }

        return null;
    }

    private static void EnsureNotNegative(int elapsedDays)
    {
        if (elapsedDays < 0)
        {
            throw new DomainException("O prazo decorrido não pode ser negativo.");
        }
    }

    // RN-04: LCI e LCA são isentos de IR para pessoa física, independentemente do prazo.
    private static bool IsExempt(AssetType assetType) =>
        assetType == AssetType.Lci || assetType == AssetType.Lca;
}
