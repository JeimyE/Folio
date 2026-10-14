using Folio.Application.Abstractions.Repositories;
using Folio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Folio.Infrastructure.Persistence.Repositories;

public sealed class EntryTemplateRepository(IDbContextFactory<FolioDbContext> contextFactory) : IEntryTemplateRepository
{
    public async Task<IReadOnlyList<EntryTemplate>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.EntryTemplates.Include(t => t.Lines).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(EntryTemplate entryTemplate, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.EntryTemplates.Add(entryTemplate);
        await context.SaveChangesAsync(cancellationToken);
    }
}
