using Central.Domain.ProductCatalog;

namespace Central.Application.ProductCatalog.Interfaces;

/// <summary>Persistence contract for the ProductCategory aggregate root.</summary>
public interface IProductCategoryRepository
{
    Task<bool> ExistsByCodeAsync(string categoryCode, CancellationToken cancellationToken);
    Task<bool> IsActiveByIdAsync(Guid categoryId, CancellationToken cancellationToken);
    void Add(ProductCategory category);
}
