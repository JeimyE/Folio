using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Folio.Desktop.Converters;

/// <summary>
/// Collapses an element when its bound value is <c>null</c> or an empty string (e.g.
/// no account selected, no error to show). Pass <c>Invert</c> as the converter
/// parameter to show the element only when the value is empty instead — e.g. a "no
/// selection" hint shown exactly when the detail panel it sits next to is not.
/// </summary>
[ValueConversion(typeof(object), typeof(Visibility))]
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isEmpty = value is null || (value is string text && string.IsNullOrEmpty(text));
        var invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
        var visible = invert ? isEmpty : !isEmpty;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
