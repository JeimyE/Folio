using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Folio.Application.Company;
using Folio.Desktop.Resources;

namespace Folio.Desktop.ViewModels;

/// <summary>
/// Placeholder landing page shown right after login. Real feature pages (chart of
/// accounts, journal entries, etc.) start arriving in M4.
/// </summary>
public sealed partial class WelcomeViewModel(ICompanyContextService companyContextService) : ObservableObject
{
    private static readonly CompositeFormat GreetingFormat = CompositeFormat.Parse(Strings.WelcomeGreetingFormat);

    [ObservableProperty]
    private string _greeting = string.Empty;

    [ObservableProperty]
    private string _companyName = string.Empty;

    public async Task InitializeAsync(string fullName)
    {
        Greeting = string.Format(CultureInfo.CurrentCulture, GreetingFormat, fullName);
        CompanyName = await companyContextService.GetCompanyNameAsync();
    }
}
