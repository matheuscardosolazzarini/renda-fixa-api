using FixedIncome.Domain.Common;

namespace FixedIncome.Domain.ValueObjects;

public static class IofTable
{
    // Alíquotas percentuais do anexo do Decreto 6.306/2007, indexadas pelo dia (1 a 30).
    // A posição 0 do vetor corresponde ao dia 1.
    private static readonly decimal[] Rates =
    {
        96m, 93m, 90m, 86m, 83m, 80m, 76m, 73m, 70m, 66m,
        63m, 60m, 56m, 53m, 50m, 46m, 43m, 40m, 36m, 33m,
        30m, 26m, 23m, 20m, 16m, 13m, 10m, 6m, 3m, 0m
    };

    public static decimal RateFor(int elapsedDays)
    {
        if (elapsedDays < 0)
        {
            throw new DomainException("O prazo decorrido não pode ser negativo.");
        }

        // Dia 0 não tem rendimento e, a partir do dia 30, o IOF deixa de incidir (RN-11).
        if (elapsedDays == 0 || elapsedDays > Rates.Length)
        {
            return 0m;
        }

        return Rates[elapsedDays - 1];
    }
}
