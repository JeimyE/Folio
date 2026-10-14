using Folio.Domain.Enums;
using Folio.Domain.Exceptions;
using Folio.Domain.ValueObjects;

namespace Folio.Domain.Entities;

/// <summary>
/// A node in the chart of accounts. Only postable (leaf) accounts accept journal
/// lines; an account with children can never be postable (SPEC §2.3).
/// </summary>
public sealed class Account
{
    public Guid Id { get; }

    public AccountCode Code { get; }

    public string Name { get; private set; }

    public AccountType Type { get; private set; }

    public NormalBalance NormalBalance { get; private set; }

    public Guid? ParentId { get; }

    public bool IsPostable { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// Whether this account has at least one child, as reported via <see cref="AddChild"/>.
    /// The Domain does not traverse the whole chart of accounts itself (that needs a
    /// repository); it only protects the invariant once told about a new child.
    /// </summary>
    public bool HasChildren { get; private set; }

    public string? Description { get; private set; }

    // Used by EF Core to materialize an existing row (constructor binding prefers a
    // parameterless constructor when one exists). It skips the business constructor's
    // validation and Id generation; EF sets every backing field directly afterward.
    private Account()
    {
        Code = null!; // overwritten by EF immediately after construction
        Name = null!; // overwritten by EF immediately after construction
    }

    public Account(
        AccountCode code,
        string name,
        AccountType type,
        bool isPostable,
        Guid? parentId = null,
        NormalBalance? normalBalanceOverride = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Account name is required.", nameof(name));
        }

        Id = Guid.NewGuid();
        Code = code;
        Name = name;
        Type = type;
        ParentId = parentId;
        NormalBalance = normalBalanceOverride ?? DeriveNormalBalance(type);
        IsPostable = isPostable;
        IsActive = true;
        Description = description;
        HasChildren = false;
    }

    private static NormalBalance DeriveNormalBalance(AccountType type) => type switch
    {
        AccountType.Asset or AccountType.Cost or AccountType.Expense => NormalBalance.Debit,
        AccountType.Liability or AccountType.Equity or AccountType.Revenue => NormalBalance.Credit,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown account type."),
    };

    /// <summary>
    /// Registers that another account was created as a child of this one. Called by
    /// whoever establishes the parent-child relationship (Application/Infrastructure).
    /// </summary>
    public void AddChild()
    {
        if (IsPostable)
        {
            throw new AccountWithChildrenCannotBePostableException(Code.Value);
        }

        HasChildren = true;
    }

    public void MarkAsPostable()
    {
        if (HasChildren)
        {
            throw new AccountWithChildrenCannotBePostableException(Code.Value);
        }

        IsPostable = true;
    }

    public void MarkAsNonPostable() => IsPostable = false;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Account name is required.", nameof(name));
        }

        Name = name;
    }

    public void UpdateDescription(string? description) => Description = description;
}
