using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Folio.Application.ChartOfAccounts;
using Folio.Desktop.Dialogs;
using Folio.Desktop.Resources;

namespace Folio.Desktop.ViewModels;

public sealed partial class ChartOfAccountsViewModel(
    IChartOfAccountsService chartOfAccountsService,
    IDialogService dialogService) : ObservableObject
{
    private static readonly CompositeFormat DeleteConfirmMessageFormat =
        CompositeFormat.Parse(Strings.ChartOfAccountsDeleteConfirmMessageFormat);

    [ObservableProperty]
    private ObservableCollection<AccountTreeNodeViewModel> _rootNodes = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private AccountTreeNodeViewModel? _selectedNode;

    [ObservableProperty]
    private AccountDetailDto? _selectedDetail;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public async Task InitializeAsync() => await LoadTreeAsync(selectId: null);

    [RelayCommand]
    private async Task RefreshAsync() => await LoadTreeAsync(SelectedNode?.Id);

    [RelayCommand]
    private async Task AddRootAccountAsync() => await ShowAccountFormAsync(parentCode: null);

    [RelayCommand(CanExecute = nameof(CanAddChildAccount))]
    private async Task AddChildAccountAsync() => await ShowAccountFormAsync(SelectedNode!.Code);

    private bool CanAddChildAccount() => SelectedNode is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task EditAccountAsync()
    {
        if (SelectedDetail is null)
        {
            return;
        }

        var formViewModel = new AccountFormViewModel(chartOfAccountsService);
        formViewModel.InitializeForEdit(SelectedDetail);

        var saved = await dialogService.ShowAccountFormAsync(formViewModel, Strings.ChartOfAccountsEditAccountTitle);
        if (saved)
        {
            ErrorMessage = null;
            await LoadTreeAsync(formViewModel.SavedAccountId);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ToggleActiveAsync()
    {
        if (SelectedNode is null)
        {
            return;
        }

        var result = SelectedNode.IsActive
            ? await chartOfAccountsService.DeactivateAsync(SelectedNode.Id)
            : await chartOfAccountsService.ActivateAsync(SelectedNode.Id);

        if (!result.Succeeded)
        {
            ErrorMessage = result.ErrorMessage;
            return;
        }

        ErrorMessage = null;
        await LoadTreeAsync(SelectedNode.Id);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteAccountAsync()
    {
        if (SelectedNode is null)
        {
            return;
        }

        var message = string.Format(
            CultureInfo.CurrentCulture,
            DeleteConfirmMessageFormat,
            SelectedNode.Code,
            SelectedNode.Name);

        var confirmed = await dialogService.ShowConfirmationAsync(Strings.ChartOfAccountsDeleteConfirmTitle, message);
        if (!confirmed)
        {
            return;
        }

        var result = await chartOfAccountsService.DeleteAsync(SelectedNode.Id);
        if (!result.Succeeded)
        {
            ErrorMessage = result.ErrorMessage;
            return;
        }

        ErrorMessage = null;
        await LoadTreeAsync(selectId: null);
    }

    private bool HasSelection() => SelectedNode is not null;

    private async Task ShowAccountFormAsync(string? parentCode)
    {
        var formViewModel = new AccountFormViewModel(chartOfAccountsService);
        formViewModel.InitializeForCreate(parentCode);

        var saved = await dialogService.ShowAccountFormAsync(formViewModel, Strings.ChartOfAccountsNewAccountTitle);
        if (saved)
        {
            ErrorMessage = null;
            await LoadTreeAsync(formViewModel.SavedAccountId);
        }
    }

    private async Task LoadTreeAsync(Guid? selectId)
    {
        IsBusy = true;
        try
        {
            var tree = await chartOfAccountsService.GetTreeAsync();
            RootNodes = new ObservableCollection<AccountTreeNodeViewModel>(tree.Select(dto => new AccountTreeNodeViewModel(dto)));
            ApplyFilter();
            SelectedNode = selectId is { } id ? FindNode(id) : null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private AccountTreeNodeViewModel? FindNode(Guid id)
    {
        foreach (var root in RootNodes)
        {
            var found = root.FindById(id);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var trimmed = SearchText.Trim();
        foreach (var root in RootNodes)
        {
            root.ApplyFilter(trimmed);
        }
    }

    partial void OnSelectedNodeChanged(AccountTreeNodeViewModel? value)
    {
        AddChildAccountCommand.NotifyCanExecuteChanged();
        EditAccountCommand.NotifyCanExecuteChanged();
        ToggleActiveCommand.NotifyCanExecuteChanged();
        DeleteAccountCommand.NotifyCanExecuteChanged();
        _ = LoadDetailAsync(value);
    }

    private async Task LoadDetailAsync(AccountTreeNodeViewModel? node) =>
        SelectedDetail = node is null ? null : await chartOfAccountsService.GetDetailAsync(node.Id);
}
