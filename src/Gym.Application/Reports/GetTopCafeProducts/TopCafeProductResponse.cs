namespace Gym.Application.Reports.GetTopCafeProducts;

/// <summary>One of the cafe's best sellers in a range (BUSINESS_RULES.md §12 <i>Operational reports</i>).</summary>
/// <param name="Name">The product's name today: a renamed product is shown by its new name.</param>
/// <param name="Quantity">How many were sold, cancelled orders left out.</param>
/// <param name="Amount">What they were sold for, at the prices of the day they were sold.</param>
public sealed record TopCafeProductResponse(Guid ProductId, string Name, int Quantity, decimal Amount);
