using Folio.Domain.Enums;
using Folio.Domain.ValueObjects;

namespace Folio.Domain.Entities;

/// <summary>
/// One line of a <see cref="JournalEntry"/>. Always has exactly one of
/// <see cref="Debit"/>/<see cref="Credit"/> set to a positive amount in functional
/// currency (CRC) — this is guaranteed by construction, via the factory methods,
/// not by a runtime check on a shared field.
/// </summary>
public sealed class JournalLine
{
    public Guid Id { get; }

    /// <summary>1-based position within the entry. Assigned by <see cref="JournalEntry"/>.</summary>
    public int LineNumber { get; internal set; }

    public Guid AccountId { get; }

    public string? Description { get; }

    /// <summary>Debit amount in functional currency (CRC). Zero if this is a credit line.</summary>
    public decimal Debit { get; }

    /// <summary>Credit amount in functional currency (CRC). Zero if this is a debit line.</summary>
    public decimal Credit { get; }

    /// <summary>The currency the line was originally entered in.</summary>
    public Currency Currency { get; }

    /// <summary>The amount in <see cref="Currency"/>, before conversion to CRC.</summary>
    public decimal OriginalAmount { get; }

    /// <summary>The rate used to convert <see cref="OriginalAmount"/> to CRC.</summary>
    public decimal ExchangeRate { get; }

    private JournalLine(
        Guid accountId,
        decimal debit,
        decimal credit,
        Currency currency,
        decimal originalAmount,
        decimal exchangeRate,
        string? description)
    {
        Id = Guid.NewGuid();
        AccountId = accountId;
        Debit = debit;
        Credit = credit;
        Currency = currency;
        OriginalAmount = originalAmount;
        ExchangeRate = exchangeRate;
        Description = description;
    }

    public static JournalLine CreateDebit(Guid accountId, Money amount, string? description = null) =>
        CreateLocalCurrencyLine(accountId, debit: amount, credit: Money.Zero(Enums.Currency.CRC), description);

    public static JournalLine CreateCredit(Guid accountId, Money amount, string? description = null) =>
        CreateLocalCurrencyLine(accountId, debit: Money.Zero(Enums.Currency.CRC), credit: amount, description);

    /// <summary>
    /// A debit line in a foreign currency. The functional (CRC) amount is computed
    /// here as <paramref name="originalAmount"/> × <paramref name="exchangeRate"/>,
    /// rounded to 2 decimals (SPEC §2.4) — the caller supplies the original-currency
    /// amount and rate, not a pre-computed CRC amount.
    /// </summary>
    public static JournalLine CreateForeignDebit(Guid accountId, Money originalAmount, decimal exchangeRate, string? description = null)
    {
        var functional = ConvertToFunctional(originalAmount, exchangeRate);
        return new JournalLine(accountId, functional.Amount, 0m, originalAmount.Currency, originalAmount.Amount, exchangeRate, description);
    }

    public static JournalLine CreateForeignCredit(Guid accountId, Money originalAmount, decimal exchangeRate, string? description = null)
    {
        var functional = ConvertToFunctional(originalAmount, exchangeRate);
        return new JournalLine(accountId, 0m, functional.Amount, originalAmount.Currency, originalAmount.Amount, exchangeRate, description);
    }

    /// <summary>A copy of this line with debit and credit swapped, used to build a reversal.</summary>
    public JournalLine Mirror() =>
        new(AccountId, debit: Credit, credit: Debit, Currency, OriginalAmount, ExchangeRate, Description);

    private static JournalLine CreateLocalCurrencyLine(Guid accountId, Money debit, Money credit, string? description)
    {
        var amount = debit.Amount + credit.Amount;
        ValidateAmount(amount);
        return new JournalLine(accountId, debit.Amount, credit.Amount, Enums.Currency.CRC, amount, exchangeRate: 1m, description);
    }

    private static Money ConvertToFunctional(Money originalAmount, decimal exchangeRate)
    {
        if (originalAmount.Currency == Enums.Currency.CRC)
        {
            throw new ArgumentException("Use Debit/Credit for CRC-denominated lines.", nameof(originalAmount));
        }

        if (originalAmount.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(originalAmount), originalAmount.Amount, "A journal line amount must be greater than zero.");
        }

        if (exchangeRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exchangeRate), exchangeRate, "Exchange rate must be greater than zero.");
        }

        var functional = new Money(originalAmount.Amount * exchangeRate, Enums.Currency.CRC);
        ValidateAmount(functional.Amount);
        return functional;
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "A journal line amount must be greater than zero.");
        }
    }
}
