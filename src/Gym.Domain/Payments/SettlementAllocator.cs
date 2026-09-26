using Gym.Domain.Common;

namespace Gym.Domain.Payments;

/// <summary>One item the desk ticked for a settlement, with what is still owed on it.</summary>
/// <param name="Date">
/// The subscription's start date, the day of the visit that was charged, or the day of the cafe
/// sale: what "oldest first" is measured by within one kind.
/// </param>
public sealed record SettlementItem(PaymentTargetKind Kind, Guid Id, DateOnly Date, decimal Outstanding);

/// <summary>What one item receives out of the settlement's amount.</summary>
public sealed record SettlementShare(SettlementItem Item, decimal Amount);

/// <summary>
/// Spreads one amount of money over several owed items (BUSINESS_RULES.md §5 <i>Settling several
/// items at once</i>): cafe first, then service charges (هوازی), then subscriptions, oldest first
/// within a kind. Each item is paid in full before the next one gets anything, so only the last
/// item reached can end up part-paid.
/// </summary>
/// <remarks>
/// Pure arithmetic with no database, so the order — which decides what stays owed when the member
/// hands over less than everything — is pinned down by unit tests rather than by an integration
/// test that would have to set up three kinds of debt.
/// </remarks>
public static class SettlementAllocator
{
    public static Result<IReadOnlyList<SettlementShare>> Allocate(IReadOnlyCollection<SettlementItem> items, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            return Result.Failure<IReadOnlyList<SettlementShare>>(SettlementErrors.NoItems);
        }

        // Only owed items reach here: the caller took them from the member's debt, which leaves
        // out anything paid in full. A zero would mean that list was built wrong.
        if (items.Any(item => item.Outstanding <= 0))
        {
            throw new ArgumentException("Every settlement item must still owe something.", nameof(items));
        }

        var amountError = Payment.CheckAmount(amount);
        if (amountError is not null)
        {
            return Result.Failure<IReadOnlyList<SettlementShare>>(amountError);
        }

        if (amount > items.Sum(item => item.Outstanding))
        {
            return Result.Failure<IReadOnlyList<SettlementShare>>(PaymentErrors.Overpayment);
        }

        var shares = new List<SettlementShare>();
        var left = amount;
        foreach (var item in items.OrderBy(item => Priority(item.Kind)).ThenBy(item => item.Date).ThenBy(item => item.Id))
        {
            if (left == 0)
            {
                break;
            }

            var share = Math.Min(left, item.Outstanding);
            shares.Add(new SettlementShare(item, share));
            left -= share;
        }

        return Result.Success<IReadOnlyList<SettlementShare>>(shares);
    }

    /// <summary>The order the developer gave: cafe, then هوازی, then the subscription.</summary>
    private static int Priority(PaymentTargetKind kind) => kind switch
    {
        PaymentTargetKind.CafeOrder => 0,
        PaymentTargetKind.ServiceCharge => 1,
        PaymentTargetKind.Subscription => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown payment target kind."),
    };
}
