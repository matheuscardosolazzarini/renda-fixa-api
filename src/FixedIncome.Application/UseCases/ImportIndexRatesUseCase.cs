using FixedIncome.Application.Common;
using FixedIncome.Application.DTOs;
using FixedIncome.Application.Providers;
using FixedIncome.Application.Repositories;
using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;
using FixedIncome.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace FixedIncome.Application.UseCases;

public interface IImportIndexRatesUseCase
{
    Task<ApiResponse<ImportIndexRatesResponse>> ExecuteAsync();
}

public class ImportIndexRatesUseCase : IImportIndexRatesUseCase
{
    // Início da série na primeira execução, quando ainda não há nenhuma cotação importada.
    private static readonly DateOnly FirstImportDate = new(2020, 1, 1);

    private readonly IIndexRateProvider _provider;
    private readonly IIndexRateRepository _indexRateRepository;
    private readonly IHolidayRepository _holidayRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ImportIndexRatesUseCase> _logger;

    public ImportIndexRatesUseCase(
        IIndexRateProvider provider,
        IIndexRateRepository indexRateRepository,
        IHolidayRepository holidayRepository,
        TimeProvider timeProvider,
        ILogger<ImportIndexRatesUseCase> logger)
    {
        _provider = provider;
        _indexRateRepository = indexRateRepository;
        _holidayRepository = holidayRepository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ApiResponse<ImportIndexRatesResponse>> ExecuteAsync()
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        var failedTypes = new List<IndexType>();
        var cdiImported = 0;
        var cdiDiscarded = 0;
        var ipcaImported = 0;

        // Um tipo que falha não impede o outro: cada um é importado e registrado à parte.
        try
        {
            (cdiImported, cdiDiscarded) = await ImportCdiAsync(today);
        }
        catch (Exception ex)
        {
            // Falha do provedor, dado inválido (DomainException, FormatException, JsonException) ou
            // do banco: a importação roda em segundo plano e não pode derrubar o processo.
            _logger.LogError(ex, "Falha ao importar as cotações de {IndexType}.", IndexType.Cdi);
            failedTypes.Add(IndexType.Cdi);
        }

        try
        {
            ipcaImported = await ImportIpcaAsync(today);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao importar as cotações de {IndexType}.", IndexType.Ipca);
            failedTypes.Add(IndexType.Ipca);
        }

        if (failedTypes.Count > 0)
        {
            return ApiResponse<ImportIndexRatesResponse>.InternalError(
                $"Falha ao importar as cotações de {string.Join(" e ", failedTypes)}.");
        }

        return ApiResponse<ImportIndexRatesResponse>.Ok(
            new ImportIndexRatesResponse(cdiImported, cdiDiscarded, ipcaImported));
    }

    private async Task<(int Imported, int Discarded)> ImportCdiAsync(DateOnly today)
    {
        var last = await _indexRateRepository.GetLastReferenceDateAsync(IndexType.Cdi);
        var from = last?.AddDays(1) ?? FirstImportDate;

        if (from > today)
        {
            return (0, 0);
        }

        var fetched = (await _provider.FetchAsync(IndexType.Cdi, from, today)).ToList();
        if (fetched.Count == 0)
        {
            return (0, 0);
        }

        var calendar = new BusinessDayCalendar(await _holidayRepository.GetAllAsync());
        var accepted = new List<IndexRate>();

        foreach (var rate in fetched)
        {
            // A entidade não conhece o calendário, então a checagem de dia útil fica aqui: cotação
            // de CDI em dia não útil é dado suspeito e entraria no produtório se fosse gravada.
            if (!calendar.IsBusinessDay(rate.ReferenceDate))
            {
                _logger.LogWarning(
                    "Cotação de CDI de {ReferenceDate} descartada: a data não é dia útil.", rate.ReferenceDate);
                continue;
            }

            accepted.Add(rate);
        }

        await _indexRateRepository.UpsertAsync(accepted);

        return (accepted.Count, fetched.Count - accepted.Count);
    }

    private async Task<int> ImportIpcaAsync(DateOnly today)
    {
        var last = await _indexRateRepository.GetLastReferenceDateAsync(IndexType.Ipca);

        // O IPCA mensal é datado no dia 1 do mês de referência. A busca incremental parte do dia 1 do
        // mês seguinte ao último publicado; "o dia seguinte" cairia no dia 2 e perderia o mês.
        var from = last?.AddMonths(1) ?? FirstImportDate;

        if (from > today)
        {
            return 0;
        }

        var fetched = (await _provider.FetchAsync(IndexType.Ipca, from, today)).ToList();

        await _indexRateRepository.UpsertAsync(fetched);

        return fetched.Count;
    }
}
