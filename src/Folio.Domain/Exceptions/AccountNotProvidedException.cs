namespace Folio.Domain.Exceptions;

/// <summary>
/// Thrown when <see cref="Entities.JournalEntry.Post"/> is called without an
/// <see cref="Entities.Account"/> for every account referenced by the entry's lines.
/// The Domain cannot look these up itself, since it depends on no repository.
/// </summary>
public sealed class AccountNotProvidedException : DomainException
{
    public AccountNotProvidedException(Guid accountId)
        : base($"Account '{accountId}', referenced by a line, was not provided to Post().")
    {
    }
}
