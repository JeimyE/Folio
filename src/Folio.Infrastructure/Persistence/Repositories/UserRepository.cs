using Folio.Application.Abstractions.Repositories;
using Folio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Folio.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(FolioDbContext context) : IUserRepository
{
    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        context.Users.FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Add(user);
        return Task.CompletedTask;
    }
}
