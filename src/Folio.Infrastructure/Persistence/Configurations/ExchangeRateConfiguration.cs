using Folio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Folio.Infrastructure.Persistence.Configurations;

public sealed class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.ToTable("ExchangeRates");
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => r.Date).IsUnique();

        builder.Property(r => r.Date);
        builder.Property(r => r.Buy);
        builder.Property(r => r.Sell);
        builder.Property(r => r.Source).HasMaxLength(100);

        // Rates (not money amounts) keep EF's default decimal storage — they are
        // never SQL-aggregated, so the exact-precision concern that drives
        // MoneyToCentsConverter for Debit/Credit does not apply here (ADR-0003).
    }
}
