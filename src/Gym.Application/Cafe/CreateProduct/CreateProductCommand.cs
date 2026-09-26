namespace Gym.Application.Cafe.CreateProduct;

public sealed record CreateProductCommand(string Name, Guid CategoryId, decimal Price);
