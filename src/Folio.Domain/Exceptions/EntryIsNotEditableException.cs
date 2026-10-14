using Folio.Domain.Enums;

namespace Folio.Domain.Exceptions;

/// <summary>
/// Thrown when trying to add, update or remove a line on a journal entry that
/// is not in <see cref="JournalEntryStatus.Draft"/>. Posted entries are immutable.
/// </summary>
public sealed class EntryIsNotEditableException : DomainException
{
    public EntryIsNotEditableException(JournalEntryStatus status)
        : base($"A journal entry in status '{status}' cannot be edited.")
    {
    }
}
