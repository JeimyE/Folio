using Folio.Domain.Entities;

namespace Folio.Application.Abstractions.Repositories;

public interface IJournalEntryRepository
{
    Task<JournalEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The next sequential number for an entry posted in this fiscal year.</summary>
    Task<int> GetNextNumberAsync(Guid fiscalYearId, CancellationToken cancellationToken = default);

    Task AddAsync(JournalEntry entry, CancellationToken cancellationToken = default);
}
