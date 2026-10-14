using Folio.Domain.Entities;
using Folio.Domain.Enums;
using Folio.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Folio.Domain.Tests.Entities;

public class JournalLineTests
{
    [Fact]
    public void CreateDebit_PositiveAmount_SetsDebitAndZeroCredit()
    {
        var accountId = Guid.NewGuid();

        var line = JournalLine.CreateDebit(accountId, new Money(100m, Currency.CRC));

        line.Debit.Should().Be(100m);
        line.Credit.Should().Be(0m);
        line.AccountId.Should().Be(accountId);
        line.Currency.Should().Be(Currency.CRC);
    }

    [Fact]
    public void CreateCredit_PositiveAmount_SetsCreditAndZeroDebit()
    {
        var line = JournalLine.CreateCredit(Guid.NewGuid(), new Money(50m, Currency.CRC));

        line.Credit.Should().Be(50m);
        line.Debit.Should().Be(0m);
    }

    [Fact]
    public void CreateDebit_ZeroAmount_Throws()
    {
        var act = () => JournalLine.CreateDebit(Guid.NewGuid(), Money.Zero(Currency.CRC));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CreateCredit_NegativeAmount_Throws()
    {
        var act = () => JournalLine.CreateCredit(Guid.NewGuid(), new Money(-10m, Currency.CRC));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CreateForeignDebit_ConvertsOriginalAmountByExchangeRate_RoundedToTwoDecimals()
    {
        // A tourist pays $20.00 at ₡520.365 — the functional amount must be rounded.
        var line = JournalLine.CreateForeignDebit(Guid.NewGuid(), new Money(20m, Currency.USD), 520.365m);

        line.Debit.Should().Be(10407.30m);
        line.OriginalAmount.Should().Be(20m);
        line.ExchangeRate.Should().Be(520.365m);
        line.Currency.Should().Be(Currency.USD);
    }

    [Fact]
    public void CreateForeignCredit_CrcCurrency_Throws()
    {
        var act = () => JournalLine.CreateForeignCredit(Guid.NewGuid(), new Money(100m, Currency.CRC), 520m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateForeignDebit_ZeroExchangeRate_Throws()
    {
        var act = () => JournalLine.CreateForeignDebit(Guid.NewGuid(), new Money(20m, Currency.USD), 0m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Mirror_DebitLine_ReturnsCreditLineWithSameAccountAndAmount()
    {
        var accountId = Guid.NewGuid();
        var line = JournalLine.CreateDebit(accountId, new Money(300m, Currency.CRC), "Alquiler");

        var mirrored = line.Mirror();

        mirrored.AccountId.Should().Be(accountId);
        mirrored.Debit.Should().Be(0m);
        mirrored.Credit.Should().Be(300m);
        mirrored.Description.Should().Be("Alquiler");
    }

    [Fact]
    public void Mirror_ForeignCurrencyLine_PreservesCurrencyAndRate()
    {
        var line = JournalLine.CreateForeignCredit(Guid.NewGuid(), new Money(10m, Currency.USD), 520m);

        var mirrored = line.Mirror();

        mirrored.Debit.Should().Be(5200m);
        mirrored.Credit.Should().Be(0m);
        mirrored.Currency.Should().Be(Currency.USD);
        mirrored.OriginalAmount.Should().Be(10m);
        mirrored.ExchangeRate.Should().Be(520m);
    }

    [Fact]
    public void IndependentPerLineRounding_CanProduceARoundingMismatch_VersusConvertingTheTotalOnce()
    {
        // $10.00 split as $3.33 + $3.33 + $3.34 balances exactly in USD, but converting
        // each split independently at ₡517.77 does not tie out to converting the $10.00
        // total directly — this is exactly the scenario the rounding ADR (docs/adr/0002)
        // covers: the Domain validates the functional-currency total strictly and does
        // not paper over this gap; whoever builds the lines must avoid it.
        const decimal rate = 517.77m;

        var line1 = JournalLine.CreateForeignCredit(Guid.NewGuid(), new Money(3.33m, Currency.USD), rate).Credit;
        var line2 = JournalLine.CreateForeignCredit(Guid.NewGuid(), new Money(3.33m, Currency.USD), rate).Credit;
        var line3 = JournalLine.CreateForeignCredit(Guid.NewGuid(), new Money(3.34m, Currency.USD), rate).Credit;
        var splitTotal = line1 + line2 + line3;

        var directTotal = JournalLine.CreateForeignDebit(Guid.NewGuid(), new Money(10.00m, Currency.USD), rate).Debit;

        line1.Should().Be(1724.17m);
        line2.Should().Be(1724.17m);
        line3.Should().Be(1729.35m);
        directTotal.Should().Be(5177.70m);
        splitTotal.Should().Be(5177.69m);
        splitTotal.Should().NotBe(directTotal);
    }
}
