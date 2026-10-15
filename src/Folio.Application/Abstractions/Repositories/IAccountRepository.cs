using Folio.Domain.Entities;

namespace Folio.Application.Abstractions.Repositories;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Account?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, Account>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Whether any posted journal line references this account.</summary>
    Task<bool> HasMovementsAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sums posted journal lines for the given accounts (e.g. every leaf descendant
    /// of a grouping account, or just the one account for a leaf) — in functional
    /// currency, via SQL <c>SUM</c> on the underlying integer-cents columns (ADR-0003).
    /// </summary>
    Task<(decimal TotalDebit, decimal TotalCredit)> GetMovementTotalsAsync(IEnumerable<Guid> accountIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posted movement totals for every account that has any, grouped by account in
    /// a single SQL <c>GROUP BY</c> — not one query per account — so the chart-of-
    /// accounts tree can show every row's own balance (ledger design, component #4)
    /// without an N+1 query per node.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, (decimal TotalDebit, decimal TotalCredit)>> GetAllMovementTotalsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Every calendar month with at least one posted movement for the given accounts,
    /// up to and including <paramref name="through"/> — the FULL history, not just a
    /// recent window, so the caller can compute a running closing balance that
    /// already reflects everything before whatever window it cares about. Ordered by
    /// year then month, ascending.
    /// </summary>
    Task<IReadOnlyList<MonthlyMovementTotals>> GetMonthlyMovementTotalsAsync(
        IEnumerable<Guid> accountIds, DateOnly through, CancellationToken cancellationToken = default);

    /// <summary>
    /// The most recent posted journal entries that touched any of the given
    /// accounts, newest first, each carrying only the combined debit/credit of the
    /// lines for those accounts (not the entry's other side).
    /// </summary>
    Task<IReadOnlyList<RecentAccountEntry>> GetRecentEntriesAsync(
        IEnumerable<Guid> accountIds, int take, CancellationToken cancellationToken = default);

    Task AddAsync(Account account, CancellationToken cancellationToken = default);

    Task UpdateAsync(Account account, CancellationToken cancellationToken = default);

    /// <summary>Hard-deletes an account. Callers must ensure it has no movements and no children first.</summary>
    Task DeleteAsync(Account account, CancellationToken cancellationToken = default);
}
