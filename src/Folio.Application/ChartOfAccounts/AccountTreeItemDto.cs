using Folio.Domain.Enums;

namespace Folio.Application.ChartOfAccounts;

/// <summary>One node of the chart-of-accounts tree, already sorted by code (numeric
/// segment order — see <see cref="Folio.Domain.ValueObjects.AccountCode.CompareTo"/>).</summary>
public sealed class AccountTreeItemDto
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public AccountType Type { get; init; }

    public bool IsPostable { get; init; }

    public bool IsActive { get; init; }

    public bool HasChildren { get; init; }

    /// <summary>Own balance (leaf) or the sum of every descendant leaf's balance
    /// (group), signed per the account's normal balance — see
    /// <see cref="AccountDetailDto.Balance"/> for the exact same convention.</summary>
    public decimal Balance { get; init; }

    public IReadOnlyList<AccountTreeItemDto> Children { get; init; } = [];
}
