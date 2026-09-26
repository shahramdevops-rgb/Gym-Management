namespace Gym.Application.Cafe.UpdateProductCategory;

/// <param name="Version">The <c>version</c> from the category as it was read before editing.</param>
public sealed record UpdateProductCategoryCommand(string Name, uint Version);
