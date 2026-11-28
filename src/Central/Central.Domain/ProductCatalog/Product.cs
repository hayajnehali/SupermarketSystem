namespace Central.Domain.ProductCatalog;

/// <summary>
/// Central-owned Product aggregate. Persistence mappings are kept outside the Domain project.
/// </summary>
public sealed class Product
{
    private readonly List<ProductUnit> _units = [];

    private Product() { }

    public Guid Id { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid? BrandId { get; private set; }
    public Guid? CountryOfOriginId { get; private set; }
    public bool RequiresExpiryTracking { get; private set; }
    public ProductStatus Status { get; private set; } = ProductStatus.Draft;
    public IReadOnlyCollection<ProductUnit> Units => _units.AsReadOnly();

    public static Product Create(
        Guid id,
        string sku,
        string name,
        Guid categoryId,
        Guid? brandId = null,
        Guid? countryOfOriginId = null,
        bool requiresExpiryTracking = false,
        string? description = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Product ID cannot be empty.", nameof(id));

        ValidateSku(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (categoryId == Guid.Empty)
            throw new ArgumentException("Category ID cannot be empty.", nameof(categoryId));

        return new Product
        {
            Id = id,
            Sku = sku,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CategoryId = categoryId,
            BrandId = brandId,
            CountryOfOriginId = countryOfOriginId,
            RequiresExpiryTracking = requiresExpiryTracking
        };
    }

    public void AddUnit(
        Guid unitId,
        string code,
        string name,
        ProductUnitKind kind,
        bool isInventoryBaseUnit,
        bool isSellable,
        bool isOpenPackAllowed,
        bool isWeightBased,
        byte quantityPrecision)
    {
        EnsureDraft();

        var normalizedCode = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedCode))
            throw new ArgumentException("Unit code is required.", nameof(code));

        if (_units.Any(x => string.Equals(x.Code, normalizedCode, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Unit code '{normalizedCode}' already exists for this product.");

        if (isInventoryBaseUnit && _units.Any(x => x.IsInventoryBaseUnit))
            throw new InvalidOperationException("A product can have only one inventory base unit.");

        _units.Add(ProductUnit.Create(
            unitId, normalizedCode, name, kind, isInventoryBaseUnit,
            isSellable, isOpenPackAllowed, isWeightBased, quantityPrecision));
    }

    public void Activate()
    {
        EnsureDraft();

        if (_units.Count(x => x.IsInventoryBaseUnit) != 1)
            throw new InvalidOperationException("An active product must have exactly one inventory base unit.");

        Status = ProductStatus.Active;
    }

    public void Deactivate()
    {
        if (Status != ProductStatus.Active)
            throw new InvalidOperationException("Only an active product can be deactivated.");

        Status = ProductStatus.Inactive;
    }

    private static void ValidateSku(string sku)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        if (sku.Length > 60)
            throw new ArgumentOutOfRangeException(nameof(sku), "SKU cannot exceed 60 characters.");

        if (sku.Any(char.IsWhiteSpace))
            throw new ArgumentException("SKU cannot contain whitespace. The value is never trimmed or normalized.", nameof(sku));
    }

    private void EnsureDraft()
    {
        if (Status != ProductStatus.Draft)
            throw new InvalidOperationException("Product structure can only be changed while the product is in Draft status.");
    }
}
