namespace Folio.Domain.Exceptions;

/// <summary>
/// Thrown when a journal line references an account that is not postable
/// (i.e. it has children, or was never marked as a leaf account).
/// </summary>
public sealed class NonPostableAccountException : DomainException
{
    public NonPostableAccountException(Guid accountId)
        : base($"Account '{accountId}' is not postable.")
    {
    }
}
