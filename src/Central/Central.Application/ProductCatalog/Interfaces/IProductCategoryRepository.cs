using Central.Domain.ProductCatalog;

namespace Central.Application.ProductCatalog.Interfaces;

public interface IProductCategoryRepository
{
    Task<bool> ExistsByCodeAsync(string categoryCode, CancellationToken cancellationToken);
    Task<bool> IsActiveByIdAsync(Guid categoryId, CancellationToken cancellationToken);
    Task AddAsync(ProductCategory category, CancellationToken cancellationToken);
}
