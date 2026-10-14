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
}
