namespace Gym.Application.Cafe.UpdateProduct;

/// <param name="Version">The <c>version</c> from the product as it was read before editing.</param>
public sealed record UpdateProductCommand(string Name, Guid CategoryId, decimal Price, uint Version);
