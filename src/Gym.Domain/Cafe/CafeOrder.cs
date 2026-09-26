using Gym.Domain.Common;

namespace Gym.Domain.Cafe;

/// <summary>
/// One sale at the cafe counter (BUSINESS_RULES.md §8): what was bought, for how much, and whose
/// account it went on if anyone's.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing is counted and nothing is reserved.</b> The gym keeps no stock, so creating an order
/// can never be refused for want of goods and cancelling one puts nothing back. What an order
/// holds is money owed and a record of what was handed over.
/// </para>
/// <para>
/// <b>An order is never edited.</b> Two of something that should have been one is a cancellation
/// and a fresh order, not a quantity corrected in place — §5's rule that financial records are
/// never edited, applied to the cafe. That is why there is no method here to add, remove or change
/// a line after <see cref="Create"/>.
/// </para>
/// <para>
/// <b>A walk-in order names no member</b> and must be paid in full at creation, because there is
/// no account to leave a balance on. An order on a member's account may be paid in part, in full,
/// or not at all, and whatever is left counts toward that member's debt (§5 <i>Member debt</i>).
/// Whether it has been paid is not stored here: it is the sum of its payments, calculated the same
/// way a subscription's payment status is.
/// </para>
/// </remarks>
public sealed class CafeOrder : Entity
{
    /// <summary>Decided during task 7.2: a counter order with more lines than this is a mistake.</summary>
    public const int MaxItems = 50;

    public const int CancelReasonMaxLength = 500;

    private readonly List<CafeOrderItem> _items = [];

    // For EF Core.
    private CafeOrder()
    {
    }

    /// <summary><c>null</c> for a walk-in customer (BUSINESS_RULES.md §8).</summary>
    public Guid? MemberId { get; private set; }

    /// <summary>The sum of the lines, stored: it is what the customer was charged.</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>A business date in the gym's time zone: the day of the sale, for reports.</summary>
    public DateOnly OrderedOn { get; private set; }

    public Guid PlacedByUserId { get; private set; }

    /// <summary>A moment (UTC); <c>null</c> while the order still stands.</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>Required whenever <see cref="CancelledAt"/> is set, and null otherwise.</summary>
    public string? CancelReason { get; private set; }

    public Guid? CancelledByUserId { get; private set; }

    /// <summary>Postgres <c>xmin</c>: two people cannot cancel the same order at once.</summary>
    public uint Version { get; private set; }

    public IReadOnlyList<CafeOrderItem> Items => _items;

    public bool IsCancelled => CancelledAt is not null;

    /// <summary>
    /// Rings up an order. The caller has already loaded the products, which is where "this product
    /// can be sold today" is answered: that is a question about the product and its category, two
    /// other tables (BUSINESS_RULES.md §8).
    /// </summary>
    /// <param name="lines">Each product with how many of it, in the order they were rung up.</param>
    public static Result<CafeOrder> Create(
        Guid? memberId,
        IReadOnlyList<(Product Product, int Quantity)> lines,
        DateOnly orderedOn,
        Guid placedByUserId)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count == 0)
        {
            return Result.Failure<CafeOrder>(CafeOrderErrors.NoItems);
        }

        if (lines.Count > MaxItems)
        {
            return Result.Failure<CafeOrder>(CafeOrderErrors.TooManyItems);
        }

        // Two lines of the same product would each be correct and the total would still be right,
        // but the receipt would read as two purchases of one thing. Combining them is the till's
        // job, not a thing to discover afterwards on a report.
        if (lines.Select(line => line.Product.Id).Distinct().Count() != lines.Count)
        {
            return Result.Failure<CafeOrder>(CafeOrderErrors.DuplicateProduct);
        }

        var order = new CafeOrder
        {
            MemberId = memberId,
            OrderedOn = orderedOn,
            PlacedByUserId = placedByUserId,
        };

        foreach (var (product, quantity) in lines)
        {
            var item = CafeOrderItem.Create(product, quantity);
            if (item.IsFailure)
            {
                return Result.Failure<CafeOrder>(item.Error);
            }

            item.Value.AttachTo(order.Id);
            order._items.Add(item.Value);
        }

        order.TotalAmount = order._items.Sum(item => item.LineTotal);

        return order;
    }

    /// <summary>
    /// Undoes the sale without erasing it (BUSINESS_RULES.md §8). A cancelled order owes nothing,
    /// so it leaves the member's debt; money already taken against it comes back as a refund,
    /// which the caller writes because payments live in another aggregate.
    /// </summary>
    public Result Cancel(string reason, DateTimeOffset now, Guid cancelledByUserId)
    {
        ArgumentNullException.ThrowIfNull(reason);

        if (IsCancelled)
        {
            return Result.Failure(CafeOrderErrors.AlreadyCancelled);
        }

        var cleanReason = reason.Trim();
        if (cleanReason.Length == 0)
        {
            return Result.Failure(CafeOrderErrors.CancelReasonRequired);
        }

        if (cleanReason.Length > CancelReasonMaxLength)
        {
            return Result.Failure(CafeOrderErrors.CancelReasonTooLong);
        }

        CancelledAt = now;
        CancelReason = cleanReason;
        CancelledByUserId = cancelledByUserId;

        return Result.Success();
    }

    /// <summary>
    /// What is still owed on this order: its total less what has been paid net of refunds, never
    /// below zero, and nothing at all once it is cancelled (BUSINESS_RULES.md §5 <i>Member debt</i>).
    /// The net paid figure is the caller's, because payments are not in this aggregate.
    /// </summary>
    public decimal Outstanding(decimal netPaid) =>
        IsCancelled ? 0m : Math.Max(0m, TotalAmount - netPaid);
}
