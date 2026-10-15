using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Folio.Application.Authentication;
using Folio.Desktop.Messages;
using Folio.Desktop.Resources;
using Folio.Domain.Common;

namespace Folio.Desktop.ViewModels;

public sealed partial class LoginViewModel(IAuthenticationService authenticationService, IMessenger messenger, IClock clock)
    : ObservableObject
{
    private static readonly CompositeFormat AccountLockedFormat = CompositeFormat.Parse(Strings.LoginAccountLockedFormat);

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand]
    private async Task SignInAsync()
    {
        ErrorMessage = null;
        IsBusy = true;

        try
        {
            var result = await authenticationService.LoginAsync(Username, Password);

            if (result.Succeeded)
            {
                messenger.Send(new UserSignedInMessage(result.UserId!.Value, result.FullName!, result.Role!.Value));
            }
            else
            {
                ErrorMessage = BuildErrorMessage(result);
            }
        }
        finally
        {
            // Never linger in memory longer than one attempt, success or failure —
            // and never anywhere in a log message either.
            Password = string.Empty;
            IsBusy = false;
        }
    }

    private string BuildErrorMessage(AuthenticationResult result) => result.FailureReason switch
    {
        AuthenticationFailureReason.AccountLocked =>
            string.Format(CultureInfo.CurrentCulture, AccountLockedFormat, MinutesUntil(result.LockedUntilUtc!.Value)),
        AuthenticationFailureReason.AccountInactive => Strings.LoginAccountInactive,
        _ => Strings.LoginInvalidCredentials,
    };

    private int MinutesUntil(DateTime utcInstant) =>
        Math.Max(1, (int)Math.Ceiling((utcInstant - clock.UtcNow).TotalMinutes));
}
