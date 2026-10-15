using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Folio.Application.Abstractions;
using Folio.Desktop.Messages;
using Folio.Desktop.Navigation;
using Folio.Desktop.Theming;
using Folio.Domain.Enums;

namespace Folio.Desktop.ViewModels;

public sealed partial class ShellViewModel(
    ICurrentUserSession currentUserSession,
    IMessenger messenger,
    IThemeService themeService,
    INavigationService navigationService) : ObservableObject
{
    [ObservableProperty]
    private string _fullName = string.Empty;

    [ObservableProperty]
    private UserRole _role;

    [ObservableProperty]
    private ShellPage _currentPage = ShellPage.Welcome;

    public INavigationService Navigation => navigationService;

    public void Initialize(string fullName, UserRole role)
    {
        FullName = fullName;
        Role = role;
        // Fire-and-forget: Initialize is called from ShellCoordinatorViewModel.Receive,
        // a synchronous IRecipient callback that can't itself be awaited — same
        // reasoning as AccountTreeNodeViewModel's selection-changed hook elsewhere.
        _ = navigationService.NavigateTo<WelcomeViewModel>().InitializeAsync(fullName);
        CurrentPage = ShellPage.Welcome;
    }

    [RelayCommand]
    private async Task NavigateToWelcomeAsync()
    {
        await navigationService.NavigateTo<WelcomeViewModel>().InitializeAsync(FullName);
        CurrentPage = ShellPage.Welcome;
    }

    [RelayCommand]
    private async Task NavigateToChartOfAccountsAsync()
    {
        await navigationService.NavigateTo<ChartOfAccountsViewModel>().InitializeAsync();
        CurrentPage = ShellPage.ChartOfAccounts;
    }

    [RelayCommand]
    private void SignOut()
    {
        currentUserSession.SignOut();
        messenger.Send(new UserSignedOutMessage());
    }

    [RelayCommand]
    private void ToggleTheme() => themeService.ToggleTheme();
}
