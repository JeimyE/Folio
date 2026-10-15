using System.Globalization;
using System.Windows.Data;

namespace Folio.Desktop.Converters;

/// <summary>Uppercases a dynamic string for display in a "versalita" café label
/// (see <c>Folio.TextStyles.xaml</c>'s <c>FolioVersalitasLabelTextStyle</c> remark —
/// WPF's <c>TextBlock</c> has no <c>CharacterCasing</c> property, unlike
/// <c>TextBox</c>, so this is the only way to uppercase bound text). Static
/// literals (e.g. "FOLIVORA SOFTWARE") are simply stored upper-case in
/// Strings.resx instead and don't need this.</summary>
[ValueConversion(typeof(string), typeof(string))]
public sealed class UpperCaseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string text ? text.ToUpper(culture) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
