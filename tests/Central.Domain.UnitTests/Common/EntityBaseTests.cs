using Central.Domain.ProductCatalog;
using Xunit;

namespace Central.Domain.UnitTests.Common;

public sealed class EntityBaseTests
{
    [Fact]
    public void Entities_WithSameConcreteTypeAndId_AreEqual()
    {
        var id = Guid.NewGuid();
        var first = Product.Create(id, "SKU-1", "First", Guid.NewGuid());
        var second = Product.Create(id, "SKU-2", "Second", Guid.NewGuid());

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Entities_WithDifferentIds_AreNotEqual()
    {
        var first = Product.Create(Guid.NewGuid(), "SKU-1", "First", Guid.NewGuid());
        var second = Product.Create(Guid.NewGuid(), "SKU-1", "First", Guid.NewGuid());

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Entities_WithSameIdButDifferentConcreteTypes_AreNotEqual()
    {
        var id = Guid.NewGuid();
        var product = Product.Create(id, "SKU-1", "Product", Guid.NewGuid());
        var category = ProductCategory.Create(id, null, "CAT-1", "Category", null, 0);

        Assert.NotEqual(product, category);
    }

    [Fact]
    public void Create_WithEmptyEntityId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Product.Create(Guid.Empty, "SKU-1", "Product", Guid.NewGuid()));
    }
}
