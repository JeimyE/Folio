using Folio.Application.Abstractions.Repositories;
using Folio.Domain.Common;
using Folio.Domain.Entities;
using Folio.Domain.Enums;
using Folio.Domain.Exceptions;
using Folio.Domain.ValueObjects;

namespace Folio.Application.ChartOfAccounts;

/// <summary>
/// The chart-of-accounts use cases (SPEC §2.3). Every rule the UI needs to react to —
/// duplicate codes, a missing parent, a type mismatch, movements blocking a change —
/// is enforced here, not re-implemented in the ViewModel; the ViewModel only ever
/// shows whatever message this service returns.
/// </summary>
public sealed class ChartOfAccountsService(IAccountRepository accountRepository, IClock clock) : IChartOfAccountsService
{
    private const int RecentEntriesTake = 5;
    private const int MonthlyBalanceWindowSize = 6;

    public async Task<IReadOnlyList<AccountTreeItemDto>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await accountRepository.GetAllAsync(cancellationToken);
        var byParent = accounts.ToLookup(a => a.ParentId);
        // One GROUP BY query for every account's movements — not one query per
        // tree node — so every row can show its own balance (component #4)
        // without an N+1 query per account.
        var movementTotalsByAccount = await accountRepository.GetAllMovementTotalsAsync(cancellationToken);

        // Bottom-up: children are built before their parent. Each node returns its
        // RAW debit/credit totals alongside its DTO, and every level signs those
        // totals with its OWN normal balance. Summing children's already-signed
        // balances instead would add a contra-account (e.g. Depreciación acumulada,
        // credit-normal under the debit-normal Activo group) instead of subtracting it.
        (AccountTreeItemDto Node, (decimal TotalDebit, decimal TotalCredit) Totals) BuildNode(Account account)
        {
            var children = byParent[account.Id]
                .OrderBy(a => a.Code)
                .Select(BuildNode)
                .ToList();

            var totals = account.IsPostable
                ? movementTotalsByAccount.GetValueOrDefault(account.Id)
                : (children.Sum(c => c.Totals.TotalDebit), children.Sum(c => c.Totals.TotalCredit));

            var node = new AccountTreeItemDto
            {
                Id = account.Id,
                Code = account.Code.Value,
                Name = account.Name,
                Type = account.Type,
                IsPostable = account.IsPostable,
                IsActive = account.IsActive,
                HasChildren = account.HasChildren,
                Balance = SignedBalance(account.NormalBalance, totals),
                Children = children.Select(c => c.Node).ToList(),
            };

            return (node, totals);
        }

