using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Domain.Common;

namespace Gym.Application.Bookings.GetMySessions;

/// <summary>The member opens «سانس‌های من» (BUSINESS_RULES.md §15 <i>Students in sessions</i>).</summary>
public sealed class GetMySessionsHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<MySessionsResponse>> Handle(Guid siteAccountId, CancellationToken cancellationToken)
    {
        var member = await CoachingAccess.MemberAsync(db, siteAccountId, cancellationToken);
        if (member.IsFailure)
        {
            return Result.Failure<MySessionsResponse>(member.Error);
        }

        return await MySessionsResponse.ForAsync(db, member.Value.Id, calendar.Today(), calendar.TimeOfDay(), cancellationToken);
    }
}
