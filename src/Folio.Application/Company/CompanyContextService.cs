using Folio.Application.Abstractions.Repositories;

namespace Folio.Application.Company;

public sealed class CompanyContextService(ICompanySettingsRepository companySettingsRepository) : ICompanyContextService
{
    public async Task<string> GetCompanyNameAsync(CancellationToken cancellationToken = default)
    {
        var settings = await companySettingsRepository.GetAsync(cancellationToken);
        return settings?.Name ?? string.Empty;
    }
}
