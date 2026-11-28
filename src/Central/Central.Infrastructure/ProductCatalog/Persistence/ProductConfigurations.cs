using Central.Domain.ProductCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Central.Infrastructure.ProductCatalog.Persistence;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", "catalog", table =>
            table.HasCheckConstraint("CK_Products_ProductStatus", "[ProductStatus] IN (1, 2, 3)"));

        builder.HasKey(x => x.Id).HasName("PK_Products");
        builder.Property(x => x.Id)
            .HasColumnName("ProductId")
            .ValueGeneratedNever();

        builder.Property(x => x.Sku)
            .HasColumnName("SKU")
            .HasMaxLength(60)
            .IsRequired();

        builder.HasIndex(x => x.Sku)
            .IsUnique()
            .HasDatabaseName("UX_Products_SKU");

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.CategoryId)
            .HasColumnName("ProductCategoryId")
            .IsRequired();

        builder.Property(x => x.BrandId);
        builder.Property(x => x.CountryOfOriginId);
        builder.Property(x => x.RequiresExpiryTracking).IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("ProductStatus")
            .HasConversion<byte>()
            .IsRequired();

        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.HasMany(x => x.Units)
            .WithOne()
            .HasForeignKey("ProductId")
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(x => x.Units)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ProductUnitConfiguration : IEntityTypeConfiguration<ProductUnit>
{
    public void Configure(EntityTypeBuilder<ProductUnit> builder)
    {
        builder.ToTable("ProductUnits", "catalog", table =>
        {
            table.HasCheckConstraint("CK_ProductUnits_QuantityPrecision", "[QuantityPrecision] BETWEEN 0 AND 3");
        });

        builder.HasKey(x => x.Id).HasName("PK_ProductUnits");
        builder.Property(x => x.Id)
            .HasColumnName("ProductUnitId")
            .ValueGeneratedNever();

        builder.Property<Guid>("ProductId").IsRequired();

        builder.Property(x => x.Code)
            .HasColumnName("UnitCode")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("UnitName")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(x => x.Kind)
            .HasColumnName("UnitKind")
            .HasConversion<byte>()
            .IsRequired();

        builder.Property(x => x.IsInventoryBaseUnit).IsRequired();
        builder.Property(x => x.IsSellable).IsRequired();
        builder.Property(x => x.IsOpenPackAllowed).IsRequired();
        builder.Property(x => x.IsWeightBased).IsRequired();
        builder.Property(x => x.QuantityPrecision).IsRequired();

        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.HasIndex("ProductId", nameof(ProductUnit.Code))
            .IsUnique()
            .HasDatabaseName("UX_ProductUnits_ProductId_UnitCode");

        builder.HasIndex("ProductId")
            .IsUnique()
            .HasFilter("[IsInventoryBaseUnit] = 1")
            .HasDatabaseName("UX_ProductUnits_OneInventoryBaseUnit");
    }
}