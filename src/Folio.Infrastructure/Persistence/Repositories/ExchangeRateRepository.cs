using Folio.Application.Abstractions.Repositories;
using Folio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Folio.Infrastructure.Persistence.Repositories;

public sealed class ExchangeRateRepository(FolioDbContext context) : IExchangeRateRepository
{
    public Task<ExchangeRate?> GetByDateAsync(DateOnly rateDate, CancellationToken cancellationToken = default) =>
        context.ExchangeRates.FirstOrDefaultAsync(r => r.Date == rateDate, cancellationToken);

    public Task AddAsync(ExchangeRate rate, CancellationToken cancellationToken = default)
    {
        context.ExchangeRates.Add(rate);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(IEnumerable<ExchangeRate> rates, CancellationToken cancellationToken = default)
    {
        context.ExchangeRates.AddRange(rates);
        return Task.CompletedTask;
    }
}
