using System.Globalization;
using System.Windows.Data;
using Folio.Desktop.Resources;
using Folio.Domain.Enums;

namespace Folio.Desktop.Converters;

/// <summary>Renders a <see cref="NormalBalance"/> in es-CR (see <see cref="AccountTypeDisplayConverter"/>).</summary>
[ValueConversion(typeof(NormalBalance), typeof(string))]
public sealed class NormalBalanceDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        NormalBalance.Debit => Strings.NormalBalanceDebit,
        NormalBalance.Credit => Strings.NormalBalanceCredit,
        _ => value?.ToString() ?? string.Empty,
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
