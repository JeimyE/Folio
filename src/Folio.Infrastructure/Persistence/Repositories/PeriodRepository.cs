using Folio.Application.Abstractions.Repositories;
using Folio.Domain.Entities;
using Folio.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Folio.Infrastructure.Persistence.Repositories;

public sealed class PeriodRepository(IDbContextFactory<FolioDbContext> contextFactory) : IPeriodRepository
{
    public async Task<Period?> GetOpenPeriodForDateAsync(DateOnly entryDate, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Periods.FirstOrDefaultAsync(
            p => p.Status == PeriodStatus.Open && entryDate >= p.StartDate && entryDate <= p.EndDate,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Period>> GetAllForFiscalYearAsync(Guid fiscalYearId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Periods.Where(p => p.FiscalYearId == fiscalYearId).OrderBy(p => p.Month).ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<Period> periods, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Periods.AddRange(periods);
        await context.SaveChangesAsync(cancellationToken);
    }
}
