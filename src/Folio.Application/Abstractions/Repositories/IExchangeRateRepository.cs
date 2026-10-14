using Folio.Domain.Entities;

namespace Folio.Application.Abstractions.Repositories;

public interface IExchangeRateRepository
{
    Task<ExchangeRate?> GetByDateAsync(DateOnly rateDate, CancellationToken cancellationToken = default);

    Task AddAsync(ExchangeRate rate, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<ExchangeRate> rates, CancellationToken cancellationToken = default);
}
