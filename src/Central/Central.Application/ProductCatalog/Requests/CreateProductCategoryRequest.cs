namespace Central.Application.ProductCatalog.Requests;

/// <summary>Input DTO for creating a product category; this is not a CQRS command.</summary>
public sealed record CreateProductCategoryRequest(
    Guid CategoryId,
    Guid? ParentCategoryId,
    string CategoryCode,
    string Name,
    string? Description = null,
    int DisplayOrder = 0);