        return byParent[null]
            .OrderBy(a => a.Code)
            .Select(account => BuildNode(account).Node)
            .ToList();
    }

    public async Task<AccountDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.GetByIdAsync(id, cancellationToken);
        if (account is null)
        {
            return null;
        }

        var hasMovements = await accountRepository.HasMovementsAsync(id, cancellationToken);

        IReadOnlyList<Guid> accountIds = account.IsPostable
            ? [account.Id]
            : await GetDescendantLeafIdsAsync(account.Id, cancellationToken);

        var (balance, debitTotal, creditTotal) = await ComputeBalanceAsync(account.NormalBalance, accountIds, cancellationToken);
        var monthlyBalances = await ComputeMonthlyBalancesAsync(account.NormalBalance, accountIds, cancellationToken);
        var recentEntries = await LoadRecentEntriesAsync(accountIds, cancellationToken);

        return new AccountDetailDto
        {
            Id = account.Id,
            Code = account.Code.Value,
            Name = account.Name,
            Type = account.Type,
            NormalBalance = account.NormalBalance,
            ParentId = account.ParentId,
            IsPostable = account.IsPostable,
            IsActive = account.IsActive,
            HasChildren = account.HasChildren,
            HasMovements = hasMovements,
            Description = account.Description,
            Balance = balance,
            DebitTotal = debitTotal,
            CreditTotal = creditTotal,
            MonthlyBalances = monthlyBalances,
            RecentEntries = recentEntries,
        };
    }

    public async Task<BalanceCheckDto> GetBalanceCheckAsync(CancellationToken cancellationToken = default)
    {
        var allAccounts = await accountRepository.GetAllAsync(cancellationToken);

        var assetLeafIds = allAccounts.Where(a => a.IsPostable && a.Type == AccountType.Asset).Select(a => a.Id).ToList();
        // Every other postable account goes on the right-hand side — not only
        // Liability/Equity. Until a fiscal year is closed, its Revenue/Cost/Expense
        // balances are the period's result, which is still part of Patrimonio:
        // A = L + E + (Revenue - Cost - Expense). Leaving them out would show the
        // check as failed on every correctly balanced book with income activity.
        var liabilityEquityLeafIds = allAccounts
            .Where(a => a.IsPostable && a.Type != AccountType.Asset)
            .Select(a => a.Id)
            .ToList();

        var assetTotal = 0m;
        if (assetLeafIds.Count > 0)
        {
            var (debit, credit) = await accountRepository.GetMovementTotalsAsync(assetLeafIds, cancellationToken);
            assetTotal = debit - credit; // Asset is always debit-normal on this side of the equation.
        }

        var liabilityEquityTotal = 0m;
        if (liabilityEquityLeafIds.Count > 0)
        {
            var (debit, credit) = await accountRepository.GetMovementTotalsAsync(liabilityEquityLeafIds, cancellationToken);
            liabilityEquityTotal = credit - debit; // Credit-positive on this side of the equation.
        }

        return new BalanceCheckDto(assetTotal, liabilityEquityTotal);
    }

    public async Task<ChartOfAccountsResult> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        AccountCode accountCode;
        try
        {
            accountCode = new AccountCode(request.Code);
        }
        catch (ArgumentException)
        {
            return ChartOfAccountsResult.Failure(
                $"\"{request.Code}\" no es un código válido. Use números separados por puntos, por ejemplo 1.1.07.");
        }

        if (await accountRepository.CodeExistsAsync(request.Code, cancellationToken))
        {
            return ChartOfAccountsResult.Failure($"Ya existe una cuenta con el código {request.Code}.");
        }

        Account? parent = null;
        var parentCode = accountCode.Parent();
        if (parentCode is not null)
        {
            parent = await accountRepository.GetByCodeAsync(parentCode.Value, cancellationToken);
            if (parent is null)
            {
                return ChartOfAccountsResult.Failure($"No existe una cuenta con el código padre {parentCode.Value}.");
            }

            if (parent.Type != request.Type)
            {
                return ChartOfAccountsResult.Failure(
                    $"El tipo ({request.Type}) no coincide con el de la cuenta padre {parent.Code.Value} ({parent.Type}).");
            }

            if (parent.IsPostable)
            {
                return ChartOfAccountsResult.Failure(
                    $"La cuenta padre {parent.Code.Value} es postable; desmarque \"postable\" en ella antes de agregarle subcuentas.");
            }
        }

        Account account;
        try
        {
            account = new Account(accountCode, request.Name, request.Type, request.IsPostable, parent?.Id, request.NormalBalanceOverride, request.Description);
        }
        catch (ArgumentException ex)
        {
            return ChartOfAccountsResult.Failure(ex.Message);
        }

        await accountRepository.AddAsync(account, cancellationToken);

        if (parent is not null)
        {
            parent.AddChild();
            await accountRepository.UpdateAsync(parent, cancellationToken);
        }

        return ChartOfAccountsResult.Success(account.Id);
    }

    public async Task<ChartOfAccountsResult> UpdateAsync(UpdateAccountRequest request, CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.GetByIdAsync(request.Id, cancellationToken);
        if (account is null)
        {
            return ChartOfAccountsResult.Failure("La cuenta no existe.");
        }

        var hasMovements = await accountRepository.HasMovementsAsync(account.Id, cancellationToken);
        var changingTypeOrBalance = request.Type != account.Type || request.NormalBalance != account.NormalBalance;
        var turningOffPostable = account.IsPostable && !request.IsPostable;

        if (changingTypeOrBalance && hasMovements)
        {
            return ChartOfAccountsResult.Failure("No se puede cambiar el tipo ni el saldo normal de una cuenta con movimientos.");
        }

        if (turningOffPostable && hasMovements)
        {
            return ChartOfAccountsResult.Failure("No se puede convertir en cuenta de agrupación una cuenta que ya tiene movimientos.");
        }

        try
        {
            account.Rename(request.Name);
        }
        catch (ArgumentException ex)
        {
            return ChartOfAccountsResult.Failure(ex.Message);
        }

        account.UpdateDescription(request.Description);

        if (changingTypeOrBalance)
        {
            account.ChangeType(request.Type, request.NormalBalance);
        }

        if (request.IsPostable && !account.IsPostable)
        {
            try
            {
                account.MarkAsPostable();
            }
            catch (AccountWithChildrenCannotBePostableException)
            {
                return ChartOfAccountsResult.Failure("Esta cuenta tiene subcuentas y no puede ser postable.");
            }
        }
        else if (!request.IsPostable && account.IsPostable)
        {
            account.MarkAsNonPostable();
        }

        await accountRepository.UpdateAsync(account, cancellationToken);
        return ChartOfAccountsResult.Success(account.Id);
    }

    public async Task<ChartOfAccountsResult> DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.GetByIdAsync(id, cancellationToken);
        if (account is null)
        {
            return ChartOfAccountsResult.Failure("La cuenta no existe.");
        }

        if (account.HasChildren)
        {
            var allAccounts = await accountRepository.GetAllAsync(cancellationToken);
            if (allAccounts.Any(a => a.ParentId == id && a.IsActive))
            {
                return ChartOfAccountsResult.Failure("Desactive primero las subcuentas.");
            }
        }

        account.Deactivate();
        await accountRepository.UpdateAsync(account, cancellationToken);
        return ChartOfAccountsResult.Success(account.Id);
    }

    public async Task<ChartOfAccountsResult> ActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.GetByIdAsync(id, cancellationToken);
        if (account is null)
        {
            return ChartOfAccountsResult.Failure("La cuenta no existe.");
        }

        if (account.ParentId is { } parentId)
        {
            var parent = await accountRepository.GetByIdAsync(parentId, cancellationToken);
            if (parent is not null && !parent.IsActive)
            {
                return ChartOfAccountsResult.Failure("No se puede activar una cuenta cuyo padre está inactivo.");
            }
        }

        account.Activate();
        await accountRepository.UpdateAsync(account, cancellationToken);
        return ChartOfAccountsResult.Success(account.Id);
    }

    public async Task<ChartOfAccountsResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.GetByIdAsync(id, cancellationToken);
        if (account is null)
        {
            return ChartOfAccountsResult.Failure("La cuenta no existe.");
        }

        if (account.HasChildren)
        {
            return ChartOfAccountsResult.Failure("No se puede eliminar una cuenta con subcuentas.");
        }

        if (await accountRepository.HasMovementsAsync(id, cancellationToken))
        {
            return ChartOfAccountsResult.Failure("No se puede eliminar una cuenta con movimientos; desactívela en su lugar.");
        }

        await accountRepository.DeleteAsync(account, cancellationToken);
        return ChartOfAccountsResult.Success();
    }

    private async Task<(decimal Balance, decimal DebitTotal, decimal CreditTotal)> ComputeBalanceAsync(
        NormalBalance normalBalance, IReadOnlyList<Guid> accountIds, CancellationToken cancellationToken)
    {
        if (accountIds.Count == 0)
        {
            return (0m, 0m, 0m);
        }

        var totals = await accountRepository.GetMovementTotalsAsync(accountIds, cancellationToken);
        return (SignedBalance(normalBalance, totals), totals.TotalDebit, totals.TotalCredit);
    }

    /// <summary>A positive result always means "more of what this account normally
    /// accumulates," regardless of type — see <see cref="AccountDetailDto.Balance"/>.</summary>
    private static decimal SignedBalance(NormalBalance normalBalance, (decimal TotalDebit, decimal TotalCredit) totals) =>
        normalBalance == NormalBalance.Debit ? totals.TotalDebit - totals.TotalCredit : totals.TotalCredit - totals.TotalDebit;

    /// <summary>
    /// The account's closing balance at the end of each of the last six calendar
    /// months (oldest first). "Closing" means the running total of every posted
    /// movement since the start of the account's history, not just that month's own
    /// movement — a month with no activity of its own inherits the previous month's
    /// balance instead of resetting to zero, which is also what makes the first
    /// month of the window already reflect everything posted before it.
    /// </summary>
    private async Task<IReadOnlyList<MonthlyBalanceDto>> ComputeMonthlyBalancesAsync(
        NormalBalance normalBalance, IReadOnlyList<Guid> accountIds, CancellationToken cancellationToken)
    {
        var today = clock.TodayInCostaRica();
        var currentMonthStart = new DateOnly(today.Year, today.Month, 1);
        var targetMonths = Enumerable.Range(0, MonthlyBalanceWindowSize)
            .Select(offset => currentMonthStart.AddMonths(offset - (MonthlyBalanceWindowSize - 1)))
            .ToList(); // oldest .. newest (current month), exactly 6 entries

        if (accountIds.Count == 0)
        {
            return targetMonths.Select(m => new MonthlyBalanceDto(m.Year, m.Month, 0m)).ToList();
        }

        var windowEnd = targetMonths[^1].AddMonths(1).AddDays(-1);
        var monthlyTotals = await accountRepository.GetMonthlyMovementTotalsAsync(accountIds, windowEnd, cancellationToken);

        // Every month that ever had activity, oldest first (the repository already
        // orders this way), converted into a running closing balance.
        var runningDebit = 0m;
        var runningCredit = 0m;
        var closingBalances = new List<((int Year, int Month) Month, decimal Balance)>();
        foreach (var monthTotal in monthlyTotals)
        {
            runningDebit += monthTotal.TotalDebit;
            runningCredit += monthTotal.TotalCredit;
            var closing = normalBalance == NormalBalance.Debit ? runningDebit - runningCredit : runningCredit - runningDebit;
            closingBalances.Add(((monthTotal.Year, monthTotal.Month), closing));
        }

        // Merge the two already-sorted sequences: for each of the six target
        // months, advance through every known month up to and including it,
        // carrying its balance forward — so a target month with no entry of its
        // own simply keeps whatever the last known month's balance was.
        var result = new List<MonthlyBalanceDto>(MonthlyBalanceWindowSize);
        var knownIndex = 0;
        var carryForward = 0m;
        foreach (var target in targetMonths)
        {
            while (knownIndex < closingBalances.Count && closingBalances[knownIndex].Month.CompareTo((target.Year, target.Month)) <= 0)
            {
                carryForward = closingBalances[knownIndex].Balance;
                knownIndex++;
            }

            result.Add(new MonthlyBalanceDto(target.Year, target.Month, carryForward));
        }

        return result;
    }

    private async Task<IReadOnlyList<RecentEntryDto>> LoadRecentEntriesAsync(IReadOnlyList<Guid> accountIds, CancellationToken cancellationToken)
    {
        if (accountIds.Count == 0)
        {
            return [];
        }

        var entries = await accountRepository.GetRecentEntriesAsync(accountIds, RecentEntriesTake, cancellationToken);
        return entries
            .Select(e => new RecentEntryDto(e.EntryId, e.Number, e.Description, e.Date, e.TotalDebit, e.TotalCredit))
            .ToList();
    }

    private async Task<IReadOnlyList<Guid>> GetDescendantLeafIdsAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var allAccounts = await accountRepository.GetAllAsync(cancellationToken);
        var byParent = allAccounts.ToLookup(a => a.ParentId);
        var leafIds = new List<Guid>();

        void Visit(Guid parentId)
        {
            foreach (var child in byParent[parentId])
            {
                if (child.IsPostable)
                {
                    leafIds.Add(child.Id);
                }
                else
                {
                    Visit(child.Id);
                }
            }
        }

        Visit(accountId);
        return leafIds;
    }
}
