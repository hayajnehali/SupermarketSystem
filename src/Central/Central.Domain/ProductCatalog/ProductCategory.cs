using Central.Domain.Common;

namespace Central.Domain.ProductCatalog;

/// <summary>
/// Central-owned product-category aggregate. Category hierarchy is validated by the application service.
/// </summary>
public sealed class ProductCategory : AggregateRoot
{
    private ProductCategory() { }

    private ProductCategory(Guid id) : base(id) { }

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
        if (parentCategoryId == Guid.Empty || parentCategoryId == id)
            throw new ArgumentException("Parent category ID is invalid.", nameof(parentCategoryId));

        ArgumentException.ThrowIfNullOrWhiteSpace(categoryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!string.Equals(categoryCode, categoryCode.Trim(), StringComparison.Ordinal))
            throw new ArgumentException(
                "Category code cannot contain leading or trailing whitespace.",
                nameof(categoryCode));

        var normalizedName = name.Trim();
        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        if (categoryCode.Length > 50)
            throw new ArgumentOutOfRangeException(nameof(categoryCode), "Category code cannot exceed 50 characters.");
        if (normalizedName.Length > 150)
            throw new ArgumentOutOfRangeException(nameof(name), "Category name cannot exceed 150 characters.");
        if (normalizedDescription?.Length > 500)
            throw new ArgumentOutOfRangeException(nameof(description), "Category description cannot exceed 500 characters.");
        if (displayOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(displayOrder), "Display order cannot be negative.");

        return new ProductCategory(id)
        {
            ParentCategoryId = parentCategoryId,
            CategoryCode = categoryCode,
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
