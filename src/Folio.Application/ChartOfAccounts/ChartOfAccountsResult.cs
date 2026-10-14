namespace Folio.Application.ChartOfAccounts;

public sealed class ChartOfAccountsResult
{
    public bool Succeeded { get; }

    public string? ErrorMessage { get; }

    public Guid? AccountId { get; }

    private ChartOfAccountsResult(bool succeeded, string? errorMessage, Guid? accountId)
    {
        Succeeded = succeeded;
        ErrorMessage = errorMessage;
        AccountId = accountId;
    }

    public static ChartOfAccountsResult Success(Guid? accountId = null) => new(true, null, accountId);

    public static ChartOfAccountsResult Failure(string errorMessage) => new(false, errorMessage, null);
}
