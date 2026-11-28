namespace Central.Domain.ProductCatalog;

/// <summary>
/// Central-owned product category. Category hierarchy is validated by the application service.
/// </summary>
public sealed class ProductCategory
{
    private ProductCategory() { }

    public Guid Id { get; private set; }
    public Guid? ParentCategoryId { get; private set; }
    public string CategoryCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static ProductCategory Create(
        Guid id,
        Guid? parentCategoryId,
        string categoryCode,
        string name,
        string? description,
        int displayOrder)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Category ID cannot be empty.", nameof(id));

        if (parentCategoryId == Guid.Empty || parentCategoryId == id)
            throw new ArgumentException("Parent category ID is invalid.", nameof(parentCategoryId));

        ArgumentException.ThrowIfNullOrWhiteSpace(categoryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalizedCode = categoryCode.Trim().ToUpperInvariant();
        var normalizedName = name.Trim();
        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (normalizedCode.Length > 50)
            throw new ArgumentOutOfRangeException(nameof(categoryCode), "Category code cannot exceed 50 characters.");
        if (normalizedName.Length > 150)
            throw new ArgumentOutOfRangeException(nameof(name), "Category name cannot exceed 150 characters.");
        if (normalizedDescription?.Length > 500)
            throw new ArgumentOutOfRangeException(nameof(description), "Category description cannot exceed 500 characters.");
        if (displayOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(displayOrder), "Display order cannot be negative.");

        return new ProductCategory
        {
            Id = id,
            ParentCategoryId = parentCategoryId,
            CategoryCode = normalizedCode,
            Name = normalizedName,
            Description = normalizedDescription,
            DisplayOrder = displayOrder
        };
    }

    public void UpdateDetails(string name, string? description, int displayOrder)
    {
        EnsureActive();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalizedName = name.Trim();
        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (normalizedName.Length > 150)
            throw new ArgumentOutOfRangeException(nameof(name), "Category name cannot exceed 150 characters.");
        if (normalizedDescription?.Length > 500)
            throw new ArgumentOutOfRangeException(nameof(description), "Category description cannot exceed 500 characters.");
        if (displayOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(displayOrder), "Display order cannot be negative.");

        Name = normalizedName;
        Description = normalizedDescription;
        DisplayOrder = displayOrder;
    }

    public void Deactivate()
    {
        EnsureActive();
        IsActive = false;
    }

    private void EnsureActive()
    {
        if (!IsActive)
            throw new InvalidOperationException("An inactive category cannot be modified.");
    }
}
