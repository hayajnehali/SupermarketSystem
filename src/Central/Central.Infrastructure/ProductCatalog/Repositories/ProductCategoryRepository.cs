using Central.Application.ProductCatalog.Interfaces;
using Central.Domain.ProductCatalog;
using Central.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Central.Infrastructure.ProductCatalog.Repositories;

/// <summary>EF Core implementation of the ProductCategory aggregate repository.</summary>
public sealed class ProductCategoryRepository(CentralDbContext dbContext) : IProductCategoryRepository
{
    public Task<bool> ExistsByCodeAsync(string categoryCode, CancellationToken cancellationToken)
        => dbContext.ProductCategories
            .AsNoTracking()
            .AnyAsync(x => x.CategoryCode == categoryCode, cancellationToken);

    public Task<bool> IsActiveByIdAsync(Guid categoryId, CancellationToken cancellationToken)
        => dbContext.ProductCategories
            .AsNoTracking()
            .AnyAsync(x => x.Id == categoryId && x.IsActive, cancellationToken);

    public void Add(ProductCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);
        dbContext.ProductCategories.Add(category);
    }
}
