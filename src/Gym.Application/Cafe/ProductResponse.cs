using Gym.Domain.Cafe;

namespace Gym.Application.Cafe;

/// <param name="CategoryName">
/// Carried on the product so the price list can be grouped without a second request. It is the
/// category's current name, not a snapshot: only an order snapshots anything (BUSINESS_RULES.md §8).
/// </param>
/// <param name="IsActive">The product's own switch: موجود when true.</param>
/// <param name="CategoryIsActive">
/// Its category's switch. Sent because a product can be switched on inside a shelf that is
/// switched off, and the management screen has to be able to say so rather than showing a
/// موجود item the till refuses to offer.
/// </param>
/// <param name="IsSellable">
/// What the till actually asks: switched on, in a category that is switched on. Computed here
/// rather than left to each screen, so "sellable" means one thing everywhere.
/// </param>
/// <param name="Version">Sent back with an update, so a stale edit is refused.</param>
public sealed record ProductResponse(
    Guid Id,
    string Name,
    Guid CategoryId,
    string CategoryName,
    decimal Price,
    bool IsActive,
    bool CategoryIsActive,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt)
{
    public bool IsSellable => IsActive && CategoryIsActive;

    public static ProductResponse From(Product product, ProductCategory category)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(category);

        return new ProductResponse(
            product.Id,
            product.Name,
            product.CategoryId,
            category.Name,
            product.Price,
            product.IsActive,
            category.IsActive,
            product.Version,
            product.CreatedAt,
            product.UpdatedAt);
    }
}
