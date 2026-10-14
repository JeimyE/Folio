using Folio.Domain.Entities;
using Folio.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Folio.Domain.Tests.Entities;

public class UserTests
{
    private static readonly DateTime CreatedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static User CreateUser() =>
        new("barista1", "Ana Pérez", "ana@sleepysloth.cr", "hash", "salt", UserRole.Contador, CreatedAt);

    [Fact]
    public void Constructor_BlankUsername_Throws()
    {
        var act = () => new User("  ", "Ana", "a@a.com", "hash", "salt", UserRole.Contador, CreatedAt);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NewUser_IsActiveAndHasNoFailedAttempts()
    {
        var user = CreateUser();

        user.IsActive.Should().BeTrue();
        user.FailedAttempts.Should().Be(0);
    }

    [Fact]
    public void RecordFailedLogin_FewerThanFiveAttempts_DoesNotLockAccount()
    {
        var user = CreateUser();
        var now = CreatedAt;

        for (var i = 0; i < 4; i++)
        {
            user.RecordFailedLogin(now);
        }

        user.FailedAttempts.Should().Be(4);
        user.IsLockedOut(now).Should().BeFalse();
    }

    [Fact]
    public void RecordFailedLogin_FifthAttempt_LocksAccountForFiveMinutes()
    {
        var user = CreateUser();
        var now = CreatedAt;

        for (var i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(now);
        }

        user.IsLockedOut(now).Should().BeTrue();
        user.IsLockedOut(now.AddMinutes(5)).Should().BeFalse();
        user.IsLockedOut(now.AddMinutes(4).AddSeconds(59)).Should().BeTrue();
    }

    [Fact]
    public void RecordSuccessfulLogin_ResetsFailedAttemptsAndLockout()
    {
        var user = CreateUser();
        var now = CreatedAt;
        for (var i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(now);
        }

        user.RecordSuccessfulLogin();

        user.FailedAttempts.Should().Be(0);
        user.IsLockedOut(now).Should().BeFalse();
    }

    [Fact]
    public void ChangePassword_ValidHashAndSalt_UpdatesBoth()
    {
        var user = CreateUser();

        user.ChangePassword("newHash", "newSalt");

        user.PasswordHash.Should().Be("newHash");
        user.Salt.Should().Be("newSalt");
    }

    [Fact]
    public void Deactivate_ActiveUser_SetsIsActiveFalse()
    {
        var user = CreateUser();

        user.Deactivate();

        user.IsActive.Should().BeFalse();
    }
}
