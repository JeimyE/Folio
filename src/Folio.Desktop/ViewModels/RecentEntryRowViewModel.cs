namespace Folio.Desktop.ViewModels;

/// <summary>One row of the detail panel's "últimos asientos" list — the viewed
/// account's own amount and which side (D/H) it was on, already picked out of
/// whichever of <c>RecentEntryDto</c>'s Debit/Credit was the nonzero one.</summary>
public sealed record RecentEntryRowViewModel(int? Number, string Description, decimal Amount, string Side);
