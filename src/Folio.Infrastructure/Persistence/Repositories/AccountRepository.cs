using Folio.Application.Abstractions.Repositories;
using Folio.Domain.Entities;
using Folio.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Folio.Infrastructure.Persistence.Repositories;

public sealed class AccountRepository(FolioDbContext context) : IAccountRepository
{
    public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<Account?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var accountCode = new AccountCode(code);
        return context.Accounts.FirstOrDefaultAsync(a => a.Code == accountCode, cancellationToken);
    }

    public async Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await context.Accounts.ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, Account>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        var accounts = await context.Accounts.Where(a => idList.Contains(a.Id)).ToListAsync(cancellationToken);
        return accounts.ToDictionary(a => a.Id);
    }

    public Task AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        context.Accounts.Add(account);
        return Task.CompletedTask;
    }
}
