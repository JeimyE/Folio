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

    private AuthenticationResult(bool succeeded, AuthenticationFailureReason? failureReason, DateTime? lockedUntilUtc, Guid? userId, string? fullName)
    {
        Succeeded = succeeded;
        FailureReason = failureReason;
        LockedUntilUtc = lockedUntilUtc;
        UserId = userId;
        FullName = fullName;
    }

    public static AuthenticationResult Success(Guid userId, string fullName) =>
        new(true, null, null, userId, fullName);

    public static AuthenticationResult InvalidCredentials() =>
        new(false, AuthenticationFailureReason.InvalidCredentials, null, null, null);

    public static AuthenticationResult AccountLocked(DateTime lockedUntilUtc) =>
        new(false, AuthenticationFailureReason.AccountLocked, lockedUntilUtc, null, null);

    public static AuthenticationResult AccountInactive() =>
        new(false, AuthenticationFailureReason.AccountInactive, null, null, null);
}
