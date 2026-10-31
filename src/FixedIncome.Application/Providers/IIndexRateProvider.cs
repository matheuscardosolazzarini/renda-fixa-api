using FixedIncome.Domain.Entities;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Application.Providers;

public interface IIndexRateProvider
{
    // Busca as cotações publicadas na fonte externa. Ambas as datas são inclusivas; período sem
    // cotação publicada devolve lista vazia. Só há cotação para CDI e IPCA.
    Task<IEnumerable<IndexRate>> FetchAsync(IndexType type, DateOnly from, DateOnly to);
}
