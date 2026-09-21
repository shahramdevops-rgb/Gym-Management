using Gym.Application.Common;
using Gym.Application.Payments;
using Gym.Domain.Common;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Subscriptions.CancelSubscription;

/// <summary>
/// Cancels a subscription whatever its status (BUSINESS_RULES.md §4 Cancel). Owner only.
/// Payments are untouched and queued subscriptions are not moved earlier.
/// </summary>
public sealed class CancelSubscriptionHandler(IAppDbContext db, IGymCalendar calendar, TimeProvider time)
{
    public async Task<Result<SubscriptionResponse>> Handle(Guid id, CancelSubscriptionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var subscription = await db.Subscriptions.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (subscription is null)
        {
            return Result.Failure<SubscriptionResponse>(SubscriptionErrors.NotFound);
        }

        var cancelled = subscription.Cancel(command.Reason, time.GetUtcNow());
        if (cancelled.IsFailure)
        {
            return Result.Failure<SubscriptionResponse>(cancelled.Error);
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

        var planName = await PlanNames.ForAsync(db, subscription.PlanId, cancellationToken);

        return SubscriptionResponse.From(subscription, planName, calendar.Today(), netPaid);
    }
}
