using System.Text.RegularExpressions;

namespace Folio.Domain.ValueObjects;

/// <summary>
/// A hierarchical account code made of dot-separated numeric segments, e.g. "1.1.01".
/// </summary>
public sealed partial record AccountCode
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

    public override string ToString() => Value;

    [GeneratedRegex(@"^\d+(\.\d+)*$")]
    private static partial Regex FormatPattern();
}
