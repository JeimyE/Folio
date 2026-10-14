using System.Text.Json;
using Folio.Domain.Common;
using Folio.Infrastructure.Common;
using Folio.Infrastructure.Security;
using Folio.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

namespace Folio.Infrastructure.Persistence;

/// <summary>
/// Applies pending migrations and, only when the database is empty, runs the demo
/// seed. Builds its own <see cref="FolioDbContext"/> deliberately without the audit
/// interceptor — bootstrapping the database is not a user action, so seeding would
/// otherwise flood the audit log with one row per seeded entity. One summary row is
/// written directly instead, so the Bitácora (M9) still shows that seeding happened.
/// </summary>
public static class FolioDbInitializer
{
    public static async Task InitializeAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var options = new DbContextOptionsBuilder<FolioDbContext>()
            .UseSqlite(connectionString)
            .Options;

        using var context = new FolioDbContext(options);

        await context.Database.MigrateAsync(cancellationToken);

        if (await context.Accounts.AnyAsync(cancellationToken))
        {
            return;
        }

        var clock = new SystemClock();
        var passwordHasher = new Pbkdf2PasswordHasher();

        var summary = await FolioSeeder.SeedAsync(context, clock, passwordHasher, cancellationToken);

        context.AuditLogs.Add(new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            TimestampUtc = clock.UtcNow,
            UserId = Guid.Empty,
            Entity = "Database",
            EntityId = "Seed",
            Action = "Seed",
            NewValuesJson = JsonSerializer.Serialize(new { Summary = summary }),
        });

        await context.SaveChangesAsync(cancellationToken);
    }
}
