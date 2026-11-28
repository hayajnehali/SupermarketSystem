using Central.Application.ProductCatalog.Interfaces;
using Central.Domain.ProductCatalog;
using Central.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Central.Infrastructure.ProductCatalog.Repositories;

/// <summary>
/// EF Core implementation of the Product Catalog repository contract.
/// SKU comparisons use the database column's case-insensitive collation.
/// </summary>
public sealed class ProductRepository(CentralDbContext dbContext) : IProductRepository
{
    public Task<bool> ExistsBySkuAsync(
        string sku,
        CancellationToken cancellationToken)
        => dbContext.Products
            .AsNoTracking()
            .AnyAsync(x => x.Sku == sku, cancellationToken);

    public async Task AddAsync(Product product, CancellationToken cancellationToken)
        => await dbContext.Products.AddAsync(product, cancellationToken);
}
