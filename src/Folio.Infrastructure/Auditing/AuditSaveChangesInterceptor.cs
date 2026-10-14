using System.Text.Json;
using System.Text.Json.Serialization;
using Folio.Application.Abstractions;
using Folio.Domain.Common;
using Folio.Domain.Entities;
using Folio.Domain.Enums;
using Folio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Folio.Infrastructure.Auditing;

/// <summary>
/// Automatically records a create/update/delete/post/reverse/close/reopen row (SPEC
/// §2.9) for every tracked change, by inspecting the <see cref="DbContext"/>'s
/// <see cref="ChangeTracker"/> right before it saves — no feature has to remember to
/// call an "audit this" method.
/// </summary>
public sealed class AuditSaveChangesInterceptor(IClock clock, ICurrentUserService currentUserService) : SaveChangesInterceptor
{
    /// <summary>
    /// Property names that must never appear in OldValues/NewValues, on any entity.
    /// </summary>
    private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(User.PasswordHash),
        nameof(User.Salt),
    };

    // Enums serialize as "Draft"/"Posted" etc., not 0/1 — the audit log is meant to be
    // read by a human (M9's Bitácora screen), not just stored.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            AddAuditEntries(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            AddAuditEntries(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditEntries(DbContext context)
    {
        var userId = currentUserService.UserId ?? Guid.Empty;
        var now = clock.UtcNow;
        var auditEntries = new List<AuditLogEntry>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLogEntry || entry.State is EntityState.Unchanged or EntityState.Detached)
            {
                continue;
            }

            var (oldValues, newValues) = BuildValueDictionaries(entry);
            if (oldValues is null && newValues is null)
            {
                // A Modified entry where every changed property was redacted away.
                continue;
            }

            auditEntries.Add(new AuditLogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = now,
                UserId = userId,
                Entity = entry.Entity.GetType().Name,
                EntityId = GetEntityId(entry),
                Action = DetermineAction(entry),
                OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues, JsonOptions),
                NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues, JsonOptions),
            });
        }

        if (auditEntries.Count > 0)
        {
            context.Set<AuditLogEntry>().AddRange(auditEntries);
        }
    }

    private static string DetermineAction(EntityEntry entry)
    {
        if (entry.State == EntityState.Modified)
        {
            if (entry.Entity is JournalEntry)
            {
                var status = entry.Property(nameof(JournalEntry.Status));
                if (Equals(status.OriginalValue, JournalEntryStatus.Draft) && Equals(status.CurrentValue, JournalEntryStatus.Posted))
                {
                    return "Post";
                }

                if (Equals(status.OriginalValue, JournalEntryStatus.Posted) && Equals(status.CurrentValue, JournalEntryStatus.Reversed))
                {
                    return "Reverse";
                }
            }
            else if (entry.Entity is Period)
            {
                var status = entry.Property(nameof(Period.Status));
                if (Equals(status.OriginalValue, PeriodStatus.Open) && Equals(status.CurrentValue, PeriodStatus.Closed))
                {
                    return "Close";
                }

                if (Equals(status.OriginalValue, PeriodStatus.Closed) && Equals(status.CurrentValue, PeriodStatus.Open))
                {
                    return "Reopen";
                }
            }
        }

        return entry.State switch
        {
            EntityState.Added => "Create",
            EntityState.Deleted => "Delete",
            _ => "Update",
        };
    }

    private static string GetEntityId(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null)
        {
            return string.Empty;
        }

        var values = key.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? "null");
        return string.Join(",", values);
    }

    private static (Dictionary<string, object?>? OldValues, Dictionary<string, object?>? NewValues) BuildValueDictionaries(EntityEntry entry)
    {
        switch (entry.State)
        {
            case EntityState.Added:
                return (null, BuildSnapshot(entry, useOriginalValue: false));

            case EntityState.Deleted:
                return (BuildSnapshot(entry, useOriginalValue: true), null);

            case EntityState.Modified:
                var oldValues = new Dictionary<string, object?>();
                var newValues = new Dictionary<string, object?>();

                foreach (var property in entry.Properties)
                {
                    if (!property.IsModified || IsSensitive(property))
                    {
                        continue;
                    }

                    oldValues[property.Metadata.Name] = property.OriginalValue;
                    newValues[property.Metadata.Name] = property.CurrentValue;
                }

                return (oldValues.Count == 0 ? null : oldValues, newValues.Count == 0 ? null : newValues);

            default:
                return (null, null);
        }
    }

    private static Dictionary<string, object?> BuildSnapshot(EntityEntry entry, bool useOriginalValue)
    {
        var snapshot = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            if (IsSensitive(property))
            {
                continue;
            }

            snapshot[property.Metadata.Name] = useOriginalValue ? property.OriginalValue : property.CurrentValue;
        }

        return snapshot;
    }

    private static bool IsSensitive(PropertyEntry property) => SensitivePropertyNames.Contains(property.Metadata.Name);
}
