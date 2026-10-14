using Folio.Application.Abstractions.Repositories;
using Folio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Folio.Infrastructure.Persistence.Repositories;

public sealed class ExchangeRateRepository(IDbContextFactory<FolioDbContext> contextFactory) : IExchangeRateRepository
{
    public async Task<ExchangeRate?> GetByDateAsync(DateOnly rateDate, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ExchangeRates.FirstOrDefaultAsync(r => r.Date == rateDate, cancellationToken);
    }

    public async Task AddAsync(ExchangeRate rate, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.ExchangeRates.Add(rate);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<ExchangeRate> rates, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.ExchangeRates.AddRange(rates);
        await context.SaveChangesAsync(cancellationToken);
    }
}
