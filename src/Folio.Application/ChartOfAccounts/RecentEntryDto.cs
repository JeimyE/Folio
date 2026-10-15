namespace Folio.Application.ChartOfAccounts;

/// <summary>One posted journal entry shown in the detail panel's "últimos
/// asientos" list, carrying only the viewed account's own side of the entry — if
/// the account appears on several lines of the same entry, they're summed into
/// one row rather than shown separately.</summary>
public sealed record RecentEntryDto(Guid Id, int? Number, string Description, DateOnly Date, decimal Debit, decimal Credit);
