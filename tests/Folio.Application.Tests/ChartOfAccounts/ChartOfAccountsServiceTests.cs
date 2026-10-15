using Folio.Application.Abstractions.Repositories;
using Folio.Application.ChartOfAccounts;
using Folio.Domain.Common;
using Folio.Domain.Entities;
using Folio.Domain.Enums;
using Folio.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Folio.Application.Tests.ChartOfAccounts;

public class ChartOfAccountsServiceTests
{
    private readonly IAccountRepository _repository = Substitute.For<IAccountRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ChartOfAccountsService _sut;

    public ChartOfAccountsServiceTests()
    {
        _clock.UtcNow.Returns(new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc));
        _sut = new ChartOfAccountsService(_repository, _clock);
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Account>)[]);
    }

    private static Account CreateGroupAccount(string code, AccountType type = AccountType.Asset) =>
        new(new AccountCode(code), $"Grupo {code}", type, isPostable: false);

    // --- CreateAsync ---

    [Fact]
    public async Task CreateAsync_ValidTopLevelAccount_Succeeds()
    {
        var request = new CreateAccountRequest("7", "Cuentas de orden", AccountType.Asset, true, null, null);

        var result = await _sut.CreateAsync(request);

        result.Succeeded.Should().BeTrue();
        await _repository.Received(1).AddAsync(Arg.Is<Account>(a => a.Code.Value == "7"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_InvalidCodeFormat_Fails()
    {
        var request = new CreateAccountRequest("abc", "Inválida", AccountType.Asset, true, null, null);

        var result = await _sut.CreateAsync(request);

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("código válido");
    }

    [Fact]
    public async Task CreateAsync_DuplicateCode_Fails()
    {
        _repository.CodeExistsAsync("1.1.07", Arg.Any<CancellationToken>()).Returns(true);
        var request = new CreateAccountRequest("1.1.07", "Repetida", AccountType.Asset, true, null, null);

        var result = await _sut.CreateAsync(request);

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Ya existe una cuenta");
    }

    [Fact]
    public async Task CreateAsync_ParentDoesNotExist_Fails()
    {
        _repository.GetByCodeAsync("1.1", Arg.Any<CancellationToken>()).Returns((Account?)null);
        var request = new CreateAccountRequest("1.1.07", "Sin padre", AccountType.Asset, true, null, null);

        var result = await _sut.CreateAsync(request);

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No existe una cuenta con el código padre");
    }

    [Fact]
    public async Task CreateAsync_TypeDoesNotMatchParent_Fails()
    {
        var parent = CreateGroupAccount("1.1", AccountType.Asset);
        _repository.GetByCodeAsync("1.1", Arg.Any<CancellationToken>()).Returns(parent);
        var request = new CreateAccountRequest("1.1.07", "Tipo distinto", AccountType.Liability, true, null, null);

        var result = await _sut.CreateAsync(request);

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("no coincide");
    }

    [Fact]
    public async Task CreateAsync_ParentIsPostable_Fails()
    {
        var parent = new Account(new AccountCode("1.1"), "Postable padre", AccountType.Asset, isPostable: true);
        _repository.GetByCodeAsync("1.1", Arg.Any<CancellationToken>()).Returns(parent);
        var request = new CreateAccountRequest("1.1.07", "Hija", AccountType.Asset, true, null, null);

        var result = await _sut.CreateAsync(request);

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("es postable");
    }

    [Fact]
    public async Task CreateAsync_UnderValidParent_MarksParentAsHavingChildren()
    {
        var parent = CreateGroupAccount("1.1", AccountType.Asset);
        _repository.GetByCodeAsync("1.1", Arg.Any<CancellationToken>()).Returns(parent);
        var request = new CreateAccountRequest("1.1.07", "Nueva hoja", AccountType.Asset, true, null, null);

        var result = await _sut.CreateAsync(request);

        result.Succeeded.Should().BeTrue();
        parent.HasChildren.Should().BeTrue();
        await _repository.Received(1).UpdateAsync(parent, Arg.Any<CancellationToken>());
    }

    // --- UpdateAsync ---

    [Fact]
    public async Task UpdateAsync_ChangeTypeWithMovements_Fails()
    {
        var account = new Account(new AccountCode("4.1"), "Ventas", AccountType.Revenue, isPostable: true);
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        _repository.HasMovementsAsync(account.Id, Arg.Any<CancellationToken>()).Returns(true);
        var request = new UpdateAccountRequest(account.Id, "Ventas", AccountType.Liability, true, account.NormalBalance, null);

        var result = await _sut.UpdateAsync(request);

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cambiar el tipo");
    }

    [Fact]
    public async Task UpdateAsync_ChangeTypeWithoutMovements_Succeeds()
    {
        var account = new Account(new AccountCode("4.4"), "Otros ingresos", AccountType.Revenue, isPostable: true);
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        _repository.HasMovementsAsync(account.Id, Arg.Any<CancellationToken>()).Returns(false);
        var request = new UpdateAccountRequest(account.Id, "Otros ingresos", AccountType.Asset, true, account.NormalBalance, null);

        var result = await _sut.UpdateAsync(request);

        result.Succeeded.Should().BeTrue();
        account.Type.Should().Be(AccountType.Asset);
    }

    [Fact]
    public async Task UpdateAsync_TurnOffPostableWithMovements_Fails()
    {
        var account = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true);
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        _repository.HasMovementsAsync(account.Id, Arg.Any<CancellationToken>()).Returns(true);
        var request = new UpdateAccountRequest(account.Id, "Caja", AccountType.Asset, false, account.NormalBalance, null);

        var result = await _sut.UpdateAsync(request);

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cuenta de agrupación");
    }

    [Fact]
    public async Task UpdateAsync_MarkPostableAccountWithChildren_Fails()
    {
        var account = CreateGroupAccount("1.1");
        account.AddChild();
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        _repository.HasMovementsAsync(account.Id, Arg.Any<CancellationToken>()).Returns(false);
        var request = new UpdateAccountRequest(account.Id, account.Name, account.Type, true, account.NormalBalance, null);

        var result = await _sut.UpdateAsync(request);

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("subcuentas");
    }

    // --- DeactivateAsync / ActivateAsync ---

    [Fact]
    public async Task DeactivateAsync_GroupAccountWithActiveChildren_Fails()
    {
        var parent = CreateGroupAccount("1.1");
        parent.AddChild();
        var activeChild = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true, parent.Id);
        _repository.GetByIdAsync(parent.Id, Arg.Any<CancellationToken>()).Returns(parent);
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Account>)[parent, activeChild]);

        var result = await _sut.DeactivateAsync(parent.Id);

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Be("Desactive primero las subcuentas.");
    }

    [Fact]
    public async Task DeactivateAsync_GroupAccountWithOnlyInactiveChildren_Succeeds()
    {
        var parent = CreateGroupAccount("1.1");
        parent.AddChild();
        var inactiveChild = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true, parent.Id);
        inactiveChild.Deactivate();
        _repository.GetByIdAsync(parent.Id, Arg.Any<CancellationToken>()).Returns(parent);
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Account>)[parent, inactiveChild]);

        var result = await _sut.DeactivateAsync(parent.Id);

        result.Succeeded.Should().BeTrue();
        parent.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateAsync_LeafAccount_Succeeds()
    {
        var account = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true);
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var result = await _sut.DeactivateAsync(account.Id);

        result.Succeeded.Should().BeTrue();
        account.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ActivateAsync_ParentIsInactive_Fails()
    {
        var parent = CreateGroupAccount("1.1");
        parent.Deactivate();
        var child = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true, parent.Id);
        child.Deactivate();
        _repository.GetByIdAsync(child.Id, Arg.Any<CancellationToken>()).Returns(child);
        _repository.GetByIdAsync(parent.Id, Arg.Any<CancellationToken>()).Returns(parent);

        var result = await _sut.ActivateAsync(child.Id);

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Be("No se puede activar una cuenta cuyo padre está inactivo.");
    }

    [Fact]
    public async Task ActivateAsync_ParentIsActive_Succeeds()
    {
        var parent = CreateGroupAccount("1.1");
        var child = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true, parent.Id);
        child.Deactivate();
        _repository.GetByIdAsync(child.Id, Arg.Any<CancellationToken>()).Returns(child);
        _repository.GetByIdAsync(parent.Id, Arg.Any<CancellationToken>()).Returns(parent);

        var result = await _sut.ActivateAsync(child.Id);

        result.Succeeded.Should().BeTrue();
        child.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ActivateAsync_NoParent_Succeeds()
    {
        var account = new Account(new AccountCode("7"), "Cuentas de orden", AccountType.Asset, isPostable: true);
        account.Deactivate();
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var result = await _sut.ActivateAsync(account.Id);

        result.Succeeded.Should().BeTrue();
    }

    // --- DeleteAsync ---

    [Fact]
    public async Task DeleteAsync_AccountWithMovements_Fails()
    {
        var account = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true);
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        _repository.HasMovementsAsync(account.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.DeleteAsync(account.Id);

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("desactívela");
    }

    [Fact]
    public async Task DeleteAsync_AccountWithChildren_Fails()
    {
        var account = CreateGroupAccount("1.1");
        account.AddChild();
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var result = await _sut.DeleteAsync(account.Id);

        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("subcuentas");
    }

    [Fact]
    public async Task DeleteAsync_NoMovementsNoChildren_Succeeds()
    {
        var account = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true);
        _repository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);
        _repository.HasMovementsAsync(account.Id, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.DeleteAsync(account.Id);

        result.Succeeded.Should().BeTrue();
        await _repository.Received(1).DeleteAsync(account, Arg.Any<CancellationToken>());
    }

    // --- GetDetailAsync: six-month window ---

    [Fact]
    public async Task GetDetailAsync_UtcIsAlreadyNextMonth_WindowEndsAtTheCostaRicaMonth()
    {
        // 03:00 UTC on July 1 is still 21:00 on June 30 in Costa Rica.
        _clock.UtcNow.Returns(new DateTime(2026, 7, 1, 3, 0, 0, DateTimeKind.Utc));
        var caja = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true);
        _repository.GetByIdAsync(caja.Id, Arg.Any<CancellationToken>()).Returns(caja);
        _repository.GetMovementTotalsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>()).Returns((0m, 0m));
        _repository.GetMonthlyMovementTotalsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<MonthlyMovementTotals>)[]);
        _repository.GetRecentEntriesAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<RecentAccountEntry>)[]);

        var detail = await _sut.GetDetailAsync(caja.Id);

        detail!.MonthlyBalances.Select(m => (m.Year, m.Month)).Should().Equal(
            (2026, 1), (2026, 2), (2026, 3), (2026, 4), (2026, 5), (2026, 6));
        await _repository.Received(1).GetMonthlyMovementTotalsAsync(
            Arg.Any<IEnumerable<Guid>>(), new DateOnly(2026, 6, 30), Arg.Any<CancellationToken>());
    }

    // --- GetBalanceCheckAsync ---

    [Fact]
    public async Task GetBalanceCheckAsync_NoAccounts_ReturnsZeroTotalsAndIsBalanced()
    {
        var result = await _sut.GetBalanceCheckAsync();

        result.AssetTotal.Should().Be(0m);
        result.LiabilityAndEquityTotal.Should().Be(0m);
        result.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public async Task GetBalanceCheckAsync_SeparatesAssetFromLiabilityAndEquity_ExcludingNonPostableAccounts()
    {
        var caja = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true);
        var cuentasPorPagar = new Account(new AccountCode("2.1.01"), "Cuentas por pagar", AccountType.Liability, isPostable: true);
        var capital = new Account(new AccountCode("3.1"), "Capital social", AccountType.Equity, isPostable: true);
        var activoGroup = CreateGroupAccount("1", AccountType.Asset); // non-postable: must be excluded

        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Account>)[caja, cuentasPorPagar, capital, activoGroup]);

        _repository.GetMovementTotalsAsync(
                Arg.Is<IEnumerable<Guid>>(ids => ids.ToHashSet().SetEquals(new[] { caja.Id })),
                Arg.Any<CancellationToken>())
            .Returns((1000m, 400m));
        _repository.GetMovementTotalsAsync(
                Arg.Is<IEnumerable<Guid>>(ids => ids.ToHashSet().SetEquals(new[] { cuentasPorPagar.Id, capital.Id })),
                Arg.Any<CancellationToken>())
            .Returns((100m, 700m));

        var result = await _sut.GetBalanceCheckAsync();

        result.AssetTotal.Should().Be(600m); // 1000 debit - 400 credit
        result.LiabilityAndEquityTotal.Should().Be(600m); // 700 credit - 100 debit
        result.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public async Task GetBalanceCheckAsync_UnclosedRevenueAndExpense_CountAsPartOfEquity()
    {
        var caja = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true);
        var capital = new Account(new AccountCode("3.1"), "Capital social", AccountType.Equity, isPostable: true);
        var ventas = new Account(new AccountCode("4.1"), "Ventas", AccountType.Revenue, isPostable: true);
        var alquiler = new Account(new AccountCode("6.1"), "Alquiler", AccountType.Expense, isPostable: true);

        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Account>)[caja, capital, ventas, alquiler]);

        // Capital 1000 + sales 500 - rent 200 = 1300 in Caja. Without the period's
        // result on the right-hand side, the check would compare 1300 vs. 1000.
        _repository.GetMovementTotalsAsync(
                Arg.Is<IEnumerable<Guid>>(ids => ids.ToHashSet().SetEquals(new[] { caja.Id })),
                Arg.Any<CancellationToken>())
            .Returns((1500m, 200m));
        _repository.GetMovementTotalsAsync(
                Arg.Is<IEnumerable<Guid>>(ids => ids.ToHashSet().SetEquals(new[] { capital.Id, ventas.Id, alquiler.Id })),
                Arg.Any<CancellationToken>())
            .Returns((200m, 1500m));

        var result = await _sut.GetBalanceCheckAsync();

        result.AssetTotal.Should().Be(1300m);
        result.LiabilityAndEquityTotal.Should().Be(1300m);
        result.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public async Task GetBalanceCheckAsync_UnequalSides_IsNotBalanced()
    {
        var caja = new Account(new AccountCode("1.1.01"), "Caja", AccountType.Asset, isPostable: true);
        var cuentasPorPagar = new Account(new AccountCode("2.1.01"), "Cuentas por pagar", AccountType.Liability, isPostable: true);

        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Account>)[caja, cuentasPorPagar]);
        _repository.GetMovementTotalsAsync(
                Arg.Is<IEnumerable<Guid>>(ids => ids.ToHashSet().SetEquals(new[] { caja.Id })),
                Arg.Any<CancellationToken>())
            .Returns((500m, 0m));
        _repository.GetMovementTotalsAsync(
                Arg.Is<IEnumerable<Guid>>(ids => ids.ToHashSet().SetEquals(new[] { cuentasPorPagar.Id })),
                Arg.Any<CancellationToken>())
            .Returns((0m, 300m));

        var result = await _sut.GetBalanceCheckAsync();

        result.IsBalanced.Should().BeFalse();
    }
}
