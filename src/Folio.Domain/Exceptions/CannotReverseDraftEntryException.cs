namespace Folio.Domain.Exceptions;

/// <summary>
/// Thrown when trying to reverse a journal entry that has not been posted.
/// Only posted entries represent something worth reversing.
/// </summary>
public sealed class CannotReverseDraftEntryException : DomainException
{
    public CannotReverseDraftEntryException()
        : base("A draft journal entry cannot be reversed; only posted entries can.")
    {
    }
}
