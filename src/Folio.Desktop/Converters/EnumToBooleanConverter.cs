using System.Globalization;
using System.Windows.Data;

namespace Folio.Desktop.Converters;

/// <summary>The standard WPF enum↔bool converter, for binding a group of
/// <c>RadioButton</c>s to a single enum property (one tab per enum value): each
/// RadioButton's <c>IsChecked</c> binds to the same property with a different
/// <c>ConverterParameter</c> naming the enum value it represents — either the value
/// itself (<c>{x:Static vm:AccountTypeFilter.Asset}</c>, what the views use, so a
/// typo is a compile error) or its name as a string (<c>Asset</c>).</summary>
public sealed class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Enum && ToEnumValue(value.GetType(), parameter) is { } expected && value.Equals(expected);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true && ToEnumValue(targetType, parameter) is { } enumValue ? enumValue : Binding.DoNothing;

    private static object? ToEnumValue(Type enumType, object? parameter) => parameter switch
    {
        Enum enumValue when enumValue.GetType() == enumType => enumValue,
        string name => Enum.Parse(enumType, name),
        _ => null,
    };
}
