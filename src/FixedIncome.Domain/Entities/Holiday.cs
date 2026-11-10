using FixedIncome.Domain.Common;

namespace FixedIncome.Domain.Entities;

public class Holiday : BaseEntity
{
    public const int NameMaxLength = 120;

    public DateOnly Date { get; private set; }
    // Stryker disable once String : valor inicial sempre sobrescrito pelo construtor público e, na materialização do EF, pela coluna name; não é observável
    public string Name { get; private set; } = string.Empty;

    public Holiday(DateOnly date, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("O nome do feriado é obrigatório.");
        }

        if (name.Length > NameMaxLength)
        {
            throw new DomainException($"O nome do feriado deve ter no máximo {NameMaxLength} caracteres.");
        }

        Date = date;
        Name = name;
    }

    // Construtor privado sem parâmetros, para materialização pelo EF Core.
    private Holiday()
    {
    }
}
