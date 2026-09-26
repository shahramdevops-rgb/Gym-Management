using Gym.Application.Cafe;
using Gym.Domain.Cafe;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>products</c> table. The rules live in <see cref="Product"/>; the database repeats every
/// one it can check, so a bug or a hand-written SQL statement cannot store a product the app
/// would reject.
/// </summary>
/// <remarks>
/// There is no stock column here on purpose (BUSINESS_RULES.md §8): the gym does not count what
/// is in the fridge.
/// </remarks>
public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", table =>
        {
            table.HasCheckConstraint("ck_products_name_not_blank", "btrim(name) <> ''");
            table.HasCheckConstraint("ck_products_price_not_negative", "price >= 0");
        });

        builder.Property(product => product.Name).HasMaxLength(Product.NameMaxLength).IsRequired();
        builder.Property(product => product.NormalizedName).HasMaxLength(Product.NameMaxLength).IsRequired();

        // numeric(18,2): exact decimal arithmetic, never float (CLAUDE.md).
        builder.Property(product => product.Price).HasPrecision(18, Product.PriceDecimals);

        // Unique across the whole cafe, inactive products included, in normalized form
        // (BUSINESS_RULES.md §8) — not merely within one category.
        builder.HasIndex(product => product.NormalizedName)
            .IsUnique()
            .HasDatabaseName(CafeConstraints.UniqueProductName);

        // Restrict, and named explicitly because DeleteProductCategoryHandler recognises this
        // constraint by name: it is the database's half of "a category is deleted only while it
        // is empty". Without it, deleting a category would cascade away part of the price list.
        builder.HasOne<ProductCategory>()
            .WithMany()
            .HasForeignKey(product => product.CategoryId)
            .HasConstraintName(CafeConstraints.ProductCategoryForeignKey)
            .OnDelete(DeleteBehavior.Restrict);

        // The price list is read by category on every till screen.
        builder.HasIndex(product => product.CategoryId);

        builder.Property(product => product.Version).IsRowVersion();
    }
}
