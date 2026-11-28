using Central.Domain.ProductCatalog;

namespace Central.Application.ProductCatalog.Interfaces;

/// <summary>
/// Persistence contract required by the Product Catalog application services.
/// The implementation belongs to Central.Infrastructure.
/// </summary>
public interface IProductRepository
{
    Task<bool> ExistsBySkuAsync(string normalizedSku, CancellationToken cancellationToken);
    Task AddAsync(Product product, CancellationToken cancellationToken);
}
