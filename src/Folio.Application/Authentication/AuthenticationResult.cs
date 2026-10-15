using Folio.Domain.Enums;

namespace Folio.Application.Authentication;

public enum AuthenticationFailureReason
{
    InvalidCredentials,
    AccountLocked,
    AccountInactive,
}

/// <summary>
/// The outcome of <see cref="IAuthenticationService.LoginAsync"/>. Carries the minimum
/// the UI needs — never the full <c>User</c> entity — and deliberately uses the same
/// <see cref="AuthenticationFailureReason.InvalidCredentials"/> value whether the
/// username doesn't exist or the password is wrong, so the UI can never leak which
/// one it was.
/// </summary>
public sealed class AuthenticationResult
{
    public bool Succeeded { get; }

    public AuthenticationFailureReason? FailureReason { get; }

    public DateTime? LockedUntilUtc { get; }

    public Guid? UserId { get; }

    public string? FullName { get; }

    public UserRole? Role { get; }

    private AuthenticationResult(bool succeeded, AuthenticationFailureReason? failureReason, DateTime? lockedUntilUtc, Guid? userId, string? fullName, UserRole? role)
    {
        Succeeded = succeeded;
        FailureReason = failureReason;
        LockedUntilUtc = lockedUntilUtc;
        UserId = userId;
        FullName = fullName;
        Role = role;
    }

    public static AuthenticationResult Success(Guid userId, string fullName, UserRole role) =>
        new(true, null, null, userId, fullName, role);

    public static AuthenticationResult InvalidCredentials() =>
        new(false, AuthenticationFailureReason.InvalidCredentials, null, null, null, null);

    public static AuthenticationResult AccountLocked(DateTime lockedUntilUtc) =>
        new(false, AuthenticationFailureReason.AccountLocked, lockedUntilUtc, null, null, null);

    public static AuthenticationResult AccountInactive() =>
        new(false, AuthenticationFailureReason.AccountInactive, null, null, null, null);
}
