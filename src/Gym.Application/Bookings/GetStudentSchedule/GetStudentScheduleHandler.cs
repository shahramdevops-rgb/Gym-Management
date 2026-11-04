using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Domain.Common;

namespace Gym.Application.Bookings.GetStudentSchedule;

/// <summary>The trainer opens a student's sessions (BUSINESS_RULES.md §15 <i>Students in sessions</i>): only a student of theirs now.</summary>
public sealed class GetStudentScheduleHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<StudentScheduleResponse>> Handle(Guid siteAccountId, Guid memberId, CancellationToken cancellationToken)
    {
        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<StudentScheduleResponse>(trainer.Error);
        }

        var link = await StudentSessions.LinkAsync(db, trainer.Value.Id, memberId, cancellationToken);
        if (link.IsFailure)
        {
            return Result.Failure<StudentScheduleResponse>(link.Error);
        }

        return await StudentScheduleResponse.ForAsync(db, link.Value, calendar.Today(), calendar.TimeOfDay(), cancellationToken);
    }
}
