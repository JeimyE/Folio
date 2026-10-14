using Folio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Folio.Infrastructure.Persistence.Configurations;

public sealed class FiscalYearConfiguration : IEntityTypeConfiguration<FiscalYear>
{
    public void Configure(EntityTypeBuilder<FiscalYear> builder)
    {
        builder.ToTable("FiscalYears");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Year);
        builder.Property(f => f.StartDate);
        builder.Property(f => f.EndDate);
        builder.Property(f => f.IsClosed);
        builder.HasIndex(f => f.Year).IsUnique();
    }
}
