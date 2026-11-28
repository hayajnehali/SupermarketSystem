using Central.Application.ProductCatalog.Interfaces;
using Central.Domain.ProductCatalog;
using Central.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Central.Infrastructure.ProductCatalog.Repositories;

/// <summary>
/// EF Core implementation of the Product Catalog repository contract.
/// </summary>
public sealed class ProductRepository(CentralDbContext dbContext) : IProductRepository
{
    public Task<bool> ExistsBySkuAsync(
        string normalizedSku,
        CancellationToken cancellationToken)
        => dbContext.Products
            .AsNoTracking()
            .AnyAsync(x => x.Sku == normalizedSku, cancellationToken);

    public async Task AddAsync(Product product, CancellationToken cancellationToken)
        => await dbContext.Products.AddAsync(product, cancellationToken);
}
