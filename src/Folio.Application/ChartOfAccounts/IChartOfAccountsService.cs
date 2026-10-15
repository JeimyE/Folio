namespace Folio.Application.ChartOfAccounts;

public interface IChartOfAccountsService
{
    Task<IReadOnlyList<AccountTreeItemDto>> GetTreeAsync(CancellationToken cancellationToken = default);

    Task<AccountDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The accounting-equation footnote under the tree: Activo vs. Pasivo + Patrimonio.</summary>
    Task<BalanceCheckDto> GetBalanceCheckAsync(CancellationToken cancellationToken = default);

    Task<ChartOfAccountsResult> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default);

    Task<ChartOfAccountsResult> UpdateAsync(UpdateAccountRequest request, CancellationToken cancellationToken = default);

    Task<ChartOfAccountsResult> DeactivateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ChartOfAccountsResult> ActivateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ChartOfAccountsResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
