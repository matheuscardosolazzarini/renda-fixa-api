using System.Diagnostics.CodeAnalysis;
using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Domain.ValueObjects;

// Dados de mercado já carregados em memória para a projeção: calendário de dias úteis e as
// séries de CDI (data -> % ao dia) e IPCA (mês -> % no mês). O domínio só consulta; quem
// carrega as cotações é a camada de aplicação. Consulta sem cotação devolve nulo, e cabe a
// quem projeta decidir o que fazer (RN-09 recusa; RN-10 estima só depois do último IPCA publicado).
//
// Contrato com quem carrega (D-1): a série precisa conter a última cotação de CDI do banco e os
// 12 meses de IPCA que terminam no último publicado, além do período consultado. Sem isso, o maior
// dia e o maior mês da série não seriam os do banco, e uma lacuna anterior à última cotação
// pareceria projeção.
public sealed class MarketData
{
    // Meses de competência seguidos da média geométrica do IPCA (D-4).
    public const int IpcaWindowMonths = 12;

    private readonly Dictionary<DateOnly, decimal> _cdi = [];
    private readonly Dictionary<DateOnly, decimal> _ipca = [];

    public BusinessDayCalendar Calendar { get; }

    // Dia de hoje em Brasília, para separar mês e dia de CDI ainda não publicados (projeção) dos que
    // já deviam estar (estimativa ou falha de importação). O domínio recebe só a data, sem relógio.
    public DateOnly Today { get; }

    public MarketData(BusinessDayCalendar calendar, IEnumerable<IndexRate> rates, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(rates);

        Calendar = calendar;
        Today = today;

        foreach (var rate in rates)
        {
            var series = rate.IndexType == IndexType.Cdi ? _cdi : _ipca;

            if (!series.TryAdd(rate.ReferenceDate, rate.Value))
            {
                throw new DomainException(
                    $"Cotação duplicada de {rate.IndexType} para {rate.ReferenceDate:yyyy-MM-dd}.");
            }
        }
    }

    public decimal? GetCdi(DateOnly date)
    {
        return _cdi.TryGetValue(date, out var value) ? value : null;
    }

    // Aceita qualquer dia do mês: a série é indexada pelo primeiro dia.
    public decimal? GetIpca(DateOnly dateInMonth)
    {
        return _ipca.TryGetValue(new DateOnly(dateInMonth.Year, dateInMonth.Month, 1), out var value)
            ? value
            : null;
    }

    // Cotação do dia mais recente da série; base da projeção de CDI com data futura (RN-12).
    public (DateOnly Date, decimal Value)? LastCdi
    {
        get
        {
            if (_cdi.Count == 0)
            {
                return null;
            }

            var date = _cdi.Keys.Max();
            return (date, _cdi[date]);
        }
    }

    // Variação do mês mais recente da série; base da estimativa de mês sem IPCA (RN-10).
    public (DateOnly Month, decimal Value)? LastPublishedIpca
    {
        get
        {
            if (_ipca.Count == 0)
            {
                return null;
            }

            var month = _ipca.Keys.Max();
            return (month, _ipca[month]);
        }
    }

    // Variação mensal projetada do IPCA (RN-12, D-4 e D-5): média geométrica dos 12 meses de
    // competência seguidos que terminam no último publicado, em percentual com 2 casas. O produtório
    // não tem arredondamento intermediário; o único arredondamento é no percentual final. Sem os 12
    // meses, ou sem nenhum IPCA, devolve falso e o motivo.
    public bool TryGetProjectedIpca(out decimal variation, [NotNullWhen(false)] out string? failure)
    {
        variation = 0m;

        var last = LastPublishedIpca;
        if (last is null)
        {
            failure = "Não há cotação de IPCA publicada para estimar a projeção.";
            return false;
        }

        var end = last.Value.Month;
        var start = end.AddMonths(1 - IpcaWindowMonths);

        var product = 1m;
        var present = 0;
        DateOnly? firstMissing = null;

        for (var month = start; month <= end; month = month.AddMonths(1))
        {
            if (_ipca.TryGetValue(month, out var value))
            {
                present++;
                product *= 1 + value / 100;
            }
            else
            {
                firstMissing ??= month;
            }
        }

        if (firstMissing is not null)
        {
            failure =
                $"A projeção futura do IPCA exige os {IpcaWindowMonths} meses de {start:yyyy-MM} a {end:yyyy-MM}; "
                + $"há {present}, falta {firstMissing.Value:yyyy-MM}.";
            return false;
        }

        variation = Math.Round(
            (DecimalMath.Pow(product, 1m / IpcaWindowMonths) - 1) * 100, 2, MidpointRounding.AwayFromZero);
        failure = null;
        return true;
    }
}
