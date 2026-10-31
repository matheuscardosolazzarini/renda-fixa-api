using System.Globalization;
using FixedIncome.Domain.Entities;
using FixedIncome.Infrastructure.Seeds;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FixedIncome.Infrastructure.Configurations;

public class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.ToTable("holiday");

        builder.HasKey(h => h.Id);

        // Id não tem setter público (definido em BaseEntity), então o EF precisa acessar o campo diretamente.
        builder.Property(h => h.Id)
            .HasColumnName("id")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(h => h.Date)
            .HasColumnName("date");

        builder.HasIndex(h => h.Date)
            .IsUnique();

        builder.Property(h => h.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(Holiday.NameMaxLength);

        // Id derivado da data: o seed usa o mesmo Id em toda execução e migração, e a data é única,
        // então o Id também é. Com HasData o construtor da entidade não serve, pois geraria um Guid novo.
        builder.HasData(AnbimaHolidaySeed.Holidays.Select(h => new
        {
            Id = new Guid(h.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-0000-0000-0000-000000000000"),
            Date = h.Date,
            Name = h.Name,
        }));
    }
}
