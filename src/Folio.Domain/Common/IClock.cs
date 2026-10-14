namespace Folio.Domain.Common;

/// <summary>
/// Abstracts the current time so Application/Infrastructure code never calls
/// <see cref="System.DateTime.UtcNow"/> directly, keeping tests deterministic.
/// Domain entities do not depend on this interface themselves; callers resolve
/// the current time once and pass it into entity methods as a plain value.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
