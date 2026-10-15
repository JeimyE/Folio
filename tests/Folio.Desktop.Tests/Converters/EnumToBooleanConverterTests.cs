using System.Globalization;
using Folio.Desktop.Converters;
using Folio.Desktop.ViewModels;
using FluentAssertions;
using Xunit;

namespace Folio.Desktop.Tests.Converters;

public class EnumToBooleanConverterTests
{
    private readonly EnumToBooleanConverter _sut = new();

    [Fact]
    public void Convert_MatchingEnumValue_ReturnsTrue()
    {
        var result = _sut.Convert(AccountTypeFilter.Asset, typeof(bool), "Asset", CultureInfo.InvariantCulture);

        result.Should().Be(true);
    }

    [Fact]
    public void Convert_NonMatchingEnumValue_ReturnsFalse()
    {
        var result = _sut.Convert(AccountTypeFilter.Asset, typeof(bool), "Liability", CultureInfo.InvariantCulture);

        result.Should().Be(false);
    }

    [Fact]
    public void ConvertBack_True_ReturnsTheParsedEnumValueNamedByTheParameter()
    {
        var result = _sut.ConvertBack(true, typeof(AccountTypeFilter), "CostOrExpense", CultureInfo.InvariantCulture);

        result.Should().Be(AccountTypeFilter.CostOrExpense);
    }

    [Fact]
    public void ConvertBack_False_DoesNothing()
    {
        var result = _sut.ConvertBack(false, typeof(AccountTypeFilter), "Asset", CultureInfo.InvariantCulture);

        result.Should().Be(System.Windows.Data.Binding.DoNothing);
    }

    // ChartOfAccountsView passes ConverterParameter={x:Static vm:AccountTypeFilter.X},
    // i.e. the boxed enum value itself, not its name as a string. These two tests
    // reproduce exactly what the tabs send; before the fix the converter only
    // accepted strings, so every tab read as unchecked and a click never reached
    // SelectedTypeFilter (the tree was never filtered).
    [Fact]
    public void Convert_MatchingEnumValueParameter_ReturnsTrue()
    {
        var result = _sut.Convert(AccountTypeFilter.Asset, typeof(bool), AccountTypeFilter.Asset, CultureInfo.InvariantCulture);

        result.Should().Be(true);
    }

    [Fact]
    public void ConvertBack_TrueWithEnumValueParameter_ReturnsThatEnumValue()
    {
        var result = _sut.ConvertBack(true, typeof(AccountTypeFilter), AccountTypeFilter.Liability, CultureInfo.InvariantCulture);

        result.Should().Be(AccountTypeFilter.Liability);
    }
}
