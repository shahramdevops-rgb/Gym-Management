using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Subscriptions.GetSubscription;

/// <summary>One subscription, with its status as of the gym's today.</summary>
public sealed class GetSubscriptionHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<SubscriptionResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        // The status is calculated by the entity, so the row is loaded rather than projected.
        var subscription = await db.Subscriptions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

        return subscription is null
            ? Result.Failure<SubscriptionResponse>(SubscriptionErrors.NotFound)
            : SubscriptionResponse.From(subscription, calendar.Today());
    }
}
