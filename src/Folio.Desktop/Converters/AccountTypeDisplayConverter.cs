using System.Globalization;
using System.Windows.Data;
using Folio.Desktop.Resources;
using Folio.Domain.Enums;

namespace Folio.Desktop.Converters;

/// <summary>Renders an <see cref="AccountType"/> in es-CR for the account form's type
/// picker — the enum's own names are English identifiers (SPEC: English in code,
/// Spanish for anything the user sees).</summary>
[ValueConversion(typeof(AccountType), typeof(string))]
public sealed class AccountTypeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        AccountType.Asset => Strings.AccountTypeAsset,
        AccountType.Liability => Strings.AccountTypeLiability,
        AccountType.Equity => Strings.AccountTypeEquity,
        AccountType.Revenue => Strings.AccountTypeRevenue,
        AccountType.Cost => Strings.AccountTypeCost,
        AccountType.Expense => Strings.AccountTypeExpense,
        _ => value?.ToString() ?? string.Empty,
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
