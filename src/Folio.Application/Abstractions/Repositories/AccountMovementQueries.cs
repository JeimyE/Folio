namespace Folio.Application.Abstractions.Repositories;

/// <summary>One calendar month's posted debit/credit totals for a set of accounts.</summary>
public sealed record MonthlyMovementTotals(int Year, int Month, decimal TotalDebit, decimal TotalCredit);

/// <summary>One posted journal entry that touched a set of accounts, with the
/// combined debit/credit of just the lines for those accounts (not the entry's
/// other side).</summary>
public sealed record RecentAccountEntry(Guid EntryId, int? Number, string Description, DateOnly Date, decimal TotalDebit, decimal TotalCredit);
