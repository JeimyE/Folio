using System.Globalization;
using System.Windows.Data;
using Folio.Desktop.Resources;
using Folio.Domain.Enums;

namespace Folio.Desktop.Converters;

/// <summary>Renders a <see cref="UserRole"/> in es-CR for the sidebar footer, under
/// the signed-in user's name. The enum names happen to be Spanish already, but they
/// are identifiers, not display text — the inclusive "/a" forms live in Strings.resx
/// like every other user-facing string.</summary>
[ValueConversion(typeof(UserRole), typeof(string))]
public sealed class UserRoleDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        UserRole.Administrador => Strings.RoleAdministrador,
        UserRole.Contador => Strings.RoleContador,
        UserRole.Consulta => Strings.RoleConsulta,
        _ => value?.ToString() ?? string.Empty,
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
