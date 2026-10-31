using FixedIncome.Application.Repositories;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Application.Services;

// Monta o MarketData de um período a partir dos repositórios, para o domínio calcular sem
// consultar nada. Classe concreta, sem interface: não cruza camada.
public class MarketDataLoader
{
    private readonly IHolidayRepository _holidayRepository;
    private readonly IIndexRateRepository _indexRateRepository;

    public MarketDataLoader(IHolidayRepository holidayRepository, IIndexRateRepository indexRateRepository)
    {
        _holidayRepository = holidayRepository;
        _indexRateRepository = indexRateRepository;
    }

    // Ambas as datas são inclusivas. Lança DomainException se não houver feriado cadastrado.
    public async Task<MarketData> LoadAsync(DateOnly from, DateOnly to)
    {
        var calendar = new BusinessDayCalendar(await _holidayRepository.GetAllAsync());

        var cdi = await _indexRateRepository.GetByIndexTypeAndPeriodAsync(IndexType.Cdi, from, to);

        // O IPCA do mês M é datado no dia 1, então a consulta parte do dia 1 do mês de from:
        // do contrário o IPCA do mês em que o período começa ficaria de fora.
        var firstOfMonth = new DateOnly(from.Year, from.Month, 1);
        var ipca = (await _indexRateRepository.GetByIndexTypeAndPeriodAsync(IndexType.Ipca, firstOfMonth, to))
            .ToList();

        // Achado I3: o último IPCA publicado pode ser anterior ao período (o IPCA do mês corrente só
        // sai no mês seguinte). Ele entra na série para a projeção ter com o que estimar o mês sem
        // divulgação, em vez de falhar.
        var lastBefore = await _indexRateRepository.GetLastBeforeAsync(IndexType.Ipca, firstOfMonth);
        if (lastBefore is not null)
        {
            ipca.Add(lastBefore);
        }

        return new MarketData(calendar, cdi.Concat(ipca));
    }

    // Data de referência padrão do resumo: o dia útil seguinte à última cotação de CDI importada.
    // Nula quando não há nenhuma cotação de CDI.
    public async Task<DateOnly?> GetDefaultReferenceDateAsync()
    {
        var lastCdi = await _indexRateRepository.GetLastReferenceDateAsync(IndexType.Cdi);
        if (lastCdi is null)
        {
            return null;
        }

        var calendar = new BusinessDayCalendar(await _holidayRepository.GetAllAsync());

        return calendar.NextBusinessDay(lastCdi.Value);
    }
}
