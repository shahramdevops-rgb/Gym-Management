namespace Gym.Application.Shop.RenameShopCategory;

/// <param name="Version">The <c>version</c> the form was read with; a stale one is refused.</param>
public sealed record RenameShopCategoryCommand(string Name, uint Version);
