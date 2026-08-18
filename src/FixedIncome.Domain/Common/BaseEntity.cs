namespace FixedIncome.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; }

    protected BaseEntity()
    {
        Id = Guid.NewGuid();
    }
}
