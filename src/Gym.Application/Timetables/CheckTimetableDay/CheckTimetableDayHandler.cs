using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Application.Timetables.SetTimetableDay;
using Gym.Domain.Common;
using Gym.Domain.Timetables;

namespace Gym.Application.Timetables.CheckTimetableDay;

/// <summary>
/// What «فقط این روز» would do to that date's students, before the trainer saves it (BUSINESS_RULES.md
/// §15 <i>Students in sessions</i>). A change the save would refuse is refused here.
/// </summary>
public sealed class CheckTimetableDayHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<TimetableImpactResponse>> Handle(
        Guid siteAccountId, DateOnly date, SetTimetableDayCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<TimetableImpactResponse>(trainer.Error);
        }

        var trainerId = trainer.Value.Id;
        var timetable = await TrainerTimetable.DatesAsync(db, trainerId, date, date, cancellationToken);
        var next = command.Sessions.Select(item => new PlannedSession(item.Start, item.End, item.Capacity, item.IsVip, item.Note)).ToList();

        var allowed = TimetableRules.CheckChange(date, timetable.SessionsOn(date).ToList(), next, calendar.Today(), calendar.TimeOfDay());
        if (allowed.IsFailure)
        {
            return Result.Failure<TimetableImpactResponse>(allowed.Error);
        }

        return await TimetableImpactResponse.ForAsync(db, trainerId, date, date, timetable, _ => next, cancellationToken);
    }
}
