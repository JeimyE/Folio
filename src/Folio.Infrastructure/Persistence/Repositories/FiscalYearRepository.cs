using Folio.Application.Abstractions.Repositories;
using Folio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Folio.Infrastructure.Persistence.Repositories;

public sealed class FiscalYearRepository(IDbContextFactory<FolioDbContext> contextFactory) : IFiscalYearRepository
{
    public async Task<FiscalYear?> GetByYearAsync(int year, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.FiscalYears.FirstOrDefaultAsync(f => f.Year == year, cancellationToken);
    }

    public async Task AddAsync(FiscalYear fiscalYear, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.FiscalYears.Add(fiscalYear);
        await context.SaveChangesAsync(cancellationToken);
    }
}
