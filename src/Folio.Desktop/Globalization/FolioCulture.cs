using System.Globalization;
using System.Windows;
using System.Windows.Markup;

namespace Folio.Desktop.Globalization;

/// <summary>
/// Makes every number, date, and currency the app renders use es-CR — both in C#
/// (<see cref="CultureInfo.CurrentCulture"/>) and in XAML bindings, which do **not**
/// follow the thread's culture at all: a <c>Binding</c>'s <c>StringFormat</c> (and any
/// other culture-sensitive formatting WPF does) is resolved through
/// <see cref="FrameworkElement.Language"/>, an <see cref="XmlLanguage"/> that defaults
/// to "en-US" regardless of <see cref="CultureInfo.CurrentCulture"/> — this is why a
/// `{Binding Balance, StringFormat=C2}` rendered as "$1,234.56" even though
/// <c>CultureInfo.DefaultThreadCurrentCulture</c> was already set to es-CR.
/// </summary>
public static class FolioCulture
{
    private const string CultureName = "es-CR";
    private static readonly object Lock = new();
    private static bool _applied;

    /// <summary>Idempotent, and must run before any <see cref="FrameworkElement"/> is
    /// constructed — overriding a dependency property's default metadata only affects
    /// elements created afterward.</summary>
    public static void Apply()
    {
        lock (Lock)
        {
            if (_applied)
            {
                return;
            }

            var culture = CultureInfo.GetCultureInfo(CultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

            _applied = true;
        }
    }
}
