using FixedIncome.Domain.Common;
using FixedIncome.Domain.Enums;

namespace FixedIncome.Domain.Entities;

// Cotação de um indexador como publicada pelo Banco Central: % ao dia para o CDI (série 12)
// e % no mês para o IPCA (série 433). A checagem de que a data do CDI é dia útil fica no
// caso de uso de importação, porque a entidade não conhece o calendário.
public class IndexRate : BaseEntity
{
    public IndexType IndexType { get; private set; }
    public DateOnly ReferenceDate { get; private set; }
    public decimal Value { get; private set; }

    public IndexRate(IndexType indexType, DateOnly referenceDate, decimal value)
    {
        switch (indexType)
        {
            case IndexType.Cdi:
                // O CDI diário não é negativo.
                if (value < 0)
                {
                    throw new DomainException("O valor do CDI não pode ser negativo.");
                }

                break;

            case IndexType.Ipca:
                // O IPCA é mensal e identificado pelo primeiro dia do mês; pode ser negativo (deflação).
                if (referenceDate.Day != 1)
                {
                    throw new DomainException("A data de referência do IPCA deve ser o primeiro dia do mês.");
                }

                break;

            default:
                throw new DomainException("Só há cotação para CDI e IPCA.");
        }

        IndexType = indexType;
        ReferenceDate = referenceDate;
        Value = value;
    }

    // Construtor privado sem parâmetros, para materialização pelo EF Core.
    private IndexRate()
    {
    }
}
