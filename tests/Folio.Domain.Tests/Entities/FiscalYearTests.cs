using Folio.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Folio.Domain.Tests.Entities;

public class FiscalYearTests
{
    [Fact]
    public void Constructor_EndDateNotAfterStartDate_Throws()
    {
        var act = () => new FiscalYear(2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GeneratePeriods_FullCalendarYear_Returns12MonthlyPeriods()
    {
        var fiscalYear = new FiscalYear(2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        var periods = fiscalYear.GeneratePeriods();

        periods.Should().HaveCount(12);
        periods[0].StartDate.Should().Be(new DateOnly(2026, 1, 1));
        periods[0].EndDate.Should().Be(new DateOnly(2026, 1, 31));
        periods[1].StartDate.Should().Be(new DateOnly(2026, 2, 1));
        periods[1].EndDate.Should().Be(new DateOnly(2026, 2, 28));
        periods[11].StartDate.Should().Be(new DateOnly(2026, 12, 1));
        periods[11].EndDate.Should().Be(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public void GeneratePeriods_LastPeriodTruncatedToFiscalYearEnd()
    {
        var fiscalYear = new FiscalYear(2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 15));

        var periods = fiscalYear.GeneratePeriods();

        periods.Should().HaveCount(6);
        periods[5].StartDate.Should().Be(new DateOnly(2026, 6, 1));
        periods[5].EndDate.Should().Be(new DateOnly(2026, 6, 15));
    }

    [Fact]
    public void GeneratePeriods_AllPeriodsReferenceFiscalYearId()
    {
        var fiscalYear = new FiscalYear(2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        var periods = fiscalYear.GeneratePeriods();

        periods.Should().OnlyContain(p => p.FiscalYearId == fiscalYear.Id);
    }

    [Fact]
    public void Close_OpenFiscalYear_SetsIsClosed()
    {
        var fiscalYear = new FiscalYear(2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        fiscalYear.Close();

        fiscalYear.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void Close_AlreadyClosedFiscalYear_Throws()
    {
        var fiscalYear = new FiscalYear(2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        fiscalYear.Close();

        var act = fiscalYear.Close;

        act.Should().Throw<InvalidOperationException>();
    }
}
