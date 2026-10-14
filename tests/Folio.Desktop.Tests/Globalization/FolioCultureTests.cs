using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using Folio.Desktop.Globalization;
using FluentAssertions;
using Xunit;

namespace Folio.Desktop.Tests.Globalization;

public class FolioCultureTests
{
    public FolioCultureTests() => FolioCulture.Apply();

    [Fact]
    public void Apply_SetsCurrentCultureToCostaRicanSpanish() =>
        CultureInfo.CurrentCulture.Name.Should().Be("es-CR");

    [Fact]
    public void Apply_FormatsAmountsAsCostaRicanColones()
    {
        // The bug this guards against: WPF bindings don't follow CultureInfo at all
        // (see FolioCulture's remarks), so this alone wouldn't have caught it — the
        // next test asserts the actual mechanism bindings use. The group separator
        // isn't built from a literal space: es-CR's is U+00A0 (no-break space), not
        // U+0020, and a plain literal would silently test the wrong character.
        var format = CultureInfo.CurrentCulture.NumberFormat;
        var expected = $"₡1{format.CurrencyGroupSeparator}479{format.CurrencyGroupSeparator}862{format.CurrencyDecimalSeparator}69";

        1_479_862.69m.ToString("C2", CultureInfo.CurrentCulture).Should().Be(expected);
    }

    [Fact]
    public void Apply_OverridesFrameworkElementLanguageDefault_SoXamlBindingsFormatInEsCrToo()
    {
        var metadata = FrameworkElement.LanguageProperty.GetMetadata(typeof(FrameworkElement));

        metadata.DefaultValue.Should().Be(XmlLanguage.GetLanguage("es-CR"));
    }

    [Fact]
    public void Apply_CalledMoreThanOnce_DoesNotThrow()
    {
        var act = FolioCulture.Apply;

        act.Should().NotThrow();
    }
}
