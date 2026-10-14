using Folio.Domain.Entities;

namespace Folio.Application.Abstractions.Repositories;

public interface IPeriodRepository
{
    Task<Period?> GetOpenPeriodForDateAsync(DateOnly entryDate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Period>> GetAllForFiscalYearAsync(Guid fiscalYearId, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<Period> periods, CancellationToken cancellationToken = default);
}
