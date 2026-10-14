namespace Folio.Domain.Exceptions;

/// <summary>
/// Thrown when posting a journal entry that has fewer than the 2 lines required
/// by SPEC §2.4. Drafts are allowed to have fewer lines while being edited.
/// </summary>
public sealed class JournalEntryHasFewerThanTwoLinesException : DomainException
{
    public JournalEntryHasFewerThanTwoLinesException(int lineCount)
        : base($"A journal entry needs at least 2 lines to be posted, but has {lineCount}.")
    {
    }
}
