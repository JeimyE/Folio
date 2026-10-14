using Folio.Application.Abstractions.Repositories;
using Folio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Folio.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(IDbContextFactory<FolioDbContext> contextFactory) : IUserRepository
{
    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Users.FirstOrDefaultAsync(u => u.Username == username, cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        // The user came from a different, already-disposed context (IDbContextFactory
        // gives out a short-lived one per call), so it's re-attached as Modified here
        // rather than tracked continuously. EF has no prior snapshot for it, so every
        // column — not just the ones that changed — is written back; the audit
        // interceptor still redacts PasswordHash/Salt regardless.
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Users.Update(user);
        await context.SaveChangesAsync(cancellationToken);
    }
}
