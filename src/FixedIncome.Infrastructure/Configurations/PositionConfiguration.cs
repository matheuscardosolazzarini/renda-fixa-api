using FixedIncome.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FixedIncome.Infrastructure.Configurations;

public class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("position");

        builder.HasKey(p => p.Id);

        // Id não tem setter público (definido em BaseEntity), então o EF precisa acessar o campo diretamente.
        builder.Property(p => p.Id)
            .HasColumnName("id")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(p => p.AssetId)
            .HasColumnName("asset_id")
            .IsRequired();

        builder.Property(p => p.InvestedAmount)
            .HasColumnName("invested_amount")
            .HasPrecision(18, 2);

        builder.Property(p => p.ApplicationDate)
            .HasColumnName("application_date");

        // O domínio não expõe navegação (decisão da F2): a Position guarda apenas o AssetId.
        builder.HasOne<FixedIncomeAsset>()
            .WithMany()
            .HasForeignKey(p => p.AssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
