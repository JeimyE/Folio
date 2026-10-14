using Folio.Domain.Entities;
using Folio.Infrastructure.Persistence.ValueConversion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Folio.Infrastructure.Persistence.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Code)
            .HasConversion<AccountCodeConverter>()
            .HasMaxLength(50)
            .IsRequired();
        builder.HasIndex(a => a.Code).IsUnique();

        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.NormalBalance).HasConversion<string>().HasMaxLength(10);
        builder.Property(a => a.IsPostable);
        builder.Property(a => a.IsActive);
        builder.Property(a => a.HasChildren);
        builder.Property(a => a.Description).HasMaxLength(1000);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(a => a.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
