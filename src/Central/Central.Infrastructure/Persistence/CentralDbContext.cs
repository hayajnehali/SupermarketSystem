using Central.Application.Abstractions;
using Central.Application.ProductCatalog;
using Central.Domain.ProductCatalog;
using Microsoft.EntityFrameworkCore;

namespace Central.Infrastructure.Persistence;

/// <summary>
/// Central database context. Module mappings are discovered from this assembly.
/// Initial migrations remain gated until the complete Central physical schema is mapped.
/// </summary>
public sealed class CentralDbContext(DbContextOptions<CentralDbContext> options)
    : DbContext(options), ICentralUnitOfWork, IProductRepository
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductUnit> ProductUnits => Set<ProductUnit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CentralDbContext).Assembly);
    }

    public Task<bool> ExistsBySkuAsync(string normalizedSku, CancellationToken cancellationToken)
        => Products.AsNoTracking().AnyAsync(x => x.Sku == normalizedSku, cancellationToken);

    public Task AddAsync(Product product, CancellationToken cancellationToken)
        => Products.AddAsync(product, cancellationToken).AsTask();
}
