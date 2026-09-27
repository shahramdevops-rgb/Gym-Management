using Gym.Application.Expenses;
using Gym.Domain.Expenses;
using Gym.Infrastructure.Persistence.Seed;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>expense_categories</c> table and its eight seeded rows. The rules live in
/// <see cref="ExpenseCategory"/>; the database repeats every one it can check.
/// </summary>
public sealed class ExpenseCategoryConfiguration : IEntityTypeConfiguration<ExpenseCategory>
{
    public void Configure(EntityTypeBuilder<ExpenseCategory> builder)
    {
        builder.ToTable("expense_categories", table =>
            table.HasCheckConstraint("ck_expense_categories_name_not_blank", "btrim(name) <> ''"));

        builder.Property(category => category.Name).HasMaxLength(ExpenseCategory.NameMaxLength).IsRequired();
        builder.Property(category => category.NormalizedName)
            .HasMaxLength(ExpenseCategory.NameMaxLength)
            .IsRequired();

        // Unique among all categories, in normalized form (BUSINESS_RULES.md §9).
        builder.HasIndex(category => category.NormalizedName)
            .IsUnique()
            .HasDatabaseName(ExpenseConstraints.UniqueCategoryName);

        builder.Property(category => category.Version).IsRowVersion();

        // Anonymous objects, because every setter on the entity is private. The normalized name
        // goes through the entity's own Normalize, so a seeded name is taken exactly as one the
        // Owner typed would be.
        builder.HasData(ExpenseCategorySeed.All.Select(seed => new
        {
            seed.Id,
            seed.Name,
            NormalizedName = ExpenseCategory.Normalize(seed.Name),
            ExpenseCategorySeed.CreatedAt,
        }));
    }
}
