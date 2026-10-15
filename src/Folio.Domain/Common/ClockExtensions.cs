namespace Folio.Domain.Common;

/// <summary>
/// The one place that turns "now" into an accounting date. Accounting dates
/// (<see cref="DateOnly"/>) are always the calendar date in Costa Rica, where the
/// company operates — never the UTC date, which is already "tomorrow" from 6 p.m.
/// local time onwards. Timestamps stay in UTC (<see cref="IClock.UtcNow"/>).
/// </summary>
public static class ClockExtensions
{
    /// <summary>IANA id of the company's time zone (SPEC: Costa Rican SMEs).</summary>
    public const string CostaRicaTimeZoneId = "America/Costa_Rica";

    // Windows resolves IANA ids through ICU; the Windows id is the fallback if ICU is
    // unavailable. Costa Rica is a fixed UTC-6 with no daylight saving time.
    private static readonly TimeZoneInfo CostaRicaTimeZone =
        TimeZoneInfo.TryFindSystemTimeZoneById(CostaRicaTimeZoneId, out var timeZone)
            ? timeZone
            : TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");

    /// <summary>
    /// Today's accounting date in Costa Rica, derived from <see cref="IClock.UtcNow"/>.
    /// An extension rather than an <see cref="IClock"/> member so every clock (including
    /// test substitutes that only set <c>UtcNow</c>) gets the same conversion.
    /// </summary>
    public static DateOnly TodayInCostaRica(this IClock clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, CostaRicaTimeZone));
}
