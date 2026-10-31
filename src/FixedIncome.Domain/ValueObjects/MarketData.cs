using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Domain.ValueObjects;

// Dados de mercado já carregados em memória para a projeção: calendário de dias úteis e as
// séries de CDI (data -> % ao dia) e IPCA (mês -> % no mês). O domínio só consulta; quem
// carrega as cotações é a camada de aplicação. Consulta sem cotação devolve nulo, e cabe a
// quem projeta decidir o que fazer (RN-09 recusa; RN-10 estima só depois do último IPCA publicado).
public sealed class MarketData
{
    private readonly Dictionary<DateOnly, decimal> _cdi = [];
    private readonly Dictionary<DateOnly, decimal> _ipca = [];

    public BusinessDayCalendar Calendar { get; }

    public MarketData(BusinessDayCalendar calendar, IEnumerable<IndexRate> rates)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        ArgumentNullException.ThrowIfNull(rates);

        Calendar = calendar;

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
}
