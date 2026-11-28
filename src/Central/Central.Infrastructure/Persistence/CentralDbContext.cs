using Central.Application.Abstractions;
using Central.Application.ProductCatalog;
using Central.Domain.ProductCatalog;
using Microsoft.EntityFrameworkCore;
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

    public override int SaveChanges()
    {
        ApplyProductAudit();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyProductAudit();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyProductAudit();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyProductAudit();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyProductAudit()
    {
        var now = clock.UtcNow.UtcDateTime;

        foreach (var entry in ChangeTracker.Entries<Product>())
        {
            if (entry.State == EntityState.Added)
            {
                var actorId = RequireAuthenticatedActor();

                entry.Property<DateTime>("CreatedAtUtc").CurrentValue = now;
                entry.Property<Guid>("CreatedByUserId").CurrentValue = actorId;
            }
            else if (entry.State == EntityState.Modified)
            {
                var actorId = RequireAuthenticatedActor();

                entry.Property<DateTime?>("UpdatedAtUtc").CurrentValue = now;
                entry.Property<Guid?>("UpdatedByUserId").CurrentValue = actorId;

                if (entry.Property(x => x.Status).OriginalValue == ProductStatus.Active
                    && entry.Entity.Status == ProductStatus.Inactive)
                {
                    entry.Property<DateTime?>("DeactivatedAtUtc").CurrentValue = now;
                    entry.Property<Guid?>("DeactivatedByUserId").CurrentValue = actorId;
                }
            }
        }

        foreach (var entry in ChangeTracker.Entries<ProductUnit>()
                     .Where(x => x.State == EntityState.Added))
        {
            entry.Property<DateTime>("CreatedAtUtc").CurrentValue = now;
        }
    }

    private Guid RequireAuthenticatedActor()
    {
        if (!currentActor.IsAuthenticated || currentActor.UserId is not { } userId || userId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "An authenticated actor with a valid UserId is required for Product writes. " +
                "System-initiated writes must use an explicitly configured service actor.");
        }

        return userId;
    }

    public Task<bool> ExistsBySkuAsync(string normalizedSku, CancellationToken cancellationToken)
        => Products.AsNoTracking().AnyAsync(x => x.Sku == normalizedSku, cancellationToken);

    public Task AddAsync(Product product, CancellationToken cancellationToken)
        => Products.AddAsync(product, cancellationToken).AsTask();
}
