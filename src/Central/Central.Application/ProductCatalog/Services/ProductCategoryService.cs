using Central.Application.Abstractions;
using Central.Application.ProductCatalog.Interfaces;
using Central.Application.ProductCatalog.Requests;
using Central.Domain.ProductCatalog;

namespace Central.Application.ProductCatalog.Services;

/// <summary>Application service for product category use cases.</summary>
public sealed class ProductCategoryService(
    IProductCategoryRepository categories,
    ICentralUnitOfWork unitOfWork)
{
    public async Task<Guid> CreateAsync(
        CreateProductCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedCode = request.CategoryCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedCode))
            throw new ArgumentException("Category code is required.", nameof(request));

        if (await categories.ExistsByCodeAsync(normalizedCode, cancellationToken))
            throw new InvalidOperationException($"Category code '{normalizedCode}' is already in use.");

        if (request.ParentCategoryId is { } parentId
            && !await categories.IsActiveByIdAsync(parentId, cancellationToken))
        {
            throw new InvalidOperationException($"Parent category '{parentId}' does not exist or is inactive.");
        }

        var category = ProductCategory.Create(
            request.CategoryId,
            request.ParentCategoryId,
            normalizedCode,
            request.Name,
            request.Description,
            request.DisplayOrder);

        await categories.AddAsync(category, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return category.Id;
    }
}
