namespace Folio.Domain.Enums;

/// <summary>
/// The lifecycle state of a <see cref="Entities.JournalEntry"/>.
/// </summary>
public enum JournalEntryStatus
{
    Draft,
    Posted,
    Reversed,
}
