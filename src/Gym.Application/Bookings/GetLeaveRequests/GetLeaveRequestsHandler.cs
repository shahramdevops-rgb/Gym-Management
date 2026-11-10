using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Domain.Common;

namespace Gym.Application.Bookings.GetLeaveRequests;

/// <summary>The trainer's «درخواست‌های مرخصی» (BUSINESS_RULES.md §15 <i>Leave by request</i>, task 15.7i).</summary>
public sealed class GetLeaveRequestsHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<LeaveRequestsResponse>> Handle(Guid siteAccountId, CancellationToken cancellationToken)
    {
        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<LeaveRequestsResponse>(trainer.Error);
        }

        return await LeaveRequestsResponse.ForAsync(db, trainer.Value.Id, calendar.Today(), calendar.TimeOfDay(), cancellationToken);
    }
}
