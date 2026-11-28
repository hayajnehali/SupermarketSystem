using Central.Domain.ProductCatalog;

namespace Central.Application.ProductCatalog;

public interface IProductRepository
{
    Task<bool> ExistsBySkuAsync(string normalizedSku, CancellationToken cancellationToken);
    Task AddAsync(Product product, CancellationToken cancellationToken);
}