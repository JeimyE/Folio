using Folio.Domain.Enums;
using Folio.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Folio.Domain.Tests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Constructor_MidpointAboveZero_RoundsAwayFromZero()
    {
        var money = new Money(10.005m, Currency.CRC);

        money.Amount.Should().Be(10.01m);
    }

    [Fact]
    public void Constructor_BelowMidpoint_RoundsDown()
    {
        var money = new Money(10.004m, Currency.CRC);

        money.Amount.Should().Be(10.00m);
    }

    [Fact]
    public void Constructor_MidpointBelowZero_RoundsAwayFromZero()
    {
        var money = new Money(-10.005m, Currency.CRC);

        money.Amount.Should().Be(-10.01m);
    }

    [Fact]
    public void Addition_SameCurrency_SumsAmounts()
    {
        var result = new Money(10.50m, Currency.CRC) + new Money(5.25m, Currency.CRC);

        result.Amount.Should().Be(15.75m);
        result.Currency.Should().Be(Currency.CRC);
    }

    [Fact]
    public void Addition_DifferentCurrencies_Throws()
    {
        var act = () => new Money(10m, Currency.CRC) + new Money(5m, Currency.USD);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Subtraction_DifferentCurrencies_Throws()
    {
        var act = () => new Money(10m, Currency.CRC) - new Money(5m, Currency.USD);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Zero_ReturnsZeroAmountInGivenCurrency()
    {
        var money = Money.Zero(Currency.USD);

        money.Amount.Should().Be(0m);
        money.Currency.Should().Be(Currency.USD);
    }
}
