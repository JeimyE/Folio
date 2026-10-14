using CommunityToolkit.Mvvm.ComponentModel;
using Folio.Application.ChartOfAccounts;
using Folio.Desktop.Resources;
using Folio.Domain.Enums;

namespace Folio.Desktop.ViewModels;

/// <summary>
/// Backs the create/edit account dialog. Every validation message it can show comes
/// straight from <see cref="IChartOfAccountsService"/> — the only rule enforced here
/// directly is "name is required", purely to avoid a round trip for the most common
/// slip.
/// </summary>
public sealed partial class AccountFormViewModel(IChartOfAccountsService chartOfAccountsService) : ObservableObject
{
    private Guid? _editingAccountId;

    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private AccountType _type = AccountType.Asset;

    [ObservableProperty]
    private NormalBalance _normalBalance = NormalBalance.Debit;

    [ObservableProperty]
    private bool _isPostable = true;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isSaving;

    public static IReadOnlyList<AccountType> AccountTypes { get; } = Enum.GetValues<AccountType>();

    public static IReadOnlyList<NormalBalance> NormalBalances { get; } = Enum.GetValues<NormalBalance>();

    /// <summary>Code and parent are immutable after creation (SPEC §2.3); the form
    /// only lets it be typed while creating a brand-new account.</summary>
    public bool IsCodeEditable => _editingAccountId is null;

    public Guid? SavedAccountId { get; private set; }

    public void InitializeForCreate(string? parentCode)
    {
        _editingAccountId = null;
        Code = string.IsNullOrEmpty(parentCode) ? string.Empty : $"{parentCode}.";
    }

    public void InitializeForEdit(AccountDetailDto account)
    {
        _editingAccountId = account.Id;
        Code = account.Code;
        Name = account.Name;
        Type = account.Type;
        NormalBalance = account.NormalBalance;
        IsPostable = account.IsPostable;
        Description = account.Description;
    }

    public async Task<bool> SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = Strings.AccountFormNameRequired;
            return false;
        }

        IsSaving = true;
        try
        {
            var result = _editingAccountId is { } id
                ? await chartOfAccountsService.UpdateAsync(new UpdateAccountRequest(id, Name, Type, IsPostable, NormalBalance, Description))
                : await chartOfAccountsService.CreateAsync(new CreateAccountRequest(Code, Name, Type, IsPostable, NormalBalance, Description));

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage;
                return false;
            }

            ErrorMessage = null;
            SavedAccountId = result.AccountId ?? _editingAccountId;
            return true;
        }
        finally
        {
            IsSaving = false;
        }
    }

    /// <summary>Only while creating: picking a type refreshes the suggested normal
    /// balance, so a contra-account still has to be flipped on purpose. An edit never
    /// auto-changes it — <see cref="InitializeForEdit"/> sets the real current value
    /// right after this fires once for the default <see cref="Type"/>.</summary>
    partial void OnTypeChanged(AccountType value)
    {
        if (_editingAccountId is null)
        {
            NormalBalance = value switch
            {
                AccountType.Asset or AccountType.Cost or AccountType.Expense => NormalBalance.Debit,
                _ => NormalBalance.Credit,
            };
        }
    }
}
