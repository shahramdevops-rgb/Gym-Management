namespace Gym.Application.Shop.ReorderShopPhotos;

/// <param name="PhotoIds">Every photo of the product once, in the new order; the first is the main photo.</param>
public sealed record ReorderShopPhotosCommand(IReadOnlyList<Guid> PhotoIds);
