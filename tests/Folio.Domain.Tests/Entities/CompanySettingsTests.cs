using Folio.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Folio.Domain.Tests.Entities;

public class CompanySettingsTests
{
    private static CompanySettings CreateSettings() =>
        new("Sleepy Sloth Café & Bakery S.A.", "3-101-123456", fiscalYearStartMonth: 1, vatRate: 0.13m);

    [Fact]
    public void Constructor_BlankName_Throws()
    {
        var act = () => new CompanySettings("  ", "3-101-123456", 1, 0.13m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_InvalidFiscalYearStartMonth_Throws()
    {
        var act = () => new CompanySettings("Sleepy Sloth", "3-101-123456", 13, 0.13m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Constructor_VatRateOutOfRange_Throws(double vatRate)
    {
        var act = () => new CompanySettings("Sleepy Sloth", "3-101-123456", 1, (decimal)vatRate);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UpdateVatRate_ValidRate_UpdatesValue()
    {
        var settings = CreateSettings();

        settings.UpdateVatRate(0.14m);

        settings.VatRate.Should().Be(0.14m);
    }

    [Fact]
    public void UpdateContactInfo_UpdatesAllFields()
    {
        var settings = CreateSettings();

        settings.UpdateContactInfo("Jacó, Costa Rica", "+506 2222-3333", "info@sleepysloth.cr");

        settings.Address.Should().Be("Jacó, Costa Rica");
        settings.Phone.Should().Be("+506 2222-3333");
        settings.Email.Should().Be("info@sleepysloth.cr");
    }
}
