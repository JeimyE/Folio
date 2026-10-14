using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Folio.Infrastructure.Persistence.ValueConversion;

/// <summary>
/// Stores a money amount as an integer number of cents (e.g. ₡1,234.56 → 123456)
/// instead of as text or a floating-point column. SQLite's SUM/ORDER BY/comparison
/// operators work correctly and exactly on INTEGER columns, but not on the TEXT
/// columns EF Core otherwise uses for <see cref="decimal"/> — see ADR-0003. Every
/// amount passing through this converter is already rounded to 2 decimals by the
/// Domain's <see cref="Folio.Domain.ValueObjects.Money"/>, so the ×100 conversion
/// is always exact, never a source of rounding error itself.
/// </summary>
public sealed class MoneyToCentsConverter : ValueConverter<decimal, long>
{
    public MoneyToCentsConverter()
        : base(
            amount => decimal.ToInt64(Math.Round(amount * 100m, 0, MidpointRounding.AwayFromZero)),
            cents => cents / 100m)
    {
    }
}
