namespace Folio.Application.ChartOfAccounts;

/// <summary>
/// One of the account's last six calendar months, for the detail panel's balance
/// sparkline. <see cref="Balance"/> is the account's **closing balance at the end
/// of that month** — the running total of every movement from the start of the
/// account's history through the end of that month, signed per
/// <see cref="Folio.Domain.Enums.NormalBalance"/> — not just that month's own net
/// movement. A month with no activity of its own still carries forward the
/// previous month's closing balance, so the first month of the six-month window
/// already reflects everything that happened before it.
/// </summary>
public sealed record MonthlyBalanceDto(int Year, int Month, decimal Balance);
