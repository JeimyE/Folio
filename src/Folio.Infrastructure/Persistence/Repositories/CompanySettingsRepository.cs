using Folio.Application.Abstractions.Repositories;
using Folio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Folio.Infrastructure.Persistence.Repositories;

public sealed class CompanySettingsRepository(FolioDbContext context) : ICompanySettingsRepository
{
    public Task<CompanySettings?> GetAsync(CancellationToken cancellationToken = default) =>
        context.CompanySettings.FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(CompanySettings settings, CancellationToken cancellationToken = default)
    {
        context.CompanySettings.Add(settings);
        return Task.CompletedTask;
    }
}
