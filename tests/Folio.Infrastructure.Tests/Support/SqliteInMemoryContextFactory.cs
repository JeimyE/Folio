using Folio.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Folio.Infrastructure.Tests.Support;

/// <summary>
/// Creates a <see cref="FolioDbContext"/> backed by an in-memory SQLite database,
/// migrated with the project's real migrations (not <c>EnsureCreated</c>), per
/// CLAUDE.md's testing rules. The connection must stay open for the context's
/// lifetime, or the in-memory database disappears.
/// </summary>
public sealed class SqliteInMemoryContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteInMemoryContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.Migrate();
    }

    public FolioDbContext CreateContext(SaveChangesInterceptor? interceptor = null)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FolioDbContext>()
            .UseSqlite(_connection)
            // A First/FirstOrDefault with no filter and no OrderBy returns an arbitrary
            // row; in production it's only a logged warning, here it fails the test.
            .ConfigureWarnings(warnings => warnings.Throw(CoreEventId.FirstWithoutOrderByAndFilterWarning));

        if (interceptor is not null)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }

        return new FolioDbContext(optionsBuilder.Options);
    }

    public void Dispose() => _connection.Dispose();
}
