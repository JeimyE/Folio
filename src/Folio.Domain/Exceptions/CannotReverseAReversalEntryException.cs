namespace Folio.Domain.Exceptions;

/// <summary>
/// Thrown when trying to reverse a journal entry that is itself the reversal
/// of another entry. Reversals mirror the original; they are not reversed in turn.
/// </summary>
public sealed class CannotReverseAReversalEntryException : DomainException
{
    public CannotReverseAReversalEntryException()
        : base("A reversal entry cannot itself be reversed.")
    {
    }
}
