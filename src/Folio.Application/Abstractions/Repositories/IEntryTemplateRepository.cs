using Folio.Domain.Entities;

namespace Folio.Application.Abstractions.Repositories;

public interface IEntryTemplateRepository
{
    Task<IReadOnlyList<EntryTemplate>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(EntryTemplate entryTemplate, CancellationToken cancellationToken = default);
}
