using Gym.Application.Cafe;
using Gym.Domain.Cafe;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>product_categories</c> table. The rules live in <see cref="ProductCategory"/>; the
/// database repeats every one it can check.
/// </summary>
public sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.ToTable("product_categories", table =>
            table.HasCheckConstraint("ck_product_categories_name_not_blank", "btrim(name) <> ''"));

        builder.Property(category => category.Name).HasMaxLength(ProductCategory.NameMaxLength).IsRequired();
        builder.Property(category => category.NormalizedName)
            .HasMaxLength(ProductCategory.NameMaxLength)
            .IsRequired();

        // The till lists the switched-on headings, and the products list joins on this column.
        builder.HasIndex(category => category.IsActive);

        // Unique among all categories, in normalized form (BUSINESS_RULES.md §8).
        builder.HasIndex(category => category.NormalizedName)
            .IsUnique()
            .HasDatabaseName(CafeConstraints.UniqueCategoryName);

        builder.Property(category => category.Version).IsRowVersion();
    }
}
