using Central.Domain.ProductCatalog;

namespace Central.Application.ProductCatalog.Interfaces;

/// <summary>
/// Persistence contract for the Product aggregate root.
/// ProductUnit is owned by Product and therefore has no separate repository.
/// </summary>
public interface IProductRepository
{
    Task<bool> ExistsBySkuAsync(string sku, CancellationToken cancellationToken);
    void Add(Product product);
}
