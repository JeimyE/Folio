using Folio.Domain.Entities;

namespace Folio.Application.Abstractions.Repositories;

public interface IFiscalYearRepository
{
    Task<FiscalYear?> GetByYearAsync(int year, CancellationToken cancellationToken = default);

    Task AddAsync(FiscalYear fiscalYear, CancellationToken cancellationToken = default);
}
