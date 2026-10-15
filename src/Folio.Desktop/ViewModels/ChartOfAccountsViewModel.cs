using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Folio.Application.ChartOfAccounts;
using Folio.Application.Company;
using Folio.Desktop.Dialogs;
using Folio.Desktop.Resources;
using Folio.Domain.Enums;

namespace Folio.Desktop.ViewModels;

public sealed partial class ChartOfAccountsViewModel(
    IChartOfAccountsService chartOfAccountsService,
    IDialogService dialogService,
    ICompanyContextService companyContextService) : ObservableObject
{
    private const double MaxMonthlyBarHeight = 56;
    private const double MinMonthlyBarHeight = 4;

    private static readonly CompositeFormat DeleteConfirmMessageFormat =
        CompositeFormat.Parse(Strings.ChartOfAccountsDeleteConfirmMessageFormat);

    private static readonly CompositeFormat DetailHeaderFormat =
        CompositeFormat.Parse(Strings.ChartOfAccountsDetailHeaderFormat);

    private static readonly CompositeFormat DetailSummaryFormat =
        CompositeFormat.Parse(Strings.ChartOfAccountsDetailSummaryFormat);

    private static readonly CompositeFormat TabAllFormat =
        CompositeFormat.Parse(Strings.ChartOfAccountsTabAllFormat);

    [ObservableProperty]
    private string _companyName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<AccountTreeNodeViewModel> _rootNodes = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private AccountTypeFilter _selectedTypeFilter = AccountTypeFilter.All;

    [ObservableProperty]
    private int _totalAccountCount;

    [ObservableProperty]
    private string _tabAllLabel = string.Empty;

    [ObservableProperty]
    private BalanceCheckDto? _balanceCheck;

    [ObservableProperty]
    private AccountTreeNodeViewModel? _selectedNode;

    [ObservableProperty]
    private AccountDetailDto? _selectedDetail;

    [ObservableProperty]
    private IReadOnlyList<MonthlyBalanceBarViewModel> _monthlyBalanceBars = [];

    [ObservableProperty]
    private IReadOnlyList<RecentEntryRowViewModel> _recentEntryRows = [];

    [ObservableProperty]
    private string _selectedDetailHeader = string.Empty;

    [ObservableProperty]
    private string _selectedDetailSummary = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    public async Task InitializeAsync()
    {
        CompanyName = await companyContextService.GetCompanyNameAsync();
        await LoadTreeAsync(selectId: null);
    }

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
            TotalAccountCount = CountAccounts(RootNodes);
            TabAllLabel = string.Format(CultureInfo.CurrentCulture, TabAllFormat, TotalAccountCount);
            ApplyFilter();
            SelectedNode = selectId is { } id ? FindNode(id) : null;
            BalanceCheck = await chartOfAccountsService.GetBalanceCheckAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static int CountAccounts(IEnumerable<AccountTreeNodeViewModel> nodes) =>
        nodes.Sum(node => 1 + CountAccounts(node.Children));

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

    partial void OnSelectedTypeFilterChanged(AccountTypeFilter value) => ApplyFilter();

    private void ApplyFilter()
    {
        var trimmed = SearchText.Trim();
        foreach (var root in RootNodes)
        {
            root.ApplyFilter(trimmed, SelectedTypeFilter);
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

    partial void OnSelectedDetailChanged(AccountDetailDto? value)
    {
        MonthlyBalanceBars = BuildMonthlyBalanceBars(value);
        RecentEntryRows = value is null
            ? []
            : value.RecentEntries
                .Select(e => e.Debit > 0m
                    ? new RecentEntryRowViewModel(e.Number, e.Description, e.Debit, "D")
                    : new RecentEntryRowViewModel(e.Number, e.Description, e.Credit, "H"))
                .ToList();
        SelectedDetailHeader = value is null
            ? string.Empty
            : string.Format(CultureInfo.CurrentCulture, DetailHeaderFormat, value.Code);
        SelectedDetailSummary = BuildSelectedDetailSummary(value);
    }

    private static List<MonthlyBalanceBarViewModel> BuildMonthlyBalanceBars(AccountDetailDto? detail)
    {
        if (detail is null || detail.MonthlyBalances.Count == 0)
        {
            return [];
        }

        var balances = detail.MonthlyBalances;
        var max = balances.Max(m => m.Balance);
        var min = Math.Min(0m, balances.Min(m => m.Balance));
        var range = max - min;

        return balances.Select((month, index) =>
        {
            var height = range == 0m
                ? MaxMonthlyBarHeight
                : MinMonthlyBarHeight + (double)((month.Balance - min) / range) * (MaxMonthlyBarHeight - MinMonthlyBarHeight);
            var monthAbbreviation = new DateOnly(month.Year, month.Month, 1)
                .ToDateTime(TimeOnly.MinValue)
                .ToString("MMM", CultureInfo.CurrentCulture)
                .TrimEnd('.'); // es-CR abbreviates with a trailing dot ("may."); the ledger design shows "may"
            return new MonthlyBalanceBarViewModel(height, index == balances.Count - 1, monthAbbreviation);
        }).ToList();
    }

    private static string BuildSelectedDetailSummary(AccountDetailDto? detail)
    {
        if (detail is null)
        {
            return string.Empty;
        }

        var activeText = detail.IsActive ? Strings.ChartOfAccountsStatusActive : Strings.ChartOfAccountsStatusInactive;
        var kindText = detail.IsPostable ? Strings.ChartOfAccountsKindPostable : Strings.ChartOfAccountsKindGroup;
        var typeText = detail.Type switch
        {
            AccountType.Asset => Strings.AccountTypeAsset,
            AccountType.Liability => Strings.AccountTypeLiability,
            AccountType.Equity => Strings.AccountTypeEquity,
            AccountType.Revenue => Strings.AccountTypeRevenue,
            AccountType.Cost => Strings.AccountTypeCost,
            AccountType.Expense => Strings.AccountTypeExpense,
            _ => string.Empty,
        };
        var natureText = detail.NormalBalance == NormalBalance.Debit ? Strings.NormalBalanceDebit : Strings.NormalBalanceCredit;

        return string.Format(CultureInfo.CurrentCulture, DetailSummaryFormat, activeText, kindText, typeText, natureText);
    }
}
