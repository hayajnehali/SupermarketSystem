using Central.Domain.ProductCatalog;

namespace Central.Application.ProductCatalog.Interfaces;

public interface IProductCategoryRepository
{
    Task<bool> ExistsByCodeAsync(string normalizedCode, CancellationToken cancellationToken);
    Task<bool> ExistsByIdAsync(Guid categoryId, CancellationToken cancellationToken);
    Task AddAsync(ProductCategory category, CancellationToken cancellationToken);
}
