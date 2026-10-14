using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Folio.Application.Abstractions;
using Folio.Desktop.Messages;
using Folio.Desktop.Navigation;
using Folio.Desktop.Theming;

namespace Folio.Desktop.ViewModels;

public sealed partial class ShellViewModel(
    ICurrentUserSession currentUserSession,
    IMessenger messenger,
    IThemeService themeService,
    INavigationService navigationService) : ObservableObject
{
    [ObservableProperty]
    private string _fullName = string.Empty;

    public INavigationService Navigation => navigationService;

    public void Initialize(string fullName)
    {
        FullName = fullName;
        navigationService.NavigateTo<WelcomeViewModel>().Initialize(fullName);
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
