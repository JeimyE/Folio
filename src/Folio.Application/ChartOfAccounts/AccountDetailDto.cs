using Folio.Domain.Enums;

namespace Folio.Application.ChartOfAccounts;

public sealed class AccountDetailDto
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public AccountType Type { get; init; }

    public NormalBalance NormalBalance { get; init; }

    public Guid? ParentId { get; init; }

    public bool IsPostable { get; init; }

    public bool IsActive { get; init; }

    public bool HasChildren { get; init; }

    public bool HasMovements { get; init; }

    public string? Description { get; init; }

    /// <summary>
    /// The account's own balance (leaf accounts) or the sum of every descendant
    /// leaf's balance (grouping accounts), in functional currency (CRC), already
    /// signed according to <see cref="NormalBalance"/> — a positive number always
    /// means "more of what this account normally accumulates."
    /// </summary>
    public decimal Balance { get; init; }

    /// <summary>Accumulated debit side, before netting — the "Debe" column of the
    /// Cuenta T. Same accounts as <see cref="Balance"/> (the account itself, or
    /// every descendant leaf for a grouping account).</summary>
    public decimal DebitTotal { get; init; }

    /// <summary>Accumulated credit side, before netting — the "Haber" column of the
    /// Cuenta T.</summary>
    public decimal CreditTotal { get; init; }

    /// <summary>The account's closing balance at the end of each of the last six
    /// calendar months, oldest first — always exactly six entries. See
    /// <see cref="MonthlyBalanceDto"/> for what "closing balance" means here.</summary>
    public IReadOnlyList<MonthlyBalanceDto> MonthlyBalances { get; init; } = [];

    /// <summary>The most recent posted journal entries that touched this account,
    /// newest first.</summary>
    public IReadOnlyList<RecentEntryDto> RecentEntries { get; init; } = [];
}
