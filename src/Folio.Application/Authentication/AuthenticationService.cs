using Folio.Application.Abstractions;
using Folio.Application.Abstractions.Repositories;
using Folio.Domain.Common;

namespace Folio.Application.Authentication;

/// <summary>
/// Orchestrates login (SPEC §2.1): looks up the user, enforces lockout before ever
/// checking the password, verifies it, and records the outcome on the <c>User</c>
/// itself (the Domain owns the failed-attempt/lockout rules — this service only
/// decides when to call them and persists the result).
/// </summary>
public sealed class AuthenticationService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IClock clock,
    ICurrentUserSession currentUserSession) : IAuthenticationService
{
    public async Task<AuthenticationResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByUsernameAsync(username, cancellationToken);
        var now = clock.UtcNow;

        if (user is null)
        {
            return AuthenticationResult.InvalidCredentials();
        }

        if (user.IsLockedOut(now))
        {
            return AuthenticationResult.AccountLocked(user.LockedUntilUtc!.Value);
        }

        if (!user.IsActive)
        {
            return AuthenticationResult.AccountInactive();
        }

        if (!passwordHasher.Verify(password, user.PasswordHash, user.Salt))
        {
            user.RecordFailedLogin(now);
            await userRepository.UpdateAsync(user, cancellationToken);

            return user.IsLockedOut(now)
                ? AuthenticationResult.AccountLocked(user.LockedUntilUtc!.Value)
                : AuthenticationResult.InvalidCredentials();
        }

        user.RecordSuccessfulLogin();
        await userRepository.UpdateAsync(user, cancellationToken);

        currentUserSession.SignIn(user.Id);
        return AuthenticationResult.Success(user.Id, user.FullName, user.Role);
    }
}
