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
    private readonly TimeProvider _timeProvider;

    public MarketDataLoader(
        IHolidayRepository holidayRepository, IIndexRateRepository indexRateRepository, TimeProvider timeProvider)
    {
        _holidayRepository = holidayRepository;
        _indexRateRepository = indexRateRepository;
        _timeProvider = timeProvider;
    }

    // Ambas as datas são inclusivas. Lança DomainException se não houver feriado cadastrado.
    // Além do período, a série sempre traz a última cotação de CDI do banco e os 12 meses de IPCA
    // que terminam no último publicado (D-1): decidir "lacuna" ou "projeção" pela série carregada
    // esconderia lacunas anteriores à última cotação.
    public async Task<MarketData> LoadAsync(DateOnly from, DateOnly to)
    {
        var calendar = new BusinessDayCalendar(await _holidayRepository.GetAllAsync());

        var cdi = (await _indexRateRepository.GetByIndexTypeAndPeriodAsync(IndexType.Cdi, from, to)).ToList();

        // O IPCA do mês M é datado no dia 1, então a consulta parte do dia 1 do mês de from:
        // do contrário o IPCA do mês em que o período começa ficaria de fora.
        var firstOfMonth = new DateOnly(from.Year, from.Month, 1);
        var ipca = (await _indexRateRepository.GetByIndexTypeAndPeriodAsync(IndexType.Ipca, firstOfMonth, to))
            .ToList();

        var lastCdi = await _indexRateRepository.GetLastReferenceDateAsync(IndexType.Cdi);
        if (lastCdi is not null)
        {
            cdi.AddRange(await _indexRateRepository.GetByIndexTypeAndPeriodAsync(
                IndexType.Cdi, lastCdi.Value, lastCdi.Value));
        }

        var lastIpca = await _indexRateRepository.GetLastReferenceDateAsync(IndexType.Ipca);
        if (lastIpca is not null)
        {
            var lastMonth = new DateOnly(lastIpca.Value.Year, lastIpca.Value.Month, 1);
            ipca.AddRange(await _indexRateRepository.GetByIndexTypeAndPeriodAsync(
                IndexType.Ipca, lastMonth.AddMonths(1 - MarketData.IpcaWindowMonths), lastMonth));
        }

        // As faixas se sobrepõem no caso comum e o MarketData recusa cotação duplicada.
        var rates = cdi.Concat(ipca)
            .GroupBy(rate => (rate.IndexType, rate.ReferenceDate))
            .Select(group => group.First());

        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

        return new MarketData(calendar, rates, today);
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
