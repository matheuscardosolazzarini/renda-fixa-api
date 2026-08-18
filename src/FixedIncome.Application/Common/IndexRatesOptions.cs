using FixedIncome.Domain.ValueObjects;

namespace FixedIncome.Application.Common;

// Propriedades com setter público porque o binding de configuração (feito na F4b) exige.
public class IndexRatesOptions
{
    public decimal Cdi { get; set; }
    public decimal Ipca { get; set; }

    public IndexRates ToIndexRates() => new(Cdi, Ipca);
}
