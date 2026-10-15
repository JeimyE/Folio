using System.Globalization;
using Folio.Domain.Common;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Folio.Domain.Tests.Common;

public class ClockExtensionsTests
{
    [Theory]
    // 22:47 on Oct 14 in Costa Rica is already Oct 15 in UTC.
    [InlineData("2026-10-15T04:47:00Z", "2026-10-14")]
    // Exactly local midnight (UTC-6): the new day has started in Costa Rica too.
    [InlineData("2026-10-15T06:00:00Z", "2026-10-15")]
    // Month boundary: UTC says July, Costa Rica is still on the last day of June.
    [InlineData("2026-07-01T03:00:00Z", "2026-06-30")]
    // Year boundary.
    [InlineData("2027-01-01T05:59:59Z", "2026-12-31")]
    public void TodayInCostaRica_UtcInstant_ReturnsCostaRicaCalendarDate(string utcInstant, string expectedDate)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTime.Parse(utcInstant, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));

        clock.TodayInCostaRica().Should().Be(DateOnly.Parse(expectedDate, CultureInfo.InvariantCulture));
    }
}
