namespace Folio.Application.Abstractions;

/// <summary>
/// The write side of the current session: signs a user in or out. Kept separate from
/// <see cref="ICurrentUserService"/> (read-only) so most of the app — including the
/// audit interceptor — only ever sees the read-only view; only the login/logout flow
/// needs this.
/// </summary>
public interface ICurrentUserSession : ICurrentUserService
{
    void SignIn(Guid userId);

    void SignOut();
}
