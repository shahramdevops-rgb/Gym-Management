using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Members;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Subscriptions.AssignSubscription;

/// <summary>
/// Sells a member the plan the desk built for them — so many days, so many sessions — at today's
/// session price, starting today or queued after their latest subscription (BUSINESS_RULES.md §3,
/// §4). Owner and Staff.
/// </summary>
public sealed class AssignSubscriptionHandler(IAppDbContext db, SubscriptionSeller seller)
{
    public async Task<Result<SubscriptionResponse>> Handle(
        Guid memberId, AssignSubscriptionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var member = await db.Members.AsNoTracking().SingleOrDefaultAsync(m => m.Id == memberId, cancellationToken);
        if (member is null)
        {
            return Result.Failure<SubscriptionResponse>(MemberErrors.NotFound);
        }

        return await seller.SellMembershipAsync(member, command.DurationDays, command.SessionCount, cancellationToken);
    }
}
