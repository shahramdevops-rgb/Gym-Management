using Gym.Domain.Shop;

namespace Gym.Application.Shop;

/// <summary>A category with how many products it holds (a category is deleted only while empty).</summary>
public sealed record ShopCategoryResponse(Guid Id, string Name, int ProductCount, uint Version)
{
    public static ShopCategoryResponse From(ShopCategory category, int productCount) =>
        new(category.Id, category.Name, productCount, category.Version);
}

/// <summary>
/// One line of the shop admin's product list: what a card shows without opening the product. The
/// main photo is read at <c>/api/shop/photos/{id}/small</c>.
/// </summary>
public sealed record ShopProductListItem(
    Guid Id,
    string Name,
    Guid CategoryId,
    string CategoryName,
    bool IsVisible,
    Guid? MainPhotoId,
    decimal MinPrice,
    decimal MaxPrice,
    int TotalStock,
    int VariantCount);

public sealed record ShopVariantResponse(Guid Id, string Label, decimal Price, int Stock);

/// <summary>A photo's id and size; its files are at <c>/api/shop/photos/{id}/large</c> and <c>/small</c>.</summary>
public sealed record ShopPhotoResponse(Guid Id, int Width, int Height);

/// <summary>A whole product, as its form shows it.</summary>
public sealed record ShopProductResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid CategoryId,
    bool IsVisible,
    uint Version,
    IReadOnlyList<ShopVariantResponse> Variants,
    IReadOnlyList<ShopPhotoResponse> Photos)
{
    public static ShopProductResponse From(ShopProduct product) =>
        new(
            product.Id,
            product.Name,
            product.Description,
            product.CategoryId,
            product.IsVisible,
            product.Version,
            [.. product.Variants.Select(v => new ShopVariantResponse(v.Id, v.Label, v.Price, v.Stock))],
            [.. product.Photos.Select(p => new ShopPhotoResponse(p.Id, p.Width, p.Height))]);
}
