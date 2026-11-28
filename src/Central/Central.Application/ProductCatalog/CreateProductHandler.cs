using Central.Application.Abstractions;
using Central.Domain.ProductCatalog;

namespace Central.Application.ProductCatalog;

/// <summary>Creates a centrally owned Product aggregate; persistence is supplied by Infrastructure.</summary>
public sealed class CreateProductHandler(
    IProductRepository products,
    ICentralUnitOfWork unitOfWork)
{
    public async Task<Guid> HandleAsync(
        CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var normalizedSku = command.Sku?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedSku))
            throw new ArgumentException("SKU is required.", nameof(command));

        if (await products.ExistsBySkuAsync(normalizedSku, cancellationToken))
            throw new InvalidOperationException($"SKU '{normalizedSku}' is already in use.");

        var product = Product.Create(
            command.ProductId,
            normalizedSku,
            command.Name,
            command.CategoryId,
            command.BrandId,
            command.CountryOfOriginId,
            command.RequiresExpiryTracking);

        foreach (var unit in command.Units)
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

        if (command.Activate)
            product.Activate();

        await products.AddAsync(product, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return product.Id;
    }
}