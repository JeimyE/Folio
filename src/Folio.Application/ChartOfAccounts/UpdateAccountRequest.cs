using Folio.Domain.Enums;

namespace Folio.Application.ChartOfAccounts;

/// <summary>
/// Code and parent are immutable after creation — this request can't change them.
/// <paramref name="NormalBalance"/> is the resulting value to set, not an optional
/// override: unlike <see cref="CreateAccountRequest"/> (where "no override" cleanly
/// means "derive from type"), an edit always has a *current* value, so the form
/// pre-fills this with the account's current normal balance and only changes it if
/// the user does — there is no ambiguous "unset" state to compare against.
/// </summary>
public sealed record UpdateAccountRequest(
    Guid Id,
    string Name,
    AccountType Type,
    bool IsPostable,
    NormalBalance NormalBalance,
    string? Description);
