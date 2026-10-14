using Folio.Application.Abstractions;

namespace Folio.Infrastructure.Auditing;

/// <summary>
/// The single session of a single-user desktop app — registered once as a Singleton
/// under both <see cref="ICurrentUserService"/> (read, used by the audit interceptor)
/// and <see cref="ICurrentUserSession"/> (write, used only by login/logout).
/// </summary>
public sealed class CurrentUserSession : ICurrentUserSession
{
    private Guid? _userId;

    public Guid? UserId => _userId;

    public void SignIn(Guid userId) => _userId = userId;

    public void SignOut() => _userId = null;
}
