using Central.Application.ProductCatalog.Interfaces;
using Central.Domain.ProductCatalog;
using Central.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Central.Infrastructure.ProductCatalog.Repositories;

public sealed class ProductCategoryRepository(CentralDbContext dbContext) : IProductCategoryRepository
{
    public Task<bool> ExistsByCodeAsync(string normalizedCode, CancellationToken cancellationToken)
        => dbContext.ProductCategories
            .AsNoTracking()
            .AnyAsync(x => x.CategoryCode == normalizedCode, cancellationToken);

    public Task<bool> IsActiveByIdAsync(Guid categoryId, CancellationToken cancellationToken)
        => dbContext.ProductCategories
            .AsNoTracking()
            .AnyAsync(x => x.Id == categoryId && x.IsActive, cancellationToken);

    public async Task AddAsync(ProductCategory category, CancellationToken cancellationToken)
        => await dbContext.ProductCategories.AddAsync(category, cancellationToken);
}
