using Folio.Application.ChartOfAccounts;
using Folio.Desktop.Dialogs;
using Folio.Desktop.ViewModels;
using Folio.Domain.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Folio.Desktop.Tests.ViewModels;

public class ChartOfAccountsViewModelTests
{
    private readonly IChartOfAccountsService _chartOfAccountsService = Substitute.For<IChartOfAccountsService>();
    private readonly IDialogService _dialogService = Substitute.For<IDialogService>();
    private readonly ChartOfAccountsViewModel _sut;

    public ChartOfAccountsViewModelTests()
    {
        _sut = new ChartOfAccountsViewModel(_chartOfAccountsService, _dialogService);
    }

    private static AccountTreeItemDto Node(
        Guid id, string code, string name, bool isPostable, bool isActive = true, params AccountTreeItemDto[] children) => new()
    {
        Id = id,
        Code = code,
        Name = name,
        IsPostable = isPostable,
        IsActive = isActive,
        HasChildren = children.Length > 0,
        Children = children,
    };

    /// <summary>Makes the dialog mock behave like a real dialog that actually ran the
    /// form's save — rather than just returning a canned true/false — so these tests
    /// exercise the real <see cref="AccountFormViewModel.SaveAsync"/> the ViewModel
    /// handed it, the same way a successful user interaction with the real dialog
    /// would. This is the closest these tests can get to the real
    /// <c>DialogService</c>/<c>ContentDialog</c> flow (ADR-0011/ADR-0013) without a
    /// live WPF window — the recursive-close bug those ADRs describe lives one layer
    /// below <c>IDialogService</c>, which is mocked here, so it isn't reproduced by
    /// this test; what these tests verify is the ViewModel's own contract: once a
    /// save genuinely succeeds, the tree reloads and the affected account stays
    /// selected.</summary>
    private void StubDialogServiceToReallyRunTheFormsSave() =>
        _dialogService.ShowAccountFormAsync(Arg.Any<AccountFormViewModel>(), Arg.Any<string>())
            .Returns(callInfo =>
            {
                // Nothing plays the part of the user typing into the form, and
                // AccountFormViewModel.SaveAsync() refuses to save a blank name —
                // fill it in, same as InitializeForEdit already does for the name
                // the account already had.
                var formViewModel = callInfo.Arg<AccountFormViewModel>();
                if (string.IsNullOrWhiteSpace(formViewModel.Name))
                {
                    formViewModel.Name = "Cuenta de prueba";
                }

                return formViewModel.SaveAsync();
            });

    [Fact]
    public async Task InitializeAsync_PopulatesRootNodesFromTheTree()
    {
        var rootId = Guid.NewGuid();
        var tree = new[] { Node(rootId, "1", "Activo", isPostable: false) };
        _chartOfAccountsService.GetTreeAsync(Arg.Any<CancellationToken>()).Returns(tree);

        await _sut.InitializeAsync();

        _sut.RootNodes.Should().ContainSingle(n => n.Id == rootId && n.Code == "1" && n.Name == "Activo");
    }

