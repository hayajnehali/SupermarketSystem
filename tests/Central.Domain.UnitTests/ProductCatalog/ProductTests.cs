using Central.Domain.ProductCatalog;
using Xunit;

namespace Central.Domain.UnitTests.ProductCatalog;

public sealed class ProductTests
{
    [Fact]
    public void Activate_WithoutInventoryBaseUnit_Throws()
    {
        var product = CreateProduct();

        product.AddUnit(Guid.NewGuid(), "EA", "Each", ProductUnitKind.Piece,
            isInventoryBaseUnit: false, isSellable: true, isOpenPackAllowed: false,
            isWeightBased: false, quantityPrecision: 0);

        Assert.Throws<InvalidOperationException>(product.Activate);
    }

    [Fact]
    public void Activate_WithExactlyOneInventoryBaseUnit_Succeeds()
    {
        var product = CreateProduct();

        product.AddUnit(Guid.NewGuid(), "EA", "Each", ProductUnitKind.Piece,
            isInventoryBaseUnit: true, isSellable: true, isOpenPackAllowed: false,
            isWeightBased: false, quantityPrecision: 0);

        product.Activate();

        Assert.Equal(ProductStatus.Active, product.Status);
    }

    [Fact]
    public void AddSecondInventoryBaseUnit_Throws()
    {
        var product = CreateProduct();

        product.AddUnit(Guid.NewGuid(), "EA", "Each", ProductUnitKind.Piece,
            isInventoryBaseUnit: true, isSellable: true, isOpenPackAllowed: false,
            isWeightBased: false, quantityPrecision: 0);

        Assert.Throws<InvalidOperationException>(() =>
            product.AddUnit(Guid.NewGuid(), "BOX", "Box", ProductUnitKind.Package,
                isInventoryBaseUnit: true, isSellable: true, isOpenPackAllowed: false,
                isWeightBased: false, quantityPrecision: 0));
    }

    [Fact]
    public void AddDuplicateUnitCode_IsCaseInsensitive_AndThrows()
    {
        var product = CreateProduct();

        product.AddUnit(Guid.NewGuid(), "ea", "Each", ProductUnitKind.Piece,
            isInventoryBaseUnit: true, isSellable: true, isOpenPackAllowed: false,
            isWeightBased: false, quantityPrecision: 0);

        Assert.Throws<InvalidOperationException>(() =>
            product.AddUnit(Guid.NewGuid(), "EA", "Each copy", ProductUnitKind.Piece,
                isInventoryBaseUnit: false, isSellable: true, isOpenPackAllowed: false,
                isWeightBased: false, quantityPrecision: 0));
    }

    [Fact]
    public void AddUnit_WithPrecisionAboveThree_Throws()
    {
        var product = CreateProduct();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            product.AddUnit(Guid.NewGuid(), "KG", "Kilogram", ProductUnitKind.Weight,
                isInventoryBaseUnit: true, isSellable: true, isOpenPackAllowed: false,
                isWeightBased: true, quantityPrecision: 4));
    }

    [Fact]
    public void AddUnit_OpenPackOnNonPackageUnit_Throws()
    {
        var product = CreateProduct();

        Assert.Throws<ArgumentException>(() =>
            product.AddUnit(Guid.NewGuid(), "EA", "Each", ProductUnitKind.Piece,
                isInventoryBaseUnit: true, isSellable: true, isOpenPackAllowed: true,
                isWeightBased: false, quantityPrecision: 0));
    }

    private static Product CreateProduct() =>
        Product.Create(Guid.NewGuid(), "sku-001", "Sample product", Guid.NewGuid());
}