using Gym.Domain.Common;

namespace Gym.Domain.Cafe;

/// <summary>
/// One line of a cafe order: how many of what, at the price it cost that day
/// (BUSINESS_RULES.md §8).
/// </summary>
/// <remarks>
/// <see cref="ProductName"/> and <see cref="UnitPrice"/> are snapshots, copied at the till the
/// same way a subscription copies its plan's figures (§4). That copy is what makes editing a
/// product safe: yesterday's order keeps yesterday's price and yesterday's name, whatever the
/// price list says today. <see cref="ProductId"/> stays for the reports that count which products
/// sell (§12), which is why a product is switched off and never deleted.
/// </remarks>
public sealed class CafeOrderItem : Entity
{
    /// <summary>Decided during task 7.2: nobody buys 1,000 of one thing at a gym counter.</summary>
    public const int MaxQuantity = 999;

    // For EF Core.
    private CafeOrderItem()
    {
    }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    /// <summary>The product's name when it was sold. Never updated.</summary>
    public string ProductName { get; private set; } = string.Empty;

    /// <summary>The product's price when it was sold. Never updated.</summary>
    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>
    /// <see cref="UnitPrice"/> × <see cref="Quantity"/>, stored rather than computed on read: it
    /// is what the customer was charged, and a stored figure cannot be re-derived differently by
    /// a later rounding rule.
    /// </summary>
    public decimal LineTotal { get; private set; }

    internal static Result<CafeOrderItem> Create(Product product, int quantity)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (quantity is < 1 or > MaxQuantity)
        {
            return Result.Failure<CafeOrderItem>(CafeOrderErrors.QuantityInvalid);
        }

        return new CafeOrderItem
        {
            ProductId = product.Id,
            ProductName = product.Name,
            UnitPrice = product.Price,
            Quantity = quantity,
            LineTotal = product.Price * quantity,
        };
    }

    internal void AttachTo(Guid orderId) => OrderId = orderId;
}
