using Folio.Application.Abstractions.Repositories;
using Folio.Domain.Entities;
using Folio.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Folio.Infrastructure.Persistence.Repositories;

public sealed class AccountRepository(IDbContextFactory<FolioDbContext> contextFactory) : IAccountRepository
{
    public async Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<Account?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var accountCode = new AccountCode(code);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Accounts.FirstOrDefaultAsync(a => a.Code == accountCode, cancellationToken);
    }

    public async Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Accounts.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, Account>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var accounts = await context.Accounts.Where(a => idList.Contains(a.Id)).ToListAsync(cancellationToken);
        return accounts.ToDictionary(a => a.Id);
    }

    public async Task AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Accounts.Add(account);
        await context.SaveChangesAsync(cancellationToken);
    }
}
