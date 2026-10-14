namespace Folio.Domain.Exceptions;

/// <summary>
/// Thrown when a journal line references an account that has been deactivated.
/// </summary>
public sealed class InactiveAccountException : DomainException
{
    public InactiveAccountException(Guid accountId)
        : base($"Account '{accountId}' is not active.")
    {
    }
}
