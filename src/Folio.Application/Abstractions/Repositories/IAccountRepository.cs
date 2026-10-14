using Folio.Domain.Entities;

namespace Folio.Application.Abstractions.Repositories;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Account?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, Account>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    Task AddAsync(Account account, CancellationToken cancellationToken = default);
}
