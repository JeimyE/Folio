namespace Folio.Desktop.ViewModels;

/// <summary>The six account-type filter tabs above the chart-of-accounts tree.
/// Costo and Gasto are combined into one tab ("Costos y gastos"), matching the
/// design reference.</summary>
public enum AccountTypeFilter
{
    All,
    Asset,
    Liability,
    Equity,
    Revenue,
    CostOrExpense,
}
