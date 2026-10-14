using Folio.Application.Abstractions;
using Folio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Folio.Infrastructure.Persistence;

public sealed class FolioDbContext(DbContextOptions<FolioDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<FiscalYear> FiscalYears => Set<FiscalYear>();

    public DbSet<Period> Periods => Set<Period>();

    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();

    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    public DbSet<User> Users => Set<User>();

    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();

    public DbSet<EntryTemplate> EntryTemplates => Set<EntryTemplate>();

    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FolioDbContext).Assembly);
    }
}
