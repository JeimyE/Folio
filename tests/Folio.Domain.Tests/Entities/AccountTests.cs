using Folio.Domain.Entities;
using Folio.Domain.Enums;
using Folio.Domain.Exceptions;
using Folio.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Folio.Domain.Tests.Entities;

public class AccountTests
{
    [Theory]
    [InlineData(AccountType.Asset, NormalBalance.Debit)]
    [InlineData(AccountType.Cost, NormalBalance.Debit)]
    [InlineData(AccountType.Expense, NormalBalance.Debit)]
    [InlineData(AccountType.Liability, NormalBalance.Credit)]
    [InlineData(AccountType.Equity, NormalBalance.Credit)]
    [InlineData(AccountType.Revenue, NormalBalance.Credit)]
    public void Constructor_NoOverride_DerivesNormalBalanceFromType(AccountType type, NormalBalance expected)
    {
        var account = new Account(new AccountCode("1"), "Test", type, isPostable: true);

        account.NormalBalance.Should().Be(expected);
    }

    [Fact]
    public void Constructor_WithOverride_UsesOverride()
    {
        // Depreciación acumulada: an Asset-type contra-account with a Credit normal balance.
        var account = new Account(
            new AccountCode("1.2.04"),
            "Depreciación acumulada",
            AccountType.Asset,
            isPostable: true,
            normalBalanceOverride: NormalBalance.Credit);

        account.NormalBalance.Should().Be(NormalBalance.Credit);
    }

    [Fact]
    public void Constructor_BlankName_Throws()
    {
        var act = () => new Account(new AccountCode("1"), "   ", AccountType.Asset, isPostable: true);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NewAccount_IsActive()
    {
        var account = new Account(new AccountCode("1"), "Caja", AccountType.Asset, isPostable: true);

        account.IsActive.Should().BeTrue();
    }

    [Fact]
    public void AddChild_PostableAccount_Throws()
    {
        var account = new Account(new AccountCode("1.1"), "Activo corriente", AccountType.Asset, isPostable: true);

        var act = account.AddChild;

        act.Should().Throw<AccountWithChildrenCannotBePostableException>();
    }

    [Fact]
    public void AddChild_NonPostableAccount_MarksHasChildren()
    {
        var account = new Account(new AccountCode("1.1"), "Activo corriente", AccountType.Asset, isPostable: false);

        account.AddChild();

        account.HasChildren.Should().BeTrue();
    }

    [Fact]
    public void MarkAsPostable_AccountWithChildren_Throws()
    {
        var account = new Account(new AccountCode("1.1"), "Activo corriente", AccountType.Asset, isPostable: false);
        account.AddChild();

        var act = account.MarkAsPostable;

        act.Should().Throw<AccountWithChildrenCannotBePostableException>();
    }

    [Fact]
    public void MarkAsPostable_AccountWithoutChildren_Succeeds()
    {
        var account = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: false);

        account.MarkAsPostable();

        account.IsPostable.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_ActiveAccount_SetsIsActiveFalse()
    {
        var account = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true);

        account.Deactivate();

        account.IsActive.Should().BeFalse();
    }

    [Fact]
    public void ChangeType_NoOverride_DerivesNormalBalanceFromNewType()
    {
        var account = new Account(new AccountCode("4.1"), "Ventas", AccountType.Revenue, isPostable: true);

        account.ChangeType(AccountType.Liability);

        account.Type.Should().Be(AccountType.Liability);
        account.NormalBalance.Should().Be(NormalBalance.Credit);
    }

    [Fact]
    public void ChangeType_WithOverride_UsesOverride()
    {
        var account = new Account(new AccountCode("1.2.04"), "Depreciación acumulada", AccountType.Asset, isPostable: true);

        account.ChangeType(AccountType.Asset, NormalBalance.Credit);

        account.NormalBalance.Should().Be(NormalBalance.Credit);
    }
}
