using Folio.Domain.Entities;

namespace Folio.Application.Abstractions.Repositories;

/// <summary>There is exactly one <see cref="CompanySettings"/> row (SPEC §2.2).</summary>
public interface ICompanySettingsRepository
{
    Task<CompanySettings?> GetAsync(CancellationToken cancellationToken = default);

    Task AddAsync(CompanySettings settings, CancellationToken cancellationToken = default);
}
