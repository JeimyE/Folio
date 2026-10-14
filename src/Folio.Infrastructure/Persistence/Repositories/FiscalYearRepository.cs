using Folio.Application.Abstractions.Repositories;
using Folio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Folio.Infrastructure.Persistence.Repositories;

public sealed class FiscalYearRepository(FolioDbContext context) : IFiscalYearRepository
{
    public Task<FiscalYear?> GetByYearAsync(int year, CancellationToken cancellationToken = default) =>
        context.FiscalYears.FirstOrDefaultAsync(f => f.Year == year, cancellationToken);

    public Task AddAsync(FiscalYear fiscalYear, CancellationToken cancellationToken = default)
    {
        context.FiscalYears.Add(fiscalYear);
        return Task.CompletedTask;
    }
}
