namespace Folio.Desktop.ViewModels;

/// <summary>One bar of the detail panel's 6-month balance sparkline.
/// <paramref name="Height"/> is already normalized to a pixel height the view can
/// bind directly (0 maps to a thin, still-visible sliver, not nothing) — the
/// ViewModel does this math, not the view, because WPF has no convenient way to
/// scale a value against a dynamic min/max purely in XAML.</summary>
public sealed record MonthlyBalanceBarViewModel(double Height, bool IsCurrentMonth, string MonthAbbreviation);
