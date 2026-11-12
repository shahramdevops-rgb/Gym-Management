using Gym.Domain.Common;

namespace Gym.Domain.Shop;

/// <summary>
/// A product's photo (BUSINESS_RULES.md §16 <i>Products</i>): re-encoded to WebP on upload, stored
/// on the files volume under names made from <see cref="Entity.Id"/>, public to anyone (ADR 0011).
/// The first by <see cref="Position"/> is the product's main photo.
/// </summary>
/// <remarks>
/// The row holds what the page needs to lay a photo out before it loads (its size); the bytes live in
/// two files, the photo and a small copy for lists (<see cref="LargeKey"/>, <see cref="SmallKey"/>).
/// An id never gets other bytes, so a photo's address can be cached for good.
/// </remarks>
public sealed class ShopPhoto : Entity
{
    // For EF Core.
    private ShopPhoto()
    {
    }

    public Guid ProductId { get; private set; }

    /// <summary>From 0; 0 is the main photo.</summary>
    public int Position { get; private set; }

    /// <summary>The stored (large) photo's width in pixels.</summary>
    public int Width { get; private set; }

    /// <summary>The stored (large) photo's height in pixels.</summary>
    public int Height { get; private set; }

    /// <summary>The file store's key of the photo, at most 1600 pixels on its long side.</summary>
    public string LargeKey => KeyOf(Id, "large");

    /// <summary>The file store's key of the small copy for lists, 480 pixels.</summary>
    public string SmallKey => KeyOf(Id, "small");

    public static string KeyOf(Guid photoId, string size) => $"shop/{photoId:N}-{size}.webp";

    internal static ShopPhoto Create(Guid productId, int position, int width, int height) =>
        new() { ProductId = productId, Position = position, Width = width, Height = height };

    internal void MoveTo(int position) => Position = position;
}
