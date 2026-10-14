using Folio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Folio.Infrastructure.Persistence.Configurations;

public sealed class EntryTemplateConfiguration : IEntityTypeConfiguration<EntryTemplate>
{
    public void Configure(EntityTypeBuilder<EntryTemplate> builder)
    {
        builder.ToTable("EntryTemplates");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.DefaultDescription).HasMaxLength(500);

        // A template line is still conceptually a detail of its template (it's never
        // queried or referenced independently), so it is mapped as an owned collection
        // even though it carries its own Id, which EF's SQLite provider needs since it
        // cannot auto-generate a shadow key for a dependent in a composite relationship.
        builder.OwnsMany(t => t.Lines, lines =>
        {
            lines.ToTable("EntryTemplateLines");
            lines.HasKey(l => l.Id);
            lines.WithOwner().HasForeignKey("EntryTemplateId");

            lines.Property(l => l.AccountId);
            lines.Property(l => l.IsDebit);
            lines.Property(l => l.DefaultAmount);
            lines.Property(l => l.Description).HasMaxLength(500);
        });

        builder.Navigation(t => t.Lines)
            .HasField("_lines")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
