using CommunityToolkit.Mvvm.Messaging;
using Folio.Application.Authentication;
using Folio.Desktop.Messages;
using Folio.Desktop.ViewModels;
using Folio.Domain.Common;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Folio.Desktop.Tests.ViewModels;

public class LoginViewModelTests
{
    // A real WeakReferenceMessenger instance (not a mock): IMessenger's Send<T> goes
    // through an extension method resolving a generic token type, which is awkward to
    // verify precisely with NSubstitute. Registering a capturing recipient is simpler
    // and exercises the exact same code path LoginViewModel actually runs through.
    private static readonly DateTime Now = new(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);

    private readonly IAuthenticationService _authenticationService = Substitute.For<IAuthenticationService>();
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly LoginViewModel _sut;

    public LoginViewModelTests()
    {
        _clock.UtcNow.Returns(Now);
        _sut = new LoginViewModel(_authenticationService, _messenger, _clock);
    }

    [Fact]
    public async Task SignInAsync_Success_SendsUserSignedInMessage()
    {
        var userId = Guid.NewGuid();
        UserSignedInMessage? captured = null;
        var recipient = new object();
        _messenger.Register<UserSignedInMessage>(recipient, (_, message) => captured = message);

        _sut.Username = "admin";
        _sut.Password = "Folio2026!";
        _authenticationService.LoginAsync("admin", "Folio2026!", Arg.Any<CancellationToken>())
            .Returns(AuthenticationResult.Success(userId, "Administradora del Sistema"));

        await _sut.SignInCommand.ExecuteAsync(null);

        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(userId);
        captured.FullName.Should().Be("Administradora del Sistema");
    }

    [Fact]
    public async Task SignInAsync_InvalidCredentials_SetsGenericErrorMessage()
    {
        _authenticationService.LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(AuthenticationResult.InvalidCredentials());

        await _sut.SignInCommand.ExecuteAsync(null);

        _sut.ErrorMessage.Should().Be("Usuario o contraseña incorrectos.");
    }

    [Fact]
    public async Task SignInAsync_AccountLocked_SetsErrorMessageWithMinutesRemaining()
    {
        _authenticationService.LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(AuthenticationResult.AccountLocked(Now.AddMinutes(5)));

        await _sut.SignInCommand.ExecuteAsync(null);

        _sut.ErrorMessage.Should().Contain("5 minuto");
    }

    [Fact]
    public async Task SignInAsync_AccountInactive_SetsInactiveErrorMessage()
    {
        _authenticationService.LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(AuthenticationResult.AccountInactive());

        await _sut.SignInCommand.ExecuteAsync(null);

        _sut.ErrorMessage.Should().Be("Esta cuenta está inactiva. Contacte a un administrador.");
    }

    [Fact]
    public async Task SignInAsync_AnyFailure_ClearsPassword()
    {
        _sut.Password = "whatever";
        _authenticationService.LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(AuthenticationResult.InvalidCredentials());

        await _sut.SignInCommand.ExecuteAsync(null);

        _sut.Password.Should().BeEmpty();
    }

    [Fact]
    public async Task SignInAsync_Success_ClearsPassword()
    {
        _sut.Password = "Folio2026!";
        _authenticationService.LoginAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(AuthenticationResult.Success(Guid.NewGuid(), "Ana"));

        await _sut.SignInCommand.ExecuteAsync(null);

        _sut.Password.Should().BeEmpty();
    }
}
