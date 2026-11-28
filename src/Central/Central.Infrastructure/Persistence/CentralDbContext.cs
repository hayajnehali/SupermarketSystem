using Central.Application.Abstractions;
using Central.Application.ProductCatalog;
using Central.Domain.ProductCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Supermarket.BuildingBlocks.Identity;
using Supermarket.BuildingBlocks.Time;

namespace Central.Infrastructure.Persistence;

/// <summary>
/// Central database context. Module mappings are discovered from this assembly.
/// Initial migrations remain gated until the complete Central physical schema is mapped.
/// </summary>
public sealed class CentralDbContext(
    DbContextOptions<CentralDbContext> options,
    ICurrentActor currentActor,
    IClock clock)
    : DbContext(options), ICentralUnitOfWork, IProductRepository
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductUnit> ProductUnits => Set<ProductUnit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CentralDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyProductAudit();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyProductAudit()
    {
        var now = clock.UtcNow.UtcDateTime;

        foreach (var entry in ChangeTracker.Entries<Product>())
        {
            if (entry.State == EntityState.Added)
            {
                var actorId = currentActor.UserId
                    ?? throw new InvalidOperationException("An authenticated Central actor is required to create a product.");

                entry.Property<DateTime>("CreatedAtUtc").CurrentValue = now;
                entry.Property<Guid>("CreatedByUserId").CurrentValue = actorId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property<DateTime?>("UpdatedAtUtc").CurrentValue = now;
                entry.Property<Guid?>("UpdatedByUserId").CurrentValue = currentActor.UserId;

                if (entry.Property(x => x.Status).OriginalValue == ProductStatus.Active
                    && entry.Entity.Status == ProductStatus.Inactive)
                {
                    entry.Property<DateTime?>("DeactivatedAtUtc").CurrentValue = now;
                    entry.Property<Guid?>("DeactivatedByUserId").CurrentValue = currentActor.UserId;
                }
            }
        }

        foreach (var entry in ChangeTracker.Entries<ProductUnit>().Where(x => x.State == EntityState.Added))
            entry.Property<DateTime>("CreatedAtUtc").CurrentValue = now;
    }

    public Task<bool> ExistsBySkuAsync(string normalizedSku, CancellationToken cancellationToken)
        => Products.AsNoTracking().AnyAsync(x => x.Sku == normalizedSku, cancellationToken);

    public Task AddAsync(Product product, CancellationToken cancellationToken)
        => Products.AddAsync(product, cancellationToken).AsTask();
}
