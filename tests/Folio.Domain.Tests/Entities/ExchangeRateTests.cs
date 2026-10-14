using Folio.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Folio.Domain.Tests.Entities;

public class ExchangeRateTests
{
    [Fact]
    public void Constructor_PositiveRates_Succeeds()
    {
        var rate = new ExchangeRate(new DateOnly(2026, 1, 15), buy: 515.50m, sell: 520.75m, source: "BCCR");

        rate.Buy.Should().Be(515.50m);
        rate.Sell.Should().Be(520.75m);
    }

    [Fact]
    public void Constructor_ZeroBuy_Throws()
    {
        var act = () => new ExchangeRate(new DateOnly(2026, 1, 15), buy: 0m, sell: 520.75m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_NegativeSell_Throws()
    {
        var act = () => new ExchangeRate(new DateOnly(2026, 1, 15), buy: 515.50m, sell: -1m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Update_ValidRates_UpdatesValues()
    {
        var rate = new ExchangeRate(new DateOnly(2026, 1, 15), buy: 515.50m, sell: 520.75m);

        rate.Update(516m, 521m, "manual");

        rate.Buy.Should().Be(516m);
        rate.Sell.Should().Be(521m);
        rate.Source.Should().Be("manual");
    }
}
