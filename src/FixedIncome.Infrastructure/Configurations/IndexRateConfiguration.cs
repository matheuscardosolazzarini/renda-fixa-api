using FixedIncome.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FixedIncome.Infrastructure.Configurations;

public class IndexRateConfiguration : IEntityTypeConfiguration<IndexRate>
{
    public void Configure(EntityTypeBuilder<IndexRate> builder)
    {
        builder.ToTable("index_rate");

        builder.HasKey(r => r.Id);

        // Id não tem setter público (definido em BaseEntity), então o EF precisa acessar o campo diretamente.
        builder.Property(r => r.Id)
            .HasColumnName("id")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(r => r.IndexType)
            .HasColumnName("index_type")
            .HasConversion<string>();

        builder.Property(r => r.ReferenceDate)
            .HasColumnName("reference_date");

        builder.Property(r => r.Value)
            .HasColumnName("value")
            .HasPrecision(12, 6);

        // Um valor por indexador e data: a importação atualiza a linha existente em vez de duplicar.
        builder.HasIndex(r => new { r.IndexType, r.ReferenceDate })
            .IsUnique();
    }
}
