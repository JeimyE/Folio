using Folio.Domain.Entities;

namespace Folio.Infrastructure.Seed;

/// <summary>Generates one daily USD→CRC rate across the demo window (SPEC §2.6).</summary>
public static class ExchangeRateSeedData
{
    public static IReadOnlyList<ExchangeRate> BuildRates(DateOnly startDate, DateOnly endDate)
    {
        // Fixed seed: the demo always shows the same numbers, however many times it's seeded.
        var random = new Random(20260101);
        var rates = new List<ExchangeRate>();
        var sell = 520.00m;

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var drift = ((decimal)random.NextDouble() - 0.5m) * 2m;
            sell = Math.Clamp(sell + drift, 500m, 560m);
            var sellRounded = Math.Round(sell, 2, MidpointRounding.AwayFromZero);
            var buyRounded = Math.Round(sellRounded - 2.50m, 2, MidpointRounding.AwayFromZero);
            rates.Add(new ExchangeRate(date, buyRounded, sellRounded, "BCCR"));
        }

        return rates;
    }
}
