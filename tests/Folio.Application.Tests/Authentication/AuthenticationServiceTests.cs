using Folio.Application.Abstractions;
using Folio.Application.Abstractions.Repositories;
using Folio.Application.Authentication;
using Folio.Domain.Common;
using Folio.Domain.Entities;
using Folio.Domain.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Folio.Application.Tests.Authentication;

public class AuthenticationServiceTests
{
    private static readonly DateTime Now = new(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
    private const string CorrectPassword = "Folio2026!";
    private const string WrongPassword = "wrong-password";

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ICurrentUserSession _currentUserSession = Substitute.For<ICurrentUserSession>();
    private readonly AuthenticationService _sut;

    public AuthenticationServiceTests()
    {
        _clock.UtcNow.Returns(Now);
        _sut = new AuthenticationService(_userRepository, _passwordHasher, _clock, _currentUserSession);

        // The hasher is a stub: "correct" hash/salt pair verifies only against CorrectPassword.
        _passwordHasher.Verify(CorrectPassword, "hash", "salt").Returns(true);
        _passwordHasher.Verify(WrongPassword, "hash", "salt").Returns(false);
    }

    private static User CreateUser(UserRole role = UserRole.Contador) =>
        new("barista1", "Ana Pérez", "ana@sleepysloth.cr", "hash", "salt", role, Now);

    [Fact]
    public async Task LoginAsync_UserDoesNotExist_ReturnsInvalidCredentials()
    {
        _userRepository.GetByUsernameAsync("ghost", Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await _sut.LoginAsync("ghost", CorrectPassword);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(AuthenticationFailureReason.InvalidCredentials);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_IncrementsFailedAttemptsAndReturnsInvalidCredentials()
    {
        var user = CreateUser();
        _userRepository.GetByUsernameAsync(user.Username, Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.LoginAsync(user.Username, WrongPassword);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(AuthenticationFailureReason.InvalidCredentials);
        user.FailedAttempts.Should().Be(1);
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_FifthWrongPasswordAttempt_LocksTheAccount()
    {
        var user = CreateUser();
        for (var i = 0; i < 4; i++)
        {
            user.RecordFailedLogin(Now);
        }

        _userRepository.GetByUsernameAsync(user.Username, Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.LoginAsync(user.Username, WrongPassword);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(AuthenticationFailureReason.AccountLocked);
        result.LockedUntilUtc.Should().Be(Now.AddMinutes(5));
    }

    [Fact]
    public async Task LoginAsync_AccountAlreadyLocked_ReturnsAccountLockedWithoutCheckingPassword()
    {
        var user = CreateUser();
        for (var i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(Now);
        }

        _userRepository.GetByUsernameAsync(user.Username, Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.LoginAsync(user.Username, CorrectPassword);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(AuthenticationFailureReason.AccountLocked);
        _passwordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task LoginAsync_InactiveUser_ReturnsAccountInactive()
    {
        var user = CreateUser();
        user.Deactivate();
        _userRepository.GetByUsernameAsync(user.Username, Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.LoginAsync(user.Username, CorrectPassword);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be(AuthenticationFailureReason.AccountInactive);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ResetsFailedAttemptsAndSignsIn()
    {
        var user = CreateUser();
        user.RecordFailedLogin(Now);
        user.RecordFailedLogin(Now);
        _userRepository.GetByUsernameAsync(user.Username, Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.LoginAsync(user.Username, CorrectPassword);

        result.Succeeded.Should().BeTrue();
        result.UserId.Should().Be(user.Id);
        result.FullName.Should().Be(user.FullName);
        user.FailedAttempts.Should().Be(0);
        _currentUserSession.Received(1).SignIn(user.Id);
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_NonexistentUserAndWrongPassword_ReturnTheSameFailureReason()
    {
        _userRepository.GetByUsernameAsync("ghost", Arg.Any<CancellationToken>()).Returns((User?)null);
        var existingUser = CreateUser();
        _userRepository.GetByUsernameAsync(existingUser.Username, Arg.Any<CancellationToken>()).Returns(existingUser);

        var missingUserResult = await _sut.LoginAsync("ghost", CorrectPassword);
        var wrongPasswordResult = await _sut.LoginAsync(existingUser.Username, WrongPassword);

        missingUserResult.FailureReason.Should().Be(wrongPasswordResult.FailureReason);
        missingUserResult.FailureReason.Should().Be(AuthenticationFailureReason.InvalidCredentials);
    }
}
