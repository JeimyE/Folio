namespace Folio.Domain.Entities;

/// <summary>
/// The daily USD→CRC buy/sell exchange rate. SPEC §2.6 uses the sell rate as the
/// default reference for journal entries.
/// </summary>
public sealed class ExchangeRate
{
    public Guid Id { get; }

    public DateOnly Date { get; }

    public decimal Buy { get; private set; }

    public decimal Sell { get; private set; }

    public string? Source { get; private set; }

    public ExchangeRate(DateOnly date, decimal buy, decimal sell, string? source = null)
    {
        if (buy <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(buy), buy, "Buy rate must be greater than zero.");
        }

        if (sell <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sell), sell, "Sell rate must be greater than zero.");
        }

        Id = Guid.NewGuid();
        Date = date;
        Buy = buy;
        Sell = sell;
        Source = source;
    }

    public void Update(decimal buy, decimal sell, string? source = null)
    {
        if (buy <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(buy), buy, "Buy rate must be greater than zero.");
        }

        if (sell <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sell), sell, "Sell rate must be greater than zero.");
        }

        Buy = buy;
        Sell = sell;
        Source = source;
    }
}
