using Gym.Domain.Shop;

namespace Gym.Application.Shop.CreateShopProduct;

/// <param name="Variants">1 to 20, in the order shown; every <c>id</c> null for a new product.</param>
public sealed record CreateShopProductCommand(
    string Name,
    string? Description,
    Guid CategoryId,
    bool IsVisible,
    IReadOnlyList<ShopVariantInput> Variants);
