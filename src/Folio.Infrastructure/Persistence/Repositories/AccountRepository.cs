using Folio.Application.Abstractions.Repositories;
using Folio.Domain.Entities;
using Folio.Domain.Enums;
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

    public async Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        var accountCode = new AccountCode(code);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Accounts.AnyAsync(a => a.Code == accountCode, cancellationToken);
    }

    public async Task<bool> HasMovementsAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.JournalEntries
            .Where(e => e.Status == JournalEntryStatus.Posted)
            .SelectMany(e => e.Lines)
            .AnyAsync(l => l.AccountId == accountId, cancellationToken);
    }

    public async Task<(decimal TotalDebit, decimal TotalCredit)> GetMovementTotalsAsync(IEnumerable<Guid> accountIds, CancellationToken cancellationToken = default)
    {
        var idList = accountIds.ToList();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var lines = context.JournalEntries
            .Where(e => e.Status == JournalEntryStatus.Posted)
            .SelectMany(e => e.Lines)
            .Where(l => idList.Contains(l.AccountId));

        var totalDebit = await lines.SumAsync(l => l.Debit, cancellationToken);
        var totalCredit = await lines.SumAsync(l => l.Credit, cancellationToken);
        return (totalDebit, totalCredit);
    }

    public async Task AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Accounts.Add(account);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Account account, CancellationToken cancellationToken = default)
    {
        // See UserRepository.UpdateAsync: a fresh context per call means the account
        // is always detached by the time it's mutated, so it's re-attached as
        // Modified here rather than tracked continuously.
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Accounts.Update(account);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Account account, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Accounts.Attach(account);
        context.Accounts.Remove(account);
        await context.SaveChangesAsync(cancellationToken);
    }
}
