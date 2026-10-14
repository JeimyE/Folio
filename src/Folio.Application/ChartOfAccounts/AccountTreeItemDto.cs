namespace Folio.Application.ChartOfAccounts;

/// <summary>One node of the chart-of-accounts tree, already sorted by code (numeric
/// segment order — see <see cref="Folio.Domain.ValueObjects.AccountCode.CompareTo"/>).</summary>
public sealed class AccountTreeItemDto
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public bool IsPostable { get; init; }

    public bool IsActive { get; init; }

    public bool HasChildren { get; init; }

    public IReadOnlyList<AccountTreeItemDto> Children { get; init; } = [];
}
