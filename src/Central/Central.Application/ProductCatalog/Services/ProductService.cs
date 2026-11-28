using Central.Application.Abstractions;
using Central.Application.ProductCatalog.Interfaces;
using Central.Application.ProductCatalog.Requests;
using Central.Domain.ProductCatalog;

namespace Central.Application.ProductCatalog.Services;

/// <summary>
/// Application service for Product Catalog use cases.
/// Uses a conventional service-layer approach; no CQRS command/handler pipeline.
/// </summary>
public sealed class ProductService(
    IProductRepository products,
    IProductCategoryRepository categories,
    ICentralUnitOfWork unitOfWork)
{
    public async Task<Guid> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Units);

        ArgumentException.ThrowIfNullOrWhiteSpace(request.Sku);
        if (request.Sku.Any(char.IsWhiteSpace))
            throw new ArgumentException("SKU cannot contain whitespace.", nameof(request));
        if (request.Sku.Length > 60)
            throw new ArgumentOutOfRangeException(nameof(request), "SKU cannot exceed 60 characters.");

        if (!await categories.IsActiveByIdAsync(request.CategoryId, cancellationToken))
            throw new InvalidOperationException($"Active category '{request.CategoryId}' does not exist.");

        if (await products.ExistsBySkuAsync(request.Sku, cancellationToken))
            throw new InvalidOperationException($"SKU '{request.Sku}' is already in use.");

        var product = Product.Create(
            request.ProductId,
            request.Sku,
            request.Name,
            request.CategoryId,
            request.BrandId,
            request.CountryOfOriginId,
            request.RequiresExpiryTracking,
            request.Description);

        foreach (var unit in request.Units)
        {
            product.AddUnit(
                unit.UnitId,
                unit.Code,
                unit.Name,
                unit.Kind,
                unit.IsInventoryBaseUnit,
                unit.IsSellable,
                unit.IsOpenPackAllowed,
                unit.IsWeightBased,
                unit.QuantityPrecision);
        }

        if (request.Activate)
            product.Activate();

        await products.AddAsync(product, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return product.Id;
    }
}
