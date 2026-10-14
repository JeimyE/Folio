using Folio.Application.Abstractions.Repositories;
using Folio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Folio.Infrastructure.Persistence.Repositories;

public sealed class JournalEntryRepository(IDbContextFactory<FolioDbContext> contextFactory) : IJournalEntryRepository
{
    public async Task<JournalEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.JournalEntries.Include(e => e.Lines).FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<int> GetNextNumberAsync(Guid fiscalYearId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

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

    public async Task AddAsync(JournalEntry entry, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.JournalEntries.Add(entry);
        await context.SaveChangesAsync(cancellationToken);
    }
}
