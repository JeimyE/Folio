namespace Folio.Application.Abstractions;

/// <summary>
/// The user associated with the current session, used by the audit interceptor and
/// by authorization checks (e.g. "only Administrador can reopen a period" — SPEC §2.5).
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Null when there is no signed-in user (e.g. during startup/seeding).
    /// </summary>
    Guid? UserId { get; }
}
