namespace Folio.Infrastructure.Persistence;

/// <summary>
/// One row of the audit log (SPEC §2.9). This is an Infrastructure-only record of
/// what <see cref="Auditing.AuditSaveChangesInterceptor"/> observed — it carries no
/// business rules, so it does not belong in Domain.
/// </summary>
public sealed class AuditLogEntry
{
    public Guid Id { get; init; }

    public DateTime TimestampUtc { get; init; }

    public Guid UserId { get; init; }

    public string Entity { get; init; } = string.Empty;

    public string EntityId { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public string? OldValuesJson { get; init; }

    public string? NewValuesJson { get; init; }
}
