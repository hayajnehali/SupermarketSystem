namespace Central.Domain.ProductCatalog;

/// <summary>A product-specific unit. Selling prices are owned by the Pricing module.</summary>
public sealed class ProductUnit
{
    private ProductUnit() { }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public ProductUnitKind Kind { get; private set; }
    public bool IsInventoryBaseUnit { get; private set; }
    public bool IsSellable { get; private set; }
    public bool IsOpenPackAllowed { get; private set; }
    public bool IsWeightBased { get; private set; }
    public bool IsActive { get; private set; } = true;
    public byte QuantityPrecision { get; private set; }

    internal static ProductUnit Create(
        Guid id,
        string code,
        string name,
        ProductUnitKind kind,
        bool isInventoryBaseUnit,
        bool isSellable,
        bool isOpenPackAllowed,
        bool isWeightBased,
        byte quantityPrecision)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Unit ID cannot be empty.", nameof(id));

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        if (quantityPrecision > 3)
            throw new ArgumentOutOfRangeException(nameof(quantityPrecision), "Quantity precision cannot exceed three decimal places.");

        if (isOpenPackAllowed && kind != ProductUnitKind.Package)
            throw new ArgumentException("Open-pack behavior can only be enabled for Package units.", nameof(isOpenPackAllowed));

        return new ProductUnit
        {
            Id = id,
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Kind = kind,
            IsInventoryBaseUnit = isInventoryBaseUnit,
            IsSellable = isSellable,
            IsOpenPackAllowed = isOpenPackAllowed,
            IsWeightBased = isWeightBased,
            QuantityPrecision = quantityPrecision
        };
    }
}