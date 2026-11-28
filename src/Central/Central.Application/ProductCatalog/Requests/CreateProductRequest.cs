namespace Central.Application.ProductCatalog.Requests;

/// <summary>
/// Input model for creating a product through the Product application service.
/// This is a regular application request, not a CQRS command.
/// </summary>
public sealed record CreateProductRequest(
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
