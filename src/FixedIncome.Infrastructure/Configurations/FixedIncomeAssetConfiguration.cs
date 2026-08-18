using FixedIncome.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FixedIncome.Infrastructure.Configurations;

public class FixedIncomeAssetConfiguration : IEntityTypeConfiguration<FixedIncomeAsset>
{
    public void Configure(EntityTypeBuilder<FixedIncomeAsset> builder)
    {
        builder.ToTable("fixed_income_asset");

        builder.HasKey(a => a.Id);

        // Id não tem setter público (definido em BaseEntity), então o EF precisa acessar o campo diretamente.
        builder.Property(a => a.Id)
            .HasColumnName("id")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(a => a.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(a => a.Issuer)
            .HasColumnName("issuer")
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(a => a.AssetType)
            .HasColumnName("asset_type")
            .HasConversion<string>();

        builder.Property(a => a.IndexType)
            .HasColumnName("index_type")
            .HasConversion<string>();

        builder.Property(a => a.Rate)
            .HasColumnName("rate")
            .HasPrecision(18, 6);

        builder.Property(a => a.IssueDate)
            .HasColumnName("issue_date");

        builder.Property(a => a.MaturityDate)
            .HasColumnName("maturity_date");
    }
}
