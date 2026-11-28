using Central.Application.Abstractions;
using Central.Domain.ProductCatalog;

namespace Central.Application.ProductCatalog;

/// <summary>
/// Application service for Product Catalog use cases.
/// Uses a conventional service-layer approach; no CQRS command/handler pipeline.
/// </summary>
public sealed class ProductService(
    IProductRepository products,
    ICentralUnitOfWork unitOfWork)
{
    public async Task<Guid> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Units);

        var normalizedSku = request.Sku?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedSku))
            throw new ArgumentException("SKU is required.", nameof(request));

        if (await products.ExistsBySkuAsync(normalizedSku, cancellationToken))
            throw new InvalidOperationException($"SKU '{normalizedSku}' is already in use.");

        var product = Product.Create(
            request.ProductId,
            normalizedSku,
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
