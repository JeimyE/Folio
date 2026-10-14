using Folio.Application.Abstractions.Repositories;
using Folio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Folio.Infrastructure.Persistence.Repositories;

public sealed class CompanySettingsRepository(IDbContextFactory<FolioDbContext> contextFactory) : ICompanySettingsRepository
{
    public async Task<CompanySettings?> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.CompanySettings.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(CompanySettings settings, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.CompanySettings.Add(settings);
        await context.SaveChangesAsync(cancellationToken);
    }
}
