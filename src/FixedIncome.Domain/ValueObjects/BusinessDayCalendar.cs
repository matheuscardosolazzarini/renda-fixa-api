using FixedIncome.Domain.Common;
using FixedIncome.Domain.Entities;

namespace FixedIncome.Domain.ValueObjects;

// RN-08: dia útil é segunda a sexta que não seja feriado nacional ANBIMA. O calendário só
// responde dentro do período coberto pelos feriados informados; fora dele lança exceção,
// porque supor que não há feriado produziria uma contagem errada sem aviso.
public sealed class BusinessDayCalendar
{
    private readonly HashSet<DateOnly> _holidays;

    public DateOnly CoverageStart { get; }
    public DateOnly CoverageEnd { get; }

    public BusinessDayCalendar(IEnumerable<Holiday> holidays)
    {
        ArgumentNullException.ThrowIfNull(holidays);

        _holidays = holidays.Select(h => h.Date).ToHashSet();

        if (_holidays.Count == 0)
        {
            throw new DomainException("O calendário precisa de ao menos um feriado para definir o período coberto.");
        }

        CoverageStart = new DateOnly(_holidays.Min().Year, 1, 1);
        CoverageEnd = new DateOnly(_holidays.Max().Year, 12, 31);
    }

    public bool IsBusinessDay(DateOnly date)
    {
        EnsureCovered(date);

        return date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
            && !_holidays.Contains(date);
    }

    // Inclui start e exclui end (RN-08).
    public int CountBusinessDays(DateOnly start, DateOnly end)
    {
        return BusinessDaysBetween(start, end).Count;
    }

    // Primeiro dia útil estritamente posterior a date.
    public DateOnly NextBusinessDay(DateOnly date)
    {
        EnsureCovered(date);

        var candidate = date.AddDays(1);
        while (!IsBusinessDay(candidate))
        {
            candidate = candidate.AddDays(1);
        }

        return candidate;
    }

    // Dias úteis de [start, end), em ordem crescente, para o produtório do CDI.
    public IReadOnlyList<DateOnly> BusinessDaysBetween(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            throw new DomainException("A data final não pode ser anterior à data inicial.");
        }

        EnsureCovered(start);

        var days = new List<DateOnly>();
        if (start == end)
        {
            return days;
        }

        // O fim é exclusivo: a última data efetivamente consultada é end - 1 dia.
        EnsureCovered(end.AddDays(-1));

        for (var day = start; day < end; day = day.AddDays(1))
        {
            if (IsBusinessDay(day))
            {
                days.Add(day);
            }
        }

        return days;
    }

    private void EnsureCovered(DateOnly date)
    {
        if (date < CoverageStart)
        {
            throw new DomainException(
                $"Data {date:yyyy-MM-dd} anterior à cobertura do calendário, {CoverageStart:yyyy-MM-dd}.");
        }

        if (date > CoverageEnd)
        {
            throw new DomainException(
                $"Data {date:yyyy-MM-dd} posterior à cobertura do calendário, {CoverageEnd:yyyy-MM-dd}.");
        }
    }
}
