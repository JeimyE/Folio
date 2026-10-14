using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Folio.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> create a <see cref="FolioDbContext"/> without
/// a running composition root (the Desktop app's Generic Host, added in M3). Only used
/// at design time, by the EF Core tooling.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FolioDbContext>
{
    public FolioDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FolioDbContext>()
            .UseSqlite("Data Source=design-time.db");

        return new FolioDbContext(optionsBuilder.Options);
    }
}
