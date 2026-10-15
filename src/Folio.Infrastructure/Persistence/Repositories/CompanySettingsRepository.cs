using Folio.Application.Abstractions.Repositories;
using Folio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Folio.Infrastructure.Persistence.Repositories;

public sealed class CompanySettingsRepository(IDbContextFactory<FolioDbContext> contextFactory) : ICompanySettingsRepository
{
    public async Task<CompanySettings?> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // Single, not First: there is exactly one company row (SPEC §2.2), so no
        // ordering can matter — and a second row would be corrupt data that should
        // fail loudly rather than be silently ignored.
        return await context.CompanySettings.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(CompanySettings settings, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.CompanySettings.Add(settings);
        await context.SaveChangesAsync(cancellationToken);
    }
}
