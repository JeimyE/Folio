using System.Globalization;
using Folio.Desktop.Converters;
using Folio.Desktop.Resources;
using Folio.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Folio.Desktop.Tests.Converters;

public class UserRoleDisplayConverterTests
{
    private readonly UserRoleDisplayConverter _sut = new();

    public static TheoryData<UserRole, string> Roles => new()
    {
        { UserRole.Administrador, Strings.RoleAdministrador },
        { UserRole.Contador, Strings.RoleContador },
        { UserRole.Consulta, Strings.RoleConsulta },
    };

    [Theory]
    [MemberData(nameof(Roles))]
    public void Convert_EveryRole_ReturnsItsLocalizedName(UserRole role, string expected)
    {
        var result = _sut.Convert(role, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be(expected);
        expected.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Convert_Null_ReturnsEmptyString()
    {
        var result = _sut.Convert(null!, typeof(string), null!, CultureInfo.InvariantCulture);

        result.Should().Be(string.Empty);
    }
}
