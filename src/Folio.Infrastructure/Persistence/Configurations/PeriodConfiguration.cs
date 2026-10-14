using Folio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Folio.Infrastructure.Persistence.Configurations;

public sealed class PeriodConfiguration : IEntityTypeConfiguration<Period>
{
    public void Configure(EntityTypeBuilder<Period> builder)
    {
        builder.ToTable("Periods");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Year);
        builder.Property(p => p.Month);
        builder.Property(p => p.StartDate);
        builder.Property(p => p.EndDate);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(p => p.ClosedAtUtc);
        builder.Property(p => p.ClosedByUserId);

        builder.HasOne<FiscalYear>()
            .WithMany()
            .HasForeignKey(p => p.FiscalYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.FiscalYearId, p.Month }).IsUnique();
    }
}
