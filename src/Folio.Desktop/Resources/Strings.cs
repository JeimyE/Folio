using System.Globalization;
using System.Resources;

namespace Folio.Desktop.Resources;

/// <summary>
/// Thin accessor over <c>Strings.resx</c> (SPEC §3: UI strings live in .resx, not
/// hardcoded in XAML). Hand-written rather than relying on the resx designer
/// generator, so both XAML (<c>x:Static</c>) and C# (formatted messages) read from
/// the exact same resource file.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager = new("Folio.Desktop.Resources.Strings", typeof(Strings).Assembly);

    public static string AppTitle => Get("AppTitle");

    public static string AppSubtitle => Get("AppSubtitle");

    public static string LoginUsername => Get("LoginUsername");

    public static string LoginPassword => Get("LoginPassword");

    public static string LoginSignIn => Get("LoginSignIn");

    public static string LoginInvalidCredentials => Get("LoginInvalidCredentials");

    public static string LoginAccountLockedFormat => Get("LoginAccountLockedFormat");

    public static string LoginAccountInactive => Get("LoginAccountInactive");

    public static string ShellSignOut => Get("ShellSignOut");

    public static string ShellToggleTheme => Get("ShellToggleTheme");

    public static string ShellBrandSubtitle => Get("ShellBrandSubtitle");

    public static string RoleAdministrador => Get("RoleAdministrador");

    public static string RoleContador => Get("RoleContador");

    public static string RoleConsulta => Get("RoleConsulta");

    public static string ShellSectionGeneral => Get("ShellSectionGeneral");

    public static string ShellSectionContabilidad => Get("ShellSectionContabilidad");

    public static string WelcomeGreetingFormat => Get("WelcomeGreetingFormat");

    public static string NavWelcome => Get("NavWelcome");

    public static string NavChartOfAccounts => Get("NavChartOfAccounts");

    public static string ChartOfAccountsTitle => Get("ChartOfAccountsTitle");

    public static string ChartOfAccountsSearchPlaceholder => Get("ChartOfAccountsSearchPlaceholder");

    public static string ChartOfAccountsAddRoot => Get("ChartOfAccountsAddRoot");

    public static string ChartOfAccountsAddChild => Get("ChartOfAccountsAddChild");

    public static string ChartOfAccountsEdit => Get("ChartOfAccountsEdit");

    public static string ChartOfAccountsActivate => Get("ChartOfAccountsActivate");

    public static string ChartOfAccountsDeactivate => Get("ChartOfAccountsDeactivate");

    public static string ChartOfAccountsDelete => Get("ChartOfAccountsDelete");

    public static string ChartOfAccountsNoSelectionHint => Get("ChartOfAccountsNoSelectionHint");

    public static string ChartOfAccountsDetailCode => Get("ChartOfAccountsDetailCode");

    public static string ChartOfAccountsDetailType => Get("ChartOfAccountsDetailType");

    public static string ChartOfAccountsDetailNormalBalance => Get("ChartOfAccountsDetailNormalBalance");

    public static string ChartOfAccountsDetailBalance => Get("ChartOfAccountsDetailBalance");

    public static string ChartOfAccountsDetailStatus => Get("ChartOfAccountsDetailStatus");

    public static string ChartOfAccountsStatusActive => Get("ChartOfAccountsStatusActive");

    public static string ChartOfAccountsStatusInactive => Get("ChartOfAccountsStatusInactive");

    public static string ChartOfAccountsKindGroup => Get("ChartOfAccountsKindGroup");

    public static string ChartOfAccountsKindPostable => Get("ChartOfAccountsKindPostable");

    public static string ChartOfAccountsNewAccountTitle => Get("ChartOfAccountsNewAccountTitle");

    public static string ChartOfAccountsEditAccountTitle => Get("ChartOfAccountsEditAccountTitle");

    public static string ChartOfAccountsDeleteConfirmTitle => Get("ChartOfAccountsDeleteConfirmTitle");

    public static string ChartOfAccountsDeleteConfirmMessageFormat => Get("ChartOfAccountsDeleteConfirmMessageFormat");

    public static string ChartOfAccountsTabAllFormat => Get("ChartOfAccountsTabAllFormat");

    public static string ChartOfAccountsTabAsset => Get("ChartOfAccountsTabAsset");

    public static string ChartOfAccountsTabLiability => Get("ChartOfAccountsTabLiability");

    public static string ChartOfAccountsTabEquity => Get("ChartOfAccountsTabEquity");

    public static string ChartOfAccountsTabRevenue => Get("ChartOfAccountsTabRevenue");

    public static string ChartOfAccountsTabCostOrExpense => Get("ChartOfAccountsTabCostOrExpense");

    public static string ChartOfAccountsInactiveTag => Get("ChartOfAccountsInactiveTag");

    public static string ChartOfAccountsDetailHeaderFormat => Get("ChartOfAccountsDetailHeaderFormat");

    public static string ChartOfAccountsDetailSummaryFormat => Get("ChartOfAccountsDetailSummaryFormat");

    public static string ChartOfAccountsCuentaT => Get("ChartOfAccountsCuentaT");

    public static string ChartOfAccountsDebe => Get("ChartOfAccountsDebe");

    public static string ChartOfAccountsHaber => Get("ChartOfAccountsHaber");

    public static string ChartOfAccountsSaldo => Get("ChartOfAccountsSaldo");

    public static string ChartOfAccountsBalanceByMonth => Get("ChartOfAccountsBalanceByMonth");

    public static string ChartOfAccountsLastSixMonths => Get("ChartOfAccountsLastSixMonths");

    public static string ChartOfAccountsRecentEntries => Get("ChartOfAccountsRecentEntries");

    public static string ChartOfAccountsBalanceCheckLabel => Get("ChartOfAccountsBalanceCheckLabel");

    public static string AccountFormCode => Get("AccountFormCode");

    public static string AccountFormName => Get("AccountFormName");

    public static string AccountFormType => Get("AccountFormType");

    public static string AccountFormNormalBalance => Get("AccountFormNormalBalance");

    public static string AccountFormIsPostable => Get("AccountFormIsPostable");

    public static string AccountFormDescription => Get("AccountFormDescription");

    public static string AccountFormNameRequired => Get("AccountFormNameRequired");

    public static string DialogSave => Get("DialogSave");

    public static string DialogCancel => Get("DialogCancel");

    public static string DialogConfirm => Get("DialogConfirm");

    public static string AccountTypeAsset => Get("AccountTypeAsset");

    public static string AccountTypeLiability => Get("AccountTypeLiability");

    public static string AccountTypeEquity => Get("AccountTypeEquity");

    public static string AccountTypeRevenue => Get("AccountTypeRevenue");

    public static string AccountTypeCost => Get("AccountTypeCost");

    public static string AccountTypeExpense => Get("AccountTypeExpense");

    public static string NormalBalanceDebit => Get("NormalBalanceDebit");

    public static string NormalBalanceCredit => Get("NormalBalanceCredit");

    private static string Get(string key) => Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
