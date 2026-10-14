using Folio.Application.Abstractions;

namespace Folio.Infrastructure.Auditing;

/// <summary>
/// Reports no signed-in user. Used before a real login session exists — e.g. while
/// the app's composition root is starting up — so the audit interceptor still has
/// something to call. M3 replaces this with the logged-in user's id after sign-in.
/// </summary>
public sealed class SystemCurrentUserService : ICurrentUserService
{
    public Guid? UserId => null;
}
