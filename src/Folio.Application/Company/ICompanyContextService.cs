namespace Folio.Application.Company;

/// <summary>Read-only access to the single company's identity, for anything the UI
/// shows alongside a page title (the "Libro mayor" page header's versalita company
/// name — ADR-0014).</summary>
public interface ICompanyContextService
{
    Task<string> GetCompanyNameAsync(CancellationToken cancellationToken = default);
}
