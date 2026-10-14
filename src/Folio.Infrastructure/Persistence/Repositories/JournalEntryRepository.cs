using Folio.Application.Abstractions.Repositories;
using Folio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Folio.Infrastructure.Persistence.Repositories;

public sealed class JournalEntryRepository(FolioDbContext context) : IJournalEntryRepository
{
    public Task<JournalEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.JournalEntries.Include(e => e.Lines).FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<int> GetNextNumberAsync(Guid fiscalYearId, CancellationToken cancellationToken = default)
    {
        var periodIds = await context.Periods
            .Where(p => p.FiscalYearId == fiscalYearId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        // Max over a nullable-int sequence returns null (not an exception) when the
        // sequence is empty, which it is for the first entry of a fiscal year.
        var maxNumber = await context.JournalEntries
            .Where(e => e.PeriodId != null && periodIds.Contains(e.PeriodId!.Value))
            .Select(e => e.Number)
            .MaxAsync(cancellationToken);

        return (maxNumber ?? 0) + 1;
    }

    public Task AddAsync(JournalEntry entry, CancellationToken cancellationToken = default)
    {
        context.JournalEntries.Add(entry);
        return Task.CompletedTask;
    }
}
