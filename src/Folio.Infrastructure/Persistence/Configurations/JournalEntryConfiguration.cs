using Folio.Domain.Entities;
using Folio.Infrastructure.Persistence.ValueConversion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Folio.Infrastructure.Persistence.Configurations;

public sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("JournalEntries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Number);
        builder.Property(e => e.Date);
        builder.Property(e => e.Description).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Reference).HasMaxLength(100);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.CreatedByUserId);
        builder.Property(e => e.CreatedAtUtc);
        builder.Property(e => e.PostedByUserId);
        builder.Property(e => e.PostedAtUtc);

        builder.HasIndex(e => e.Date);
        builder.HasIndex(e => e.Number);

        builder.HasOne<Period>()
            .WithMany()
            .HasForeignKey(e => e.PeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        // A journal entry's reversal references its original via this self-FK.
        builder.HasOne<JournalEntry>()
            .WithMany()
            .HasForeignKey(e => e.ReversesEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Lines are never queried or referenced independently of their entry, so they
        // are mapped as an owned collection rather than a standalone entity with its
        // own DbSet — the aggregate boundary EF sees matches the one the Domain
        // already enforces. They still carry their own Id (Guid, assigned by the
        // Domain) because EF's SQLite provider cannot auto-generate a shadow key for
        // a dependent whose key also includes its owner's key.
        builder.OwnsMany(e => e.Lines, lines =>
        {
            lines.ToTable("JournalLines");
            lines.HasKey(l => l.Id);
            lines.WithOwner().HasForeignKey("JournalEntryId");

            lines.Property(l => l.LineNumber);
            lines.Property(l => l.Debit).HasConversion<MoneyToCentsConverter>();
            lines.Property(l => l.Credit).HasConversion<MoneyToCentsConverter>();
            lines.Property(l => l.OriginalAmount).HasConversion<MoneyToCentsConverter>();
            lines.Property(l => l.Currency).HasConversion<string>().HasMaxLength(5);
            lines.Property(l => l.ExchangeRate);
            lines.Property(l => l.AccountId);
            lines.Property(l => l.Description).HasMaxLength(500);

            lines.HasIndex(l => l.AccountId);
        });

        builder.Navigation(e => e.Lines)
            .HasField("_lines")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
