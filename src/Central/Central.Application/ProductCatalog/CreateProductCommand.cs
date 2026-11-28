namespace Central.Application.ProductCatalog;

public sealed record CreateProductCommand(
    Guid ProductId,
    string Sku,
    string Name,
    Guid CategoryId,
    Guid? BrandId,
    Guid? CountryOfOriginId,
    bool RequiresExpiryTracking,
    bool Activate,
    IReadOnlyCollection<CreateProductUnitInput> Units,
    string? Description = null);

public sealed record CreateProductUnitInput(
    Guid UnitId,
    string Code,
    string Name,
    Central.Domain.ProductCatalog.ProductUnitKind Kind,
    bool IsInventoryBaseUnit,
    bool IsSellable,
    bool IsOpenPackAllowed,
    bool IsWeightBased,
    byte QuantityPrecision);