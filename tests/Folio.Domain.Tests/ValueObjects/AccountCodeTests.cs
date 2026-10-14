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
}
