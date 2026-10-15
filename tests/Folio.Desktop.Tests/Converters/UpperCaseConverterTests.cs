using System.Globalization;
using Folio.Desktop.Converters;
using FluentAssertions;
using Xunit;

namespace Folio.Desktop.Tests.Converters;

public class UpperCaseConverterTests
{
    private readonly UpperCaseConverter _sut = new();

    [Fact]
    public void Convert_LowercaseString_ReturnsUppercase()
    {
        var result = _sut.Convert("Sleepy Sloth Café & Bakery S.A.", typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be("SLEEPY SLOTH CAFÉ & BAKERY S.A.");
    }

    [Fact]
    public void Convert_Null_ReturnsEmptyString()
    {
        var result = _sut.Convert(null!, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be(string.Empty);
    }
}
