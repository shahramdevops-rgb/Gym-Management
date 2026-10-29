using Gym.Application.Attendances.ListMemberAttendance;
using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Domain.Common;

namespace Gym.Application.SiteRecords.ListMyVisits;

/// <summary>The member's own check-ins, newest first, through the panel's history (BUSINESS_RULES.md §15).</summary>
public sealed class ListMyVisitsHandler(IAppDbContext db, ListMemberAttendanceHandler visits)
{
    public async Task<Result<PagedResponse<MyVisitResponse>>> Handle(
        Guid siteAccountId, ListMemberAttendanceQuery query, CancellationToken cancellationToken)
    {
        var member = await CoachingAccess.MemberAsync(db, siteAccountId, cancellationToken);
        if (member.IsFailure)
        {
            return Result.Failure<PagedResponse<MyVisitResponse>>(member.Error);
        }

        var page = await visits.Handle(member.Value.Id, query, cancellationToken);

        return page.IsFailure
            ? Result.Failure<PagedResponse<MyVisitResponse>>(page.Error)
            : SitePages.Map(page.Value, MyVisitResponse.From);
    }
}
