using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Application.Repositories;

public interface IIndexRateRepository
{
    // Ambas as datas são inclusivas.
    Task<IEnumerable<IndexRate>> GetByIndexTypeAndPeriodAsync(IndexType indexType, DateOnly from, DateOnly to);

    // Cotação mais recente com data estritamente anterior à informada; nula se não houver. Serve à
    // estimativa do IPCA quando o último mês publicado fica antes do período consultado.
    Task<IndexRate?> GetLastBeforeAsync(IndexType indexType, DateOnly date);

    // Nulo quando ainda não há nenhuma cotação importada desse indexador.
    Task<DateOnly?> GetLastReferenceDateAsync(IndexType indexType);

    // Insere as cotações novas e atualiza o valor das que já existem para o mesmo indexador e data.
    Task UpsertAsync(IEnumerable<IndexRate> rates);
}
