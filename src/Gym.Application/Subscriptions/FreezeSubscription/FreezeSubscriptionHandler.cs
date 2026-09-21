using Gym.Application.Common;
using Gym.Application.Payments;
using Gym.Domain.Common;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Subscriptions.FreezeSubscription;

/// <summary>Starts a freeze today (BUSINESS_RULES.md §4 Freeze). Owner only.</summary>
public sealed class FreezeSubscriptionHandler(IAppDbContext db, IGymCalendar calendar, ISubscriptionPolicy policy)
{
    public async Task<Result<SubscriptionResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        var subscription = await db.Subscriptions.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (subscription is null)
        {
            return Result.Failure<SubscriptionResponse>(SubscriptionErrors.NotFound);
        }

        var today = calendar.Today();

        // No cross-row effect and no date change, so the xmin token on this one row is enough:
        // no member lock, no transaction of its own beyond the implicit one SaveChangesAsync uses.
        var frozen = subscription.Freeze(today, policy.MaxFreezeDays);
        if (frozen.IsFailure)
        {
            return Result.Failure<SubscriptionResponse>(frozen.Error);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<SubscriptionResponse>(SubscriptionErrors.ChangedConcurrently);
        }

        var netPaid = await PaymentLedger.GetNetPaidAsync(db, id, cancellationToken);

        return SubscriptionResponse.From(subscription, today, netPaid);
    }
}
