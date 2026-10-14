using CommunityToolkit.Mvvm.ComponentModel;
using Folio.Application.ChartOfAccounts;

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

    public AccountTreeNodeViewModel(AccountTreeItemDto dto)
    {
        Id = dto.Id;
        Code = dto.Code;
        Name = dto.Name;
        IsPostable = dto.IsPostable;
        IsActive = dto.IsActive;
        HasChildren = dto.HasChildren;
        Children = dto.Children.Select(child => new AccountTreeNodeViewModel(child)).ToList();
    }

    public Guid Id { get; }

    public string Code { get; }

    public string Name { get; }

    public bool IsPostable { get; }

    public bool IsActive { get; }

    public bool HasChildren { get; }

    public IReadOnlyList<AccountTreeNodeViewModel> Children { get; }

    /// <summary>
    /// Filters this node's whole subtree by <paramref name="searchText"/> (matched
    /// against code or name), expanding and marking every ancestor of a match so the
    /// match stays reachable even when it sits several levels deep, and returns
    /// whether this node (or any descendant) should stay visible.
    /// </summary>
    public bool ApplyFilter(string searchText)
    {
        var descendantMatch = false;
        foreach (var child in Children)
        {
            if (child.ApplyFilter(searchText))
            {
                descendantMatch = true;
            }
        }

        var selfMatch = searchText.Length == 0
            || Code.Contains(searchText, StringComparison.OrdinalIgnoreCase)
            || Name.Contains(searchText, StringComparison.OrdinalIgnoreCase);

        IsSearchMatch = selfMatch && searchText.Length > 0;
        IsVisible = selfMatch || descendantMatch;

        if (descendantMatch && searchText.Length > 0)
        {
            IsExpanded = true;
        }

        return IsVisible;
    }

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
