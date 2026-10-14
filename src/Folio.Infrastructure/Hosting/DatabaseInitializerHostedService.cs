using Folio.Infrastructure.Common;
using Folio.Infrastructure.Persistence;
using Microsoft.Extensions.Hosting;

namespace Folio.Infrastructure.Hosting;

/// <summary>
/// Runs <see cref="FolioDbInitializer"/> as part of the Generic Host's startup
/// sequence, so migrating/seeding the database is no longer code the Desktop project
/// has to call manually (as it did temporarily in M2).
/// </summary>
public sealed class DatabaseInitializerHostedService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        FolioPaths.EnsureDataFolderExists();
        return FolioDbInitializer.InitializeAsync(FolioPaths.ConnectionString, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
