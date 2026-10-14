namespace Folio.Domain.Exceptions;

/// <summary>
/// Thrown when trying to mark an account postable while it has children, or to
/// add a child under an account that is currently postable. Only leaf accounts
/// accept journal lines (SPEC §2.3).
/// </summary>
public sealed class AccountWithChildrenCannotBePostableException : DomainException
{
    public AccountWithChildrenCannotBePostableException(string accountCode)
        : base($"Account '{accountCode}' has children and cannot be postable.")
    {
    }
}
