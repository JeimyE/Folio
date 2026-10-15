namespace Folio.Application.ChartOfAccounts;

/// <summary>The accounting-equation check shown as a footnote under the chart of
/// accounts tree ("Activo = Pasivo + Patrimonio"). Both totals are the net
/// debit/credit of every postable Asset account vs. every other postable account
/// (Liability, Equity, and the not-yet-closed period result from Revenue, Cost and
/// Expense), each signed per its own side of the equation — not per each
/// individual account's <see cref="Folio.Domain.Enums.NormalBalance"/>, which may
/// be overridden for a contra-account.</summary>
public sealed record BalanceCheckDto(decimal AssetTotal, decimal LiabilityAndEquityTotal)
{
    public bool IsBalanced => AssetTotal == LiabilityAndEquityTotal;
}
