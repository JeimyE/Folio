using Folio.Application.Abstractions.Repositories;
using Folio.Domain.Entities;
using Folio.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Folio.Infrastructure.Persistence.Repositories;

public sealed class PeriodRepository(FolioDbContext context) : IPeriodRepository
{
    public Task<Period?> GetOpenPeriodForDateAsync(DateOnly entryDate, CancellationToken cancellationToken = default) =>
        context.Periods.FirstOrDefaultAsync(
            p => p.Status == PeriodStatus.Open && entryDate >= p.StartDate && entryDate <= p.EndDate,
            cancellationToken);

    public async Task<IReadOnlyList<Period>> GetAllForFiscalYearAsync(Guid fiscalYearId, CancellationToken cancellationToken = default) =>
        await context.Periods.Where(p => p.FiscalYearId == fiscalYearId).OrderBy(p => p.Month).ToListAsync(cancellationToken);

    public Task AddRangeAsync(IEnumerable<Period> periods, CancellationToken cancellationToken = default)
    {
        context.Periods.AddRange(periods);
        return Task.CompletedTask;
    }
}
