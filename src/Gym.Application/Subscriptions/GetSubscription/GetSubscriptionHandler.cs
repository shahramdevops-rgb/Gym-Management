using Gym.Application.Common;
using Gym.Application.Payments;
using Gym.Domain.Common;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Subscriptions.GetSubscription;

/// <summary>One subscription, with its status and payment status as of the gym's today.</summary>
public sealed class GetSubscriptionHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<SubscriptionResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        // The status is calculated by the entity, so the row is loaded rather than projected.
        var subscription = await db.Subscriptions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (subscription is null)
        {
            return Result.Failure<SubscriptionResponse>(SubscriptionErrors.NotFound);
        }

        var netPaid = await PaymentLedger.GetNetPaidAsync(db, id, cancellationToken);

        return SubscriptionResponse.From(subscription, calendar.Today(), netPaid);
    }
}
