using Folio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Folio.Infrastructure.Tests.Support;

/// <summary>
/// Adapts <see cref="SqliteInMemoryContextFactory"/> (which hands out plain
/// <see cref="FolioDbContext"/> instances) to <see cref="IDbContextFactory{TContext}"/>,
/// so repositories — written against the real factory interface used in production —
/// can be tested against the same in-memory SQLite connection without any special-casing.
/// </summary>
public sealed class TestDbContextFactory(SqliteInMemoryContextFactory factory) : IDbContextFactory<FolioDbContext>
{
    public FolioDbContext CreateDbContext() => factory.CreateContext();

    public Task<FolioDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(factory.CreateContext());
}
