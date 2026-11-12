using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Application.Timetables.SetWeekPattern;
using Gym.Domain.Common;
using Gym.Domain.Timetables;

namespace Gym.Application.Timetables.CheckWeekPattern;

/// <summary>
/// What «ذخیره الگو» would do to the students, before the trainer presses it (BUSINESS_RULES.md §15
/// <i>Students in sessions</i>). From its date on, every date takes the new pattern's sessions,
/// except the dates the trainer changed on their own. A pattern the save would refuse is refused here.
/// </summary>
public sealed class CheckWeekPatternHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<TimetableImpactResponse>> Handle(Guid siteAccountId, SetWeekPatternCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<TimetableImpactResponse>(trainer.Error);
        }

        var trainerId = trainer.Value.Id;
        var today = calendar.Today();
        var sessions = command.Sessions.Select(item => new WeeklySession(item.Day, item.Start, item.End, item.Capacity, item.IsVip, item.Note)).ToList();
        var pattern = WeekPattern.Create(trainerId, command.StartsOn, sessions, today);
        if (pattern.IsFailure)
        {
            return Result.Failure<TimetableImpactResponse>(pattern.Error);
        }

        var lastDay = TimetableRules.LastDay(today);
        var timetable = await TrainerTimetable.DatesAsync(db, trainerId, command.StartsOn, lastDay, cancellationToken);

        IReadOnlyList<PlannedSession> After(DateOnly date) =>
            timetable.Changed.ContainsKey(date) ? timetable.SessionsOn(date) : pattern.Value.SessionsOn(date.DayOfWeek).ToList();

        return await TimetableImpactResponse.ForAsync(db, trainerId, command.StartsOn, lastDay, timetable, After, cancellationToken);
    }
}
