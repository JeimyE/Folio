using Folio.Domain.Enums;

namespace Folio.Application.ChartOfAccounts;

/// <summary>
/// <paramref name="Code"/> is the full dotted code (e.g. "1.1.07") — the parent is
/// derived from it (everything before the last segment), never chosen separately,
/// so the two can never disagree with each other.
/// </summary>
public sealed record CreateAccountRequest(
    string Code,
    string Name,
    AccountType Type,
    bool IsPostable,
    NormalBalance? NormalBalanceOverride,
    string? Description);
