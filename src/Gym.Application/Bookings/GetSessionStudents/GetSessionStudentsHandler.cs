using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Timetables;

namespace Gym.Application.Bookings.GetSessionStudents;

/// <summary>The trainer opens a date's sessions with their students (BUSINESS_RULES.md §15 <i>Students in sessions</i>): today to the timetable's last day.</summary>
public sealed class GetSessionStudentsHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<SessionStudentsResponse>> Handle(Guid siteAccountId, DateOnly date, CancellationToken cancellationToken)
    {
        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<SessionStudentsResponse>(trainer.Error);
        }

        var today = calendar.Today();
        if (date < today || date > TimetableRules.LastDay(today))
        {
            return Result.Failure<SessionStudentsResponse>(TimetableErrors.DayOutOfRange);
        }

        return await SessionStudentsResponse.ForAsync(db, trainer.Value.Id, date, today, calendar.TimeOfDay(), cancellationToken);
    }
}
