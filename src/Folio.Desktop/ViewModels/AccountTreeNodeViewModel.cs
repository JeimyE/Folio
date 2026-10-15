using CommunityToolkit.Mvvm.ComponentModel;
using Folio.Application.ChartOfAccounts;
using Folio.Domain.Enums;

namespace Folio.Desktop.ViewModels;

/// <summary>
/// Wraps one <see cref="AccountTreeItemDto"/> for binding to the tree view, adding the
/// view-only state (expansion, search match, visibility) that the DTO itself has no
/// reason to carry.
/// </summary>
public sealed partial class AccountTreeNodeViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSearchMatch;

    [ObservableProperty]
    private bool _isVisible = true;

    public AccountTreeNodeViewModel(AccountTreeItemDto dto, bool isTopLevel = true)
    {
        Id = dto.Id;
        Code = dto.Code;
        Name = dto.Name;
        Type = dto.Type;
        IsTopLevel = isTopLevel;
        IsPostable = dto.IsPostable;
        IsActive = dto.IsActive;
        HasChildren = dto.HasChildren;
        Balance = dto.Balance;
        Children = dto.Children.Select(child => new AccountTreeNodeViewModel(child, isTopLevel: false)).ToList();
    }

    public Guid Id { get; }

    public string Code { get; }

    public string Name { get; }

    public AccountType Type { get; }

    /// <summary>Whether this is a root ("nivel 1") account — the only level
    /// rendered in Fraunces in the ledger tree (component #4).</summary>
    public bool IsTopLevel { get; }

    public bool IsPostable { get; }

    public bool IsActive { get; }

    public bool HasChildren { get; }

    public decimal Balance { get; }

    public IReadOnlyList<AccountTreeNodeViewModel> Children { get; }

    /// <summary>
    /// Filters this node's whole subtree by <paramref name="searchText"/> (matched
    /// against code or name) AND by <paramref name="typeFilter"/>, expanding and
    /// marking every ancestor of a match so the match stays reachable even when it
    /// sits several levels deep, and returns whether this node (or any descendant)
    /// should stay visible. An account's type is uniform down its whole branch (a
    /// child's type must equal its parent's — enforced in
    /// <c>ChartOfAccountsService.CreateAsync</c>), so a type mismatch at this node
    /// means every descendant mismatches too — no special-casing needed, the normal
    /// descendant-match recursion already hides the whole branch.
    /// </summary>
    public bool ApplyFilter(string searchText, AccountTypeFilter typeFilter)
    {
        var descendantMatch = false;
        foreach (var child in Children)
        {
            if (child.ApplyFilter(searchText, typeFilter))
            {
                descendantMatch = true;
            }
        }

        var textMatches = searchText.Length == 0
            || Code.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || Name.Contains(searchText, StringComparison.OrdinalIgnoreCase);
        var selfMatch = TypeMatches(typeFilter) && textMatches;

        IsSearchMatch = selfMatch && searchText.Length > 0;
        IsVisible = selfMatch || descendantMatch;

        if (descendantMatch && searchText.Length > 0)
        {
            IsExpanded = true;
        }

        return IsVisible;
    }

    private bool TypeMatches(AccountTypeFilter typeFilter) => typeFilter switch
    {
        AccountTypeFilter.All => true,
        AccountTypeFilter.Asset => Type == AccountType.Asset,
        AccountTypeFilter.Liability => Type == AccountType.Liability,
        AccountTypeFilter.Equity => Type == AccountType.Equity,
        AccountTypeFilter.Revenue => Type == AccountType.Revenue,
        AccountTypeFilter.CostOrExpense => Type is AccountType.Cost or AccountType.Expense,
        _ => false,
    };

    public AccountTreeNodeViewModel? FindById(Guid id)
    {
        if (Id == id)
        {
            return this;
        }

        foreach (var child in Children)
        {
            var found = child.FindById(id);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
