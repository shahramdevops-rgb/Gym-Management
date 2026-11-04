using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Domain.Common;

namespace Gym.Application.Bookings.GetMyCancellations;

/// <summary>The member opens their home: the sessions their trainer cancelled (BUSINESS_RULES.md §15 <i>Timetable and attendance</i>).</summary>
public sealed class GetMyCancellationsHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<MyCancellationsResponse>> Handle(Guid siteAccountId, CancellationToken cancellationToken)
    {
        var member = await CoachingAccess.MemberAsync(db, siteAccountId, cancellationToken);
        if (member.IsFailure)
        {
            return Result.Failure<MyCancellationsResponse>(member.Error);
        }

        return await MyCancellationsResponse.ForAsync(db, member.Value.Id, calendar.Today(), cancellationToken);
    }
}
