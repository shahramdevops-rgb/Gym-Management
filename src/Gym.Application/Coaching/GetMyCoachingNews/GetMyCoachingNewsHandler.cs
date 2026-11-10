using Gym.Application.Common;
using Gym.Domain.Common;

namespace Gym.Application.Coaching.GetMyCoachingNews;

/// <summary>«خبر مربی» on the member's home (BUSINESS_RULES.md §15, task 15.4d).</summary>
public sealed class GetMyCoachingNewsHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<CoachingNewsResponse>> Handle(Guid siteAccountId, CancellationToken cancellationToken)
    {
        var member = await CoachingAccess.MemberAsync(db, siteAccountId, cancellationToken);
        if (member.IsFailure)
        {
            return Result.Failure<CoachingNewsResponse>(member.Error);
        }

        return await CoachingNewsResponse.ForMemberAsync(db, member.Value.Id, calendar.Today(), cancellationToken);
    }
}
