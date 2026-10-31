using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Timetables;

namespace Gym.Application.Timetables.CheckResetTimetableDay;

/// <summary>
/// What «برگرداندن به الگو» would do to that date's students, before the trainer presses it
/// (BUSINESS_RULES.md §15 <i>Students in sessions</i>): the date takes the pattern's sessions again.
/// A date never changed touches no one.
/// </summary>
public sealed class CheckResetTimetableDayHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<TimetableImpactResponse>> Handle(Guid siteAccountId, DateOnly date, CancellationToken cancellationToken)
    {
        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<TimetableImpactResponse>(trainer.Error);
        }

        var trainerId = trainer.Value.Id;
        var timetable = await TrainerTimetable.DatesAsync(db, trainerId, date, date, cancellationToken);
        if (!timetable.Changed.TryGetValue(date, out var day))
        {
            return new TimetableImpactResponse(0, []);
        }

        var patternSessions = TimetableRules.SessionsOn(date, timetable.Patterns, changedDay: null);
        var allowed = day.CheckReset(patternSessions.ToList(), calendar.Today(), calendar.TimeOfDay());
        if (allowed.IsFailure)
        {
            return Result.Failure<TimetableImpactResponse>(allowed.Error);
        }

        return await TimetableImpactResponse.ForAsync(db, trainerId, date, date, timetable, _ => patternSessions, cancellationToken);
    }
}
