using System.Globalization;
using System.Text.RegularExpressions;

namespace Folio.Domain.ValueObjects;

/// <summary>
/// A hierarchical account code made of dot-separated numeric segments, e.g. "1.1.01".
/// </summary>
public sealed partial record AccountCode : IComparable<AccountCode>
{
    public string Value { get; }

    public AccountCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !FormatPattern().IsMatch(value))
        {
            throw new ArgumentException($"'{value}' is not a valid account code.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// The code of the account one level up the hierarchy, or <c>null</c> for a top-level code.
    /// </summary>
    public AccountCode? Parent()
    {
        var lastDot = Value.LastIndexOf('.');
        return lastDot < 0 ? null : new AccountCode(Value[..lastDot]);
    }

    public bool IsChildOf(AccountCode other) => Parent() == other;

    /// <summary>
    /// Orders codes by comparing each dot-separated segment as a number, not as
    /// text — so "1.1.2" sorts before "1.1.10" (a plain string compare would put
    /// "1.1.10" first, since '1' &lt; '2'). A code with fewer segments sorts before
    /// one of its own children when every shared segment is equal (e.g. "1.1"
    /// before "1.1.01").
    /// </summary>
    public int CompareTo(AccountCode? other)
    {
        if (other is null)
        {
            return 1;
        }

        var mine = Value.Split('.');
        var theirs = other.Value.Split('.');

        for (var i = 0; i < Math.Min(mine.Length, theirs.Length); i++)
        {
            var segmentComparison = int.Parse(mine[i], CultureInfo.InvariantCulture)
                .CompareTo(int.Parse(theirs[i], CultureInfo.InvariantCulture));
            if (segmentComparison != 0)
            {
                return segmentComparison;
            }
        }

        return mine.Length.CompareTo(theirs.Length);
    }

    public static bool operator <(AccountCode left, AccountCode right) => left.CompareTo(right) < 0;

    public static bool operator <=(AccountCode left, AccountCode right) => left.CompareTo(right) <= 0;

    public static bool operator >(AccountCode left, AccountCode right) => left.CompareTo(right) > 0;

    public static bool operator >=(AccountCode left, AccountCode right) => left.CompareTo(right) >= 0;

    public override string ToString() => Value;

    [GeneratedRegex(@"^\d+(\.\d+)*$")]
    private static partial Regex FormatPattern();
}
