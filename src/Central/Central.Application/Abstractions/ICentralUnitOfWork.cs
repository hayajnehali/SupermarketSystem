namespace Central.Application.Abstractions;

/// <summary>
/// Commits all changes tracked by the scoped Central persistence context.
/// Repositories stage changes; application services define the save boundary.
/// </summary>
public interface ICentralUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
