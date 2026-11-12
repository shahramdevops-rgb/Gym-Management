using Gym.Domain.Shop;

namespace Gym.Application.Shop.UpdateShopProduct;

/// <param name="Variants">
/// The whole list in its new order: an <c>id</c> keeps and changes that variant, a null one adds a
/// variant, and one left out is removed.
/// </param>
/// <param name="Version">The <c>version</c> the form was read with; a stale one is refused.</param>
public sealed record UpdateShopProductCommand(
    string Name,
    string? Description,
    Guid CategoryId,
    bool IsVisible,
    IReadOnlyList<ShopVariantInput> Variants,
    uint Version);
