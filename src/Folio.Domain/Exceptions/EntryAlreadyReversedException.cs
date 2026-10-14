namespace Folio.Domain.Exceptions;

/// <summary>
/// Thrown when trying to reverse a journal entry that was already reversed.
/// </summary>
public sealed class EntryAlreadyReversedException : DomainException
{
    public EntryAlreadyReversedException()
        : base("This journal entry was already reversed.")
    {
    }
}
