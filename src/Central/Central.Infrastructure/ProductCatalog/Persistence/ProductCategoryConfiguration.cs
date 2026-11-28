using Central.Domain.ProductCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Central.Infrastructure.ProductCatalog.Persistence;

public sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.ToTable("ProductCategories", "catalog", table =>
        {
            table.HasCheckConstraint("CK_ProductCategories_DisplayOrder", "[DisplayOrder] >= 0");
            table.HasCheckConstraint(
                "CK_ProductCategories_NotOwnParent",
                "[ParentCategoryId] IS NULL OR [ParentCategoryId] <> [ProductCategoryId]");
        });

        builder.HasKey(x => x.Id).HasName("PK_ProductCategories");
        builder.Property(x => x.Id)
            .HasColumnName("ProductCategoryId")
            .ValueGeneratedNever();

        builder.Property(x => x.ParentCategoryId);
        builder.Property(x => x.CategoryCode)
            .HasMaxLength(50)
            .IsRequired();
        builder.HasIndex(x => x.CategoryCode)
            .IsUnique()
            .HasDatabaseName("UX_ProductCategories_CategoryCode");

        builder.Property(x => x.Name)
            .HasMaxLength(150)
            .IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.DisplayOrder).HasDefaultValue(0).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();

        builder.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime2(3)").IsRequired();
        builder.Property<Guid>("CreatedByUserId").IsRequired();
        builder.Property<DateTime?>("UpdatedAtUtc").HasColumnType("datetime2(3)");
        builder.Property<Guid?>("UpdatedByUserId");
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.HasOne<ProductCategory>()
            .WithMany()
            .HasForeignKey(x => x.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
