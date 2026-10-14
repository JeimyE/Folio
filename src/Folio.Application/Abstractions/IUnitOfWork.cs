namespace Folio.Application.Abstractions;

/// <summary>
/// Commits the changes staged by repositories in a single transaction. Implemented
/// by <c>FolioDbContext</c> itself in Infrastructure.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
