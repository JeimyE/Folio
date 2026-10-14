using Folio.Application.Abstractions.Repositories;
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
public sealed class ChartOfAccountsService(IAccountRepository accountRepository) : IChartOfAccountsService
{
    public async Task<IReadOnlyList<AccountTreeItemDto>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await accountRepository.GetAllAsync(cancellationToken);
        var byParent = accounts.ToLookup(a => a.ParentId);

        AccountTreeItemDto BuildNode(Account account) => new()
        {
            Id = account.Id,
            Code = account.Code.Value,
            Name = account.Name,
            IsPostable = account.IsPostable,
            IsActive = account.IsActive,
            HasChildren = account.HasChildren,
            Children = byParent[account.Id]
                .OrderBy(a => a.Code)
                .Select(BuildNode)
                .ToList(),
        };

        return byParent[null]
            .OrderBy(a => a.Code)
            .Select(BuildNode)
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
        var balance = await ComputeBalanceAsync(account, cancellationToken);

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
        };
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

    private async Task<decimal> ComputeBalanceAsync(Account account, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> accountIds = account.IsPostable
            ? [account.Id]
            : await GetDescendantLeafIdsAsync(account.Id, cancellationToken);

        if (accountIds.Count == 0)
        {
            return 0m;
        }

        var (totalDebit, totalCredit) = await accountRepository.GetMovementTotalsAsync(accountIds, cancellationToken);
        return account.NormalBalance == NormalBalance.Debit ? totalDebit - totalCredit : totalCredit - totalDebit;
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
