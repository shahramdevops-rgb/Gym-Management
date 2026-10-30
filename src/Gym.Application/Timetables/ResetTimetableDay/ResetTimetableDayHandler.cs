using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Timetables;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Timetables.ResetTimetableDay;

/// <summary>
/// «برگرداندن به الگو» (BUSINESS_RULES.md §15 <i>The trainer's sessions</i>): the date the trainer
/// changed takes the pattern's sessions again, under the same rules as any change, so a session of
/// today that has started stays. A date that was never changed is answered as it is.
/// </summary>
/// <remarks>Under the trainer's row lock, as <c>SetWeekPatternHandler</c>.</remarks>
public sealed class ResetTimetableDayHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<TimetableResponse>> Handle(Guid siteAccountId, DateOnly date, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<TimetableResponse>(trainer.Error);
        }

        var trainerId = trainer.Value.Id;
        await db.LockTrainerAsync(trainerId, cancellationToken);

        var today = calendar.Today();
        var day = await db.TimetableDays.Include(row => row.Sessions)
            .SingleOrDefaultAsync(row => row.TrainerId == trainerId && row.Date == date, cancellationToken);

        if (day is not null)
        {
            var patterns = await TrainerTimetable.PatternsFromAsync(db, trainerId, date, cancellationToken);
            var patternSessions = TimetableRules.SessionsOn(date, patterns, changedDay: null).ToList();

            var allowed = day.CheckReset(patternSessions, today, calendar.TimeOfDay());
            if (allowed.IsFailure)
            {
                return Result.Failure<TimetableResponse>(allowed.Error);
            }

            // A timetable is not a financial record: the change really goes, its sessions with it.
            db.TimetableDays.Remove(day);
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return await TimetableResponse.ForAsync(db, trainerId, today, cancellationToken);
    }
}
