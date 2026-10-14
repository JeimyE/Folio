using Folio.Domain.Entities;

namespace Folio.Application.Abstractions.Repositories;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes made to a <see cref="User"/> previously returned by this
    /// repository (e.g. after <see cref="User.RecordFailedLogin"/>).
    /// </summary>
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
}
