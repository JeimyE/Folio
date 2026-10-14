namespace Folio.Domain.Exceptions;

/// <summary>
/// Thrown when a journal entry's total debits do not equal its total credits,
/// in functional currency, at the moment it is posted.
/// </summary>
public sealed class UnbalancedEntryException : DomainException
{
    public UnbalancedEntryException(decimal totalDebit, decimal totalCredit)
        : base($"Journal entry is unbalanced: debit {totalDebit:N2} does not equal credit {totalCredit:N2}.")
    {
    }
}
