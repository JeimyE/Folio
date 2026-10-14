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

    Task AddAsync(Account account, CancellationToken cancellationToken = default);

    Task UpdateAsync(Account account, CancellationToken cancellationToken = default);

    /// <summary>Hard-deletes an account. Callers must ensure it has no movements and no children first.</summary>
    Task DeleteAsync(Account account, CancellationToken cancellationToken = default);
}
