namespace Central.Application.Abstractions;

public interface ICentralUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}