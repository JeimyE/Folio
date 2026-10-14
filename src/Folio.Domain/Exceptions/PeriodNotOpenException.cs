namespace Folio.Domain.Exceptions;

/// <summary>
/// Thrown when posting or reversing a journal entry dated outside the given
/// period's range, or into a period that is closed.
/// </summary>
public sealed class PeriodNotOpenException : DomainException
{
    public PeriodNotOpenException(DateOnly date)
        : base($"There is no open period covering '{date}'.")
    {
    }
}
