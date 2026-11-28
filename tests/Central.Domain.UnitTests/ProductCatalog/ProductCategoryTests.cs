using Central.Domain.ProductCatalog;
using Xunit;

namespace Central.Domain.UnitTests.ProductCatalog;

public sealed class ProductCategoryTests
{
    [Fact]
    public void Create_NormalizesCodeAndTrimsText()
    {
        var category = ProductCategory.Create(
            Guid.NewGuid(), null, "  grocery  ", "  Grocery  ", "  Daily products  ", 2);

        Assert.Equal("GROCERY", category.CategoryCode);
        Assert.Equal("Grocery", category.Name);
        Assert.Equal("Daily products", category.Description);
        Assert.True(category.IsActive);
    }

    [Fact]
    public void Create_WithNegativeDisplayOrder_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProductCategory.Create(Guid.NewGuid(), null, "FOOD", "Food", null, -1));
    }

    [Fact]
    public void Create_WithSelfAsParent_Throws()
    {
        var id = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            ProductCategory.Create(id, id, "FOOD", "Food", null, 0));
    }

    [Fact]
    public void UpdateDetails_OnInactiveCategory_Throws()
    {
        var category = ProductCategory.Create(
            Guid.NewGuid(), null, "FOOD", "Food", null, 0);

        category.Deactivate();

        Assert.Throws<InvalidOperationException>(() =>
            category.UpdateDetails("Groceries", null, 1));
    }
}
