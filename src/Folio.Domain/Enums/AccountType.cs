namespace Folio.Domain.Enums;

/// <summary>
/// Classifies an account for financial statement purposes. Drives the default
/// <see cref="NormalBalance"/> unless overridden for a contra-account.
/// </summary>
public enum AccountType
{
    Asset,
    Liability,
    Equity,
    Revenue,
    Cost,
    Expense,
}
