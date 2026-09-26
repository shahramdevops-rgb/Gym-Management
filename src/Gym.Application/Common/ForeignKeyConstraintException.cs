using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Common;

/// <summary>
/// A save broke a foreign key. Thrown by <c>AppDbContext</c> in place of the provider's own
/// exception, so a handler can recognise the one race it expects without Application knowing
/// that the database is Postgres — the same arrangement as <see cref="UniqueConstraintException"/>.
/// </summary>
/// <remarks>
/// Deleting a product category checks first that no product uses it. This covers what that check
/// cannot: a product added in another request between the check and the delete, where only the
/// database can decide, and the loser must be told the category is not empty instead of getting
/// a 500.
/// </remarks>
public sealed class ForeignKeyConstraintException(string constraintName, DbUpdateException original)
    : DbUpdateException(
        $"Foreign key constraint {constraintName} was violated.",
        original?.InnerException,
        original?.Entries ?? [])
{
    /// <summary>The constraint name, for example <c>fk_products_product_categories_category_id</c>.</summary>
    public string ConstraintName { get; } = constraintName;
}
