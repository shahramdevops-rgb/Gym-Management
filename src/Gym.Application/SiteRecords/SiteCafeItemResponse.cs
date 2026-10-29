using Gym.Application.Cafe;

namespace Gym.Application.SiteRecords;

/// <summary>One line of a cafe purchase, as the till recorded its name and price.</summary>
public sealed record SiteCafeItemResponse(string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal)
{
    public static SiteCafeItemResponse From(CafeOrderItemResponse item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new SiteCafeItemResponse(item.ProductName, item.UnitPrice, item.Quantity, item.LineTotal);
    }
}
