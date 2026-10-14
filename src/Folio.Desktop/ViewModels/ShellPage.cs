namespace Folio.Desktop.ViewModels;

/// <summary>Identifies which sidebar destination is currently shown, purely so
/// <c>ShellView</c> can highlight the matching nav button — not a navigation
/// mechanism of its own (see <c>INavigationService</c> for that).</summary>
public enum ShellPage
{
    Welcome,
    ChartOfAccounts,
}
