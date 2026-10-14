using Folio.Domain.Enums;

namespace Folio.Domain.ValueObjects;

/// <summary>
/// An amount of money in a specific currency. Always rounded to 2 decimals with
/// <see cref="MidpointRounding.AwayFromZero"/> on construction, per SPEC §3.
/// </summary>
public readonly record struct Money
{
    public decimal Amount { get; }

    public Currency Currency { get; }

    public Money(decimal amount, Currency currency)
    {
        Amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency;
    }

    public static Money Zero(Currency currency) => new(0m, currency);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot combine {left.Currency} and {right.Currency} amounts.");
        }
    }

    public override string ToString() => $"{Amount:N2} {Currency}";
}