    [Fact]
    public async Task OnSearchTextChanged_MatchingDescendant_KeepsAncestorVisibleAndExpandedWithoutMarkingItAsAMatch()
    {
        var cajaId = Guid.NewGuid();
        var rootId = Guid.NewGuid();
        var caja = Node(cajaId, "1.1", "Caja", isPostable: true);
        var root = Node(rootId, "1", "Activo", false, true, caja);
        _chartOfAccountsService.GetTreeAsync(Arg.Any<CancellationToken>()).Returns(new[] { root });
        await _sut.InitializeAsync();

        _sut.SearchText = "caja";

        var rootNode = _sut.RootNodes.Single();
        var cajaNode = rootNode.Children.Single();

        cajaNode.IsSearchMatch.Should().BeTrue();
        cajaNode.IsVisible.Should().BeTrue();
        rootNode.IsSearchMatch.Should().BeFalse("the root's own code/name don't contain the search text");
        rootNode.IsVisible.Should().BeTrue("it has to stay reachable for its matching child to be visible");
        rootNode.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public async Task OnSearchTextChanged_MatchesByCodeCaseInsensitively()
    {
        var accountId = Guid.NewGuid();
        var tree = new[] { Node(accountId, "1.1.01", "Bancos Colones", isPostable: true) };
        _chartOfAccountsService.GetTreeAsync(Arg.Any<CancellationToken>()).Returns(tree);
        await _sut.InitializeAsync();

        _sut.SearchText = "BANCOS";

        _sut.RootNodes.Single().IsSearchMatch.Should().BeTrue();
    }

    [Fact]
    public async Task OnSearchTextChanged_UnrelatedBranch_StaysHidden()
    {
        var matchingId = Guid.NewGuid();
        var unrelatedId = Guid.NewGuid();
        var tree = new[]
        {
            Node(matchingId, "1", "Caja", isPostable: true),
            Node(unrelatedId, "4", "Ingresos", isPostable: false),
        };
        _chartOfAccountsService.GetTreeAsync(Arg.Any<CancellationToken>()).Returns(tree);
        await _sut.InitializeAsync();

        _sut.SearchText = "caja";

        _sut.RootNodes.First(n => n.Id == unrelatedId).IsVisible.Should().BeFalse();
    }

    [Fact]
    public async Task OnSearchTextChanged_ClearedToEmpty_EverythingIsVisibleAndNothingIsAMatch()
    {
        var accountId = Guid.NewGuid();
        var tree = new[] { Node(accountId, "1", "Activo", isPostable: false) };
        _chartOfAccountsService.GetTreeAsync(Arg.Any<CancellationToken>()).Returns(tree);
        await _sut.InitializeAsync();
        _sut.SearchText = "activo";

        _sut.SearchText = string.Empty;

        var node = _sut.RootNodes.Single();
        node.IsVisible.Should().BeTrue();
        node.IsSearchMatch.Should().BeFalse();
    }

    [Fact]
    public async Task OnSelectedNodeChanged_SetToNode_LoadsDetailFromTheService()
    {
        var accountId = Guid.NewGuid();
        var tree = new[] { Node(accountId, "1", "Activo", isPostable: false) };
        _chartOfAccountsService.GetTreeAsync(Arg.Any<CancellationToken>()).Returns(tree);
        await _sut.InitializeAsync();

        var detail = new AccountDetailDto { Id = accountId, Code = "1", Name = "Activo", Type = AccountType.Asset, NormalBalance = NormalBalance.Debit };
        _chartOfAccountsService.GetDetailAsync(accountId, Arg.Any<CancellationToken>()).Returns(detail);

        _sut.SelectedNode = _sut.RootNodes.Single();

        _sut.SelectedDetail.Should().Be(detail);
    }

    [Fact]
    public async Task OnSelectedNodeChanged_SetToNull_ClearsDetail()
    {
        var accountId = Guid.NewGuid();
        var tree = new[] { Node(accountId, "1", "Activo", isPostable: false) };
        _chartOfAccountsService.GetTreeAsync(Arg.Any<CancellationToken>()).Returns(tree);
        await _sut.InitializeAsync();
        _chartOfAccountsService.GetDetailAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(new AccountDetailDto { Id = accountId });
        _sut.SelectedNode = _sut.RootNodes.Single();

        _sut.SelectedNode = null;

        _sut.SelectedDetail.Should().BeNull();
    }

    [Fact]
    public void AddChildAccountCommand_NoSelection_CannotExecute() =>
        _sut.AddChildAccountCommand.CanExecute(null).Should().BeFalse();

    [Fact]
    public async Task AddChildAccountCommand_WithSelection_CanExecute()
    {
        var accountId = Guid.NewGuid();
        var tree = new[] { Node(accountId, "1", "Activo", isPostable: false) };
        _chartOfAccountsService.GetTreeAsync(Arg.Any<CancellationToken>()).Returns(tree);
        await _sut.InitializeAsync();

        _sut.SelectedNode = _sut.RootNodes.Single();

        _sut.AddChildAccountCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void EditAccountCommand_ToggleActiveCommand_DeleteAccountCommand_NoSelection_CannotExecute()
    {
        _sut.EditAccountCommand.CanExecute(null).Should().BeFalse();
        _sut.ToggleActiveCommand.CanExecute(null).Should().BeFalse();
        _sut.DeleteAccountCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task AddRootAccountCommand_DialogSaved_ReloadsTreeAndSelectsTheNewAccount()
    {
        // Regression test for the bug where a successful create saved to the
        // database but the tree never refreshed until the app restarted (ADR-0013):
        // the dialog's own async-close recursion meant the ViewModel never learned
        // the save had finished, so LoadTreeAsync never ran.
        var newAccountId = Guid.NewGuid();
        _chartOfAccountsService.CreateAsync(Arg.Any<CreateAccountRequest>(), Arg.Any<CancellationToken>())
            .Returns(ChartOfAccountsResult.Success(newAccountId));
        _chartOfAccountsService.GetTreeAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { Node(newAccountId, "6.99", "Otros gastos", isPostable: true) });
        StubDialogServiceToReallyRunTheFormsSave();

        await _sut.AddRootAccountCommand.ExecuteAsync(null);

        await _chartOfAccountsService.Received(1).GetTreeAsync(Arg.Any<CancellationToken>());
        _sut.SelectedNode.Should().NotBeNull();
        _sut.SelectedNode!.Id.Should().Be(newAccountId);
    }

    [Fact]
    public async Task AddRootAccountCommand_DialogCancelled_DoesNotReloadTheTree()
    {
        _dialogService.ShowAccountFormAsync(Arg.Any<AccountFormViewModel>(), Arg.Any<string>()).Returns(false);

        await _sut.AddRootAccountCommand.ExecuteAsync(null);

        await _chartOfAccountsService.DidNotReceive().GetTreeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAccountCommand_UserDoesNotConfirm_NeverCallsDeleteAsync()
    {
        var accountId = Guid.NewGuid();
        var tree = new[] { Node(accountId, "1", "Activo", isPostable: false) };
        _chartOfAccountsService.GetTreeAsync(Arg.Any<CancellationToken>()).Returns(tree);
        await _sut.InitializeAsync();
        _sut.SelectedNode = _sut.RootNodes.Single();
        _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await _sut.DeleteAccountCommand.ExecuteAsync(null);

        await _chartOfAccountsService.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAccountCommand_ServiceFails_SetsErrorMessageAndKeepsSelection()
    {
        var accountId = Guid.NewGuid();
        var tree = new[] { Node(accountId, "1", "Activo", isPostable: false) };
        _chartOfAccountsService.GetTreeAsync(Arg.Any<CancellationToken>()).Returns(tree);
        await _sut.InitializeAsync();
        _sut.SelectedNode = _sut.RootNodes.Single();
        _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _chartOfAccountsService.DeleteAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(ChartOfAccountsResult.Failure("No se puede eliminar una cuenta con movimientos; desactívela en su lugar."));

        await _sut.DeleteAccountCommand.ExecuteAsync(null);

        _sut.ErrorMessage.Should().Be("No se puede eliminar una cuenta con movimientos; desactívela en su lugar.");
    }

    [Fact]
    public async Task EditAccountCommand_DialogSaved_ReloadsTreeAndKeepsTheEditedAccountSelected()
    {
        var accountId = Guid.NewGuid();
        var originalTree = new[] { Node(accountId, "1", "Activo viejo", isPostable: false) };
        _chartOfAccountsService.GetTreeAsync(Arg.Any<CancellationToken>()).Returns(originalTree);
        _chartOfAccountsService.GetDetailAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(new AccountDetailDto { Id = accountId, Code = "1", Name = "Activo viejo", Type = AccountType.Asset, NormalBalance = NormalBalance.Debit });
        await _sut.InitializeAsync();
        _sut.SelectedNode = _sut.RootNodes.Single();

        // Success() with no id, deliberately: exercises AccountFormViewModel's
        // fallback to the account being edited when the result doesn't carry one.
        _chartOfAccountsService.UpdateAsync(Arg.Any<UpdateAccountRequest>(), Arg.Any<CancellationToken>())
            .Returns(ChartOfAccountsResult.Success());
        _chartOfAccountsService.GetTreeAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { Node(accountId, "1", "Activo renombrado", isPostable: false) });
        StubDialogServiceToReallyRunTheFormsSave();

        await _sut.EditAccountCommand.ExecuteAsync(null);

        _sut.SelectedNode.Should().NotBeNull();
        _sut.SelectedNode!.Id.Should().Be(accountId);
        _sut.SelectedNode!.Name.Should().Be("Activo renombrado");
    }

    [Fact]
    public async Task ToggleActiveCommand_DeactivateSucceeds_ReloadsTreeAndKeepsSelectionWithUpdatedState()
    {
        var accountId = Guid.NewGuid();
        var tree = new[] { Node(accountId, "1", "Activo", isPostable: true) };
        _chartOfAccountsService.GetTreeAsync(Arg.Any<CancellationToken>()).Returns(tree);
        await _sut.InitializeAsync();
        _sut.SelectedNode = _sut.RootNodes.Single();

        _chartOfAccountsService.DeactivateAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(ChartOfAccountsResult.Success());
        _chartOfAccountsService.GetTreeAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { Node(accountId, "1", "Activo", isPostable: true, isActive: false) });

        await _sut.ToggleActiveCommand.ExecuteAsync(null);

        _sut.SelectedNode.Should().NotBeNull();
        _sut.SelectedNode!.Id.Should().Be(accountId);
        _sut.SelectedNode!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleActiveCommand_ServiceFails_SetsErrorMessageAndDoesNotReload()
    {
        var accountId = Guid.NewGuid();
        var tree = new[] { Node(accountId, "1", "Activo", isPostable: true) };
        _chartOfAccountsService.GetTreeAsync(Arg.Any<CancellationToken>()).Returns(tree);
        await _sut.InitializeAsync();
        _sut.SelectedNode = _sut.RootNodes.Single();
        _chartOfAccountsService.DeactivateAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(ChartOfAccountsResult.Failure("Desactive primero las subcuentas."));

        await _sut.ToggleActiveCommand.ExecuteAsync(null);

        _sut.ErrorMessage.Should().Be("Desactive primero las subcuentas.");
        await _chartOfAccountsService.Received(1).GetTreeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAccountCommand_Succeeds_ReloadsTreeAndClearsSelectionAndDetail()
    {
        var accountId = Guid.NewGuid();
        var tree = new[] { Node(accountId, "1", "Activo", isPostable: true) };
        _chartOfAccountsService.GetTreeAsync(Arg.Any<CancellationToken>()).Returns(tree);
        _chartOfAccountsService.GetDetailAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(new AccountDetailDto { Id = accountId });
        await _sut.InitializeAsync();
        _sut.SelectedNode = _sut.RootNodes.Single();

        _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _chartOfAccountsService.DeleteAsync(accountId, Arg.Any<CancellationToken>())
            .Returns(ChartOfAccountsResult.Success());
        _chartOfAccountsService.GetTreeAsync(Arg.Any<CancellationToken>()).Returns([]);

        await _sut.DeleteAccountCommand.ExecuteAsync(null);

        _sut.SelectedNode.Should().BeNull();
        _sut.SelectedDetail.Should().BeNull();
    }
}
