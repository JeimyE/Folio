using Folio.Domain.Entities;
using Folio.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Folio.Domain.Tests.Entities;

public class PeriodTests
{
    private static Period CreateOpenPeriod() =>
        new(Guid.NewGuid(), 2026, 3, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

    [Fact]
    public void Constructor_NewPeriod_IsOpen()
    {
        var period = CreateOpenPeriod();

        period.Status.Should().Be(PeriodStatus.Open);
    }

    [Fact]
    public void IsOpenFor_DateWithinOpenPeriod_ReturnsTrue()
    {
        var period = CreateOpenPeriod();

        period.IsOpenFor(new DateOnly(2026, 3, 15)).Should().BeTrue();
    }

    [Fact]
    public void IsOpenFor_DateOutsidePeriodRange_ReturnsFalse()
    {
        var period = CreateOpenPeriod();

        period.IsOpenFor(new DateOnly(2026, 4, 1)).Should().BeFalse();
    }

    [Fact]
    public void IsOpenFor_ClosedPeriod_ReturnsFalse()
    {
        var period = CreateOpenPeriod();
        period.Close(Guid.NewGuid(), DateTime.UtcNow);

        period.IsOpenFor(new DateOnly(2026, 3, 15)).Should().BeFalse();
    }

    [Fact]
    public void Close_OpenPeriod_SetsStatusClosed()
    {
        var period = CreateOpenPeriod();
        var closedBy = Guid.NewGuid();
        var closedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

        period.Close(closedBy, closedAt);

        period.Status.Should().Be(PeriodStatus.Closed);
        period.ClosedByUserId.Should().Be(closedBy);
        period.ClosedAtUtc.Should().Be(closedAt);
    }

    [Fact]
    public void Close_AlreadyClosedPeriod_Throws()
    {
        var period = CreateOpenPeriod();
        period.Close(Guid.NewGuid(), DateTime.UtcNow);

        var act = () => period.Close(Guid.NewGuid(), DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reopen_ClosedPeriod_SetsStatusOpenAndClearsClosedInfo()
    {
        var period = CreateOpenPeriod();
        period.Close(Guid.NewGuid(), DateTime.UtcNow);

        period.Reopen();

        period.Status.Should().Be(PeriodStatus.Open);
        period.ClosedAtUtc.Should().BeNull();
        period.ClosedByUserId.Should().BeNull();
    }

    [Fact]
    public void Reopen_AlreadyOpenPeriod_Throws()
    {
        var period = CreateOpenPeriod();

        var act = period.Reopen;

        act.Should().Throw<InvalidOperationException>();
    }
}
