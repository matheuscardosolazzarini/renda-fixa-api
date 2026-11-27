using FixedIncome.Domain.Common;

namespace FixedIncome.Domain.ValueObjects;

// Resultado da variação acumulada (RN-13): o percentual com 6 casas e o fim efetivamente coberto,
// anterior ao pedido quando o período passa da última cotação publicada.
public readonly record struct IndexAccumulationResult(decimal AccumulatedPercentage, DateOnly EffectiveTo);

// Acumulação de índices. O produtório do CDI da RN-09 (CdiFactor) é compartilhado pela projeção,
// que resolve lacuna e projeção dia a dia com o percentual do título, e pela variação acumulada da
// RN-13 (Cdi e Ipca), que usa percentual 100. CdiFactor não sabe de lacuna nem de projeção: recebe
// as taxas diárias já resolvidas.
public static class IndexAccumulation
{
    private const decimal CdiScale = 10_000_000_000_000_000m; // 16 casas

    // RN-13, CDI: produto de 1 + CDI/100 de cada dia útil de [from, effectiveTo), só com cotação
    // publicada, nunca estimativa nem projeção. effectiveTo é o menor entre to e o dia útil seguinte
    // à última cotação (o fim é exclusivo), calculado antes de percorrer o intervalo: to além da
    // cobertura do calendário não é erro, porque o período termina na última cotação.
    public static IndexAccumulationResult Cdi(MarketData market, DateOnly from, DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(market);
        EnsureOrdered(from, to);

        // Com o fim exclusivo, from == to é intervalo vazio mesmo num dia com cotação. A mensagem
        // própria evita dizer "não há cotação" para uma data que tem (risco 4 do QA da F12).
        if (from == to)
        {
            throw new DomainException(
                $"O período de {from:yyyy-MM-dd} a {to:yyyy-MM-dd} está vazio: no CDI a data final não "
                + "entra no acumulado, então to precisa ser posterior a from.");
        }

        var last = market.LastCdi ?? throw NoPublishedRate("CDI", from, to);

        var cutoff = market.Calendar.NextBusinessDay(last.Date);
        var effectiveTo = to < cutoff ? to : cutoff;

        // from a partir do fim efetivo não deixa dia útil no intervalo (e o calendário recusaria um
        // fim anterior ao início), então é período sem cotação como fim de semana.
        var businessDays = from < effectiveTo
            ? market.Calendar.BusinessDaysBetween(from, effectiveTo)
            : Array.Empty<DateOnly>();

        if (businessDays.Count == 0)
        {
            throw NoPublishedRate("CDI", from, to);
        }

        var rates = new List<decimal>(businessDays.Count);
        foreach (var day in businessDays)
        {
            // Todo dia útil antes do fim efetivo é anterior ou igual à última cotação: sem valor, é lacuna.
            rates.Add(market.GetCdi(day)
                ?? throw new DomainException($"Cotação de CDI ausente para {day:yyyy-MM-dd}."));
        }

        return new IndexAccumulationResult(ToPercentage(CdiFactor(rates, 100m)), effectiveTo);
    }

    // RN-13, IPCA: produto de 1 + IPCA/100 dos meses de competência do mês de from ao mês de to,
    // inclusive, sem pro rata e sem consultar o calendário. Todas as comparações são por mês. Com to
    // depois do último mês publicado, o período termina no último dia desse mês. O fator é
    // arredondado em 7 casas (D-10), a convenção que reproduz a Calculadora do Cidadão do Banco
    // Central; a regra foi inferida das saídas da calculadora, não de metodologia publicada.
    public static IndexAccumulationResult Ipca(MarketData market, DateOnly from, DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(market);
        EnsureOrdered(from, to);

        var last = market.LastPublishedIpca ?? throw NoPublishedRate("IPCA", from, to);

        var fromMonth = FirstOfMonth(from);
        var toMonth = FirstOfMonth(to);

        if (fromMonth > last.Month)
        {
            throw NoPublishedRate("IPCA", from, to);
        }

        var effectiveMonth = toMonth < last.Month ? toMonth : last.Month;
        var effectiveTo = toMonth > last.Month ? last.Month.AddMonths(1).AddDays(-1) : to;

        var product = 1m;
        for (var month = fromMonth; month <= effectiveMonth; month = month.AddMonths(1))
        {
            var variation = market.GetIpca(month)
                ?? throw new DomainException($"Cotação de IPCA ausente para {month:yyyy-MM}.");

            product *= 1 + variation / 100;
        }

        var factor = Math.Round(product, 7, MidpointRounding.AwayFromZero);

        return new IndexAccumulationResult(ToPercentage(factor), effectiveTo);
    }

    // RN-09, metodologia do caderno de fórmulas da B3: fator diário 1 + CDI/100 × percentual/100,
    // acumulado truncado em 16 casas a cada dia e fator final arredondado em 8 casas. As taxas vêm
    // em % ao dia (série 12 do SGS), na ordem dos dias úteis.
    public static decimal CdiFactor(IEnumerable<decimal> dailyRates, decimal percentOfCdi)
    {
        ArgumentNullException.ThrowIfNull(dailyRates);

        var accumulated = 1m;

        foreach (var rate in dailyRates)
        {
            var dailyFactor = 1 + rate / 100 * percentOfCdi / 100;
            accumulated = decimal.Truncate(accumulated * dailyFactor * CdiScale) / CdiScale;
        }

        return Math.Round(accumulated, 8, MidpointRounding.AwayFromZero);
    }

    // Seis casas explícitas, para o JSON sair 1.081673 e não 1.08167300.
    private static decimal ToPercentage(decimal factor)
    {
        return Math.Round((factor - 1) * 100, 6, MidpointRounding.AwayFromZero);
    }

    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    private static void EnsureOrdered(DateOnly from, DateOnly to)
    {
        if (from > to)
        {
            throw new DomainException("A data inicial não pode ser posterior à data final.");
        }
    }

    private static DomainException NoPublishedRate(string index, DateOnly from, DateOnly to)
    {
        return new DomainException(
            $"Não há cotação de {index} publicada no período de {from:yyyy-MM-dd} a {to:yyyy-MM-dd}.");
    }
}
