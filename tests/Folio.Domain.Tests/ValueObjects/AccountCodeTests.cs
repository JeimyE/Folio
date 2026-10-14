using Folio.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Folio.Domain.Tests.ValueObjects;

public class AccountCodeTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("1.1")]
    [InlineData("1.1.01")]
    public void Constructor_ValidFormat_Succeeds(string value)
    {
        var code = new AccountCode(value);

        code.Value.Should().Be(value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("1.")]
    [InlineData(".1")]
    [InlineData("1..1")]
    [InlineData("a.1")]
    [InlineData("1-1")]
    public void Constructor_InvalidFormat_Throws(string value)
    {
        var act = () => new AccountCode(value);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parent_TopLevelCode_ReturnsNull()
    {
        var code = new AccountCode("1");

        code.Parent().Should().BeNull();
    }

    [Fact]
    public void Parent_NestedCode_ReturnsCodeOneLevelUp()
    {
        var code = new AccountCode("1.1.01");

        code.Parent().Should().Be(new AccountCode("1.1"));
    }

    [Fact]
    public void IsChildOf_DirectParent_ReturnsTrue()
    {
        var child = new AccountCode("1.1.01");
        var parent = new AccountCode("1.1");

        child.IsChildOf(parent).Should().BeTrue();
    }

    [Fact]
    public void IsChildOf_UnrelatedCode_ReturnsFalse()
    {
        var child = new AccountCode("1.1.01");
        var unrelated = new AccountCode("2.1");

        child.IsChildOf(unrelated).Should().BeFalse();
    }

    [Fact]
    public void CompareTo_DoubleDigitSegment_SortsAfterSingleDigitSegment_NotAlphabetically()
    {
        // A plain string compare would put "1.1.10" before "1.1.2" (since '1' < '2'
        // as characters). The numeric, segment-wise compare must not do that.
        var two = new AccountCode("1.1.2");
        var ten = new AccountCode("1.1.10");

        two.CompareTo(ten).Should().BeNegative();
        ten.CompareTo(two).Should().BePositive();
    }

    [Fact]
    public void CompareTo_ParentBeforeOwnChild_WhenSharedSegmentsAreEqual()
    {
        var parent = new AccountCode("1.1");
        var child = new AccountCode("1.1.01");

        parent.CompareTo(child).Should().BeNegative();
    }

    [Fact]
    public void CompareTo_EqualCodes_ReturnsZero()
    {
        new AccountCode("1.1.01").CompareTo(new AccountCode("1.1.01")).Should().Be(0);
    }

    [Fact]
    public void Sort_MixOfSingleAndDoubleDigitSegments_OrdersNumerically()
    {
        string[] unordered = ["1.1.10", "1.1.2", "1.2", "1.1.1", "1"];
        var codes = unordered
            .Select(c => new AccountCode(c))
            .OrderBy(c => c)
            .Select(c => c.Value);

        codes.Should().ContainInOrder("1", "1.1.1", "1.1.2", "1.1.10", "1.2");
    }
}
