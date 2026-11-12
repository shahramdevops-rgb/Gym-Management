using Gym.Application.Shop;
using Gym.Domain.Shop;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>shop_categories</c> table (BUSINESS_RULES.md §16 <i>Products</i>), the online shop's own,
/// apart from the cafe's <c>product_categories</c>.
/// </summary>
public sealed class ShopCategoryConfiguration : IEntityTypeConfiguration<ShopCategory>
{
    public void Configure(EntityTypeBuilder<ShopCategory> builder)
    {
        builder.ToTable("shop_categories", table =>
            table.HasCheckConstraint("ck_shop_categories_name_not_blank", "btrim(name) <> ''"));

        builder.Property(category => category.Name).HasMaxLength(ShopCategory.NameMaxLength).IsRequired();
        builder.Property(category => category.NormalizedName).HasMaxLength(ShopCategory.NameMaxLength).IsRequired();

        builder.HasIndex(category => category.NormalizedName)
            .IsUnique()
            .HasDatabaseName(ShopCatalogConstraints.UniqueCategoryName);

        builder.Property(category => category.Version).IsRowVersion();
    }
}

/// <summary>
/// The <c>shop_products</c> table and its two child tables. The rules live in
/// <see cref="ShopProduct"/>; the database repeats each one it can check.
/// </summary>
/// <remarks>
/// The positions (and the variants' labels) are unique per product by constraints the migration
/// <c>AddShopCatalog</c> writes by hand, <c>DEFERRABLE INITIALLY DEFERRED</c>: a reorder passes
/// through two rows on one position, which a constraint checked row by row would refuse. EF Core
/// cannot describe a deferred unique constraint, so they are not in this model.
/// </remarks>
public sealed class ShopProductConfiguration : IEntityTypeConfiguration<ShopProduct>
{
    public void Configure(EntityTypeBuilder<ShopProduct> builder)
    {
        builder.ToTable("shop_products", table =>
        {
            table.HasCheckConstraint("ck_shop_products_name_not_blank", "btrim(name) <> ''");
            table.HasCheckConstraint("ck_shop_products_description_not_blank", "description IS NULL OR btrim(description) <> ''");
        });

        builder.Property(product => product.Name).HasMaxLength(ShopProduct.NameMaxLength).IsRequired();
        builder.Property(product => product.NormalizedName).HasMaxLength(ShopProduct.NameMaxLength).IsRequired();
        builder.Property(product => product.Description).HasMaxLength(ShopProduct.DescriptionMaxLength);

        builder.HasIndex(product => product.NormalizedName)
            .IsUnique()
            .HasDatabaseName(ShopCatalogConstraints.UniqueProductName);

        // Restrict: a category is deleted only while empty (DeleteShopCategoryHandler names it).
        builder.HasOne<ShopCategory>()
            .WithMany()
            .HasForeignKey(product => product.CategoryId)
            .HasConstraintName(ShopCatalogConstraints.ProductCategoryForeignKey)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(product => product.CategoryId);

        // The product owns its variants and photos: removing one from the product deletes its row.
        builder.HasMany(product => product.Variants)
            .WithOne()
            .HasForeignKey(variant => variant.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(product => product.Variants).HasField("_variants").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(product => product.Photos)
            .WithOne()
            .HasForeignKey(photo => photo.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(product => product.Photos).HasField("_photos").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(product => product.Version).IsRowVersion();
    }
}

/// <summary>The <c>shop_variants</c> table: a label, a price above zero, stock never below zero.</summary>
public sealed class ShopVariantConfiguration : IEntityTypeConfiguration<ShopVariant>
{
    public void Configure(EntityTypeBuilder<ShopVariant> builder)
    {
        builder.ToTable("shop_variants", table =>
        {
            table.HasCheckConstraint("ck_shop_variants_price_positive", "price > 0");
            table.HasCheckConstraint("ck_shop_variants_stock_range", $"stock BETWEEN 0 AND {ShopVariant.MaxStock}");
            table.HasCheckConstraint("ck_shop_variants_position_range", $"position BETWEEN 0 AND {ShopProduct.MaxVariants - 1}");
            table.HasCheckConstraint("ck_shop_variants_label_trimmed", "label = btrim(label)");
        });

        // The id is made in C# (Guid v7) when the product adds a variant; without this EF Core takes a
        // new variant with an id for an existing row and updates nothing (as CoachingPeriodConfiguration).
        builder.Property(variant => variant.Id).ValueGeneratedNever();

        builder.Property(variant => variant.Label).HasMaxLength(ShopVariant.LabelMaxLength).IsRequired();
        builder.Property(variant => variant.NormalizedLabel).HasMaxLength(ShopVariant.LabelMaxLength).IsRequired();

        // numeric(18,2): exact decimal arithmetic, never float (CLAUDE.md).
        builder.Property(variant => variant.Price).HasPrecision(18, ShopVariant.PriceDecimals);
    }
}

/// <summary>The <c>shop_photos</c> table: at most eight per product, positions 0 to 7.</summary>
public sealed class ShopPhotoConfiguration : IEntityTypeConfiguration<ShopPhoto>
{
    public void Configure(EntityTypeBuilder<ShopPhoto> builder)
    {
        builder.ToTable("shop_photos", table =>
        {
            // With the deferred unique (product_id, position), at most MaxPhotos rows per product.
            table.HasCheckConstraint("ck_shop_photos_position_range", $"position BETWEEN 0 AND {ShopProduct.MaxPhotos - 1}");
            table.HasCheckConstraint("ck_shop_photos_size_positive", "width > 0 AND height > 0");
        });

        // The same as the variants: a photo added to an existing product is a new row.
        builder.Property(photo => photo.Id).ValueGeneratedNever();

        // Made from the id, so not stored.
        builder.Ignore(photo => photo.LargeKey);
        builder.Ignore(photo => photo.SmallKey);
    }
}
