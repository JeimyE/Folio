using Folio.Application.Abstractions.Repositories;
using Folio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Folio.Infrastructure.Persistence.Repositories;

public sealed class EntryTemplateRepository(FolioDbContext context) : IEntryTemplateRepository
{
    public async Task<IReadOnlyList<EntryTemplate>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await context.EntryTemplates.Include(t => t.Lines).ToListAsync(cancellationToken);

    public Task AddAsync(EntryTemplate entryTemplate, CancellationToken cancellationToken = default)
    {
        context.EntryTemplates.Add(entryTemplate);
        return Task.CompletedTask;
    }
}
