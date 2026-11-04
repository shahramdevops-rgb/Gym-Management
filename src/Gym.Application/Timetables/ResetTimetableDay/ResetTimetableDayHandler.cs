using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Application.Notifications.SendSessionCancelledSms;
using Gym.Domain.Common;
using Gym.Domain.Timetables;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Timetables.ResetTimetableDay;

/// <summary>
/// «برگرداندن به الگو» (BUSINESS_RULES.md §15 <i>The trainer's sessions</i>): the date the trainer
/// changed takes the pattern's sessions again, under the same rules as any change, so a session of
/// today that has started stays. A date that was never changed is answered as it is. The students of
/// a session it removes or moves are told (task 15.6c).
/// </summary>
/// <remarks>Under the trainer's row lock, as <c>SetWeekPatternHandler</c>.</remarks>
public sealed class ResetTimetableDayHandler(IAppDbContext db, IGymCalendar calendar, SessionCancelledSms sessionCancelledSms)
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
        var timeOfDay = calendar.TimeOfDay();
        var cancelled = CancelledSessions.None;
        var day = await db.TimetableDays.Include(row => row.Sessions)
            .SingleOrDefaultAsync(row => row.TrainerId == trainerId && row.Date == date, cancellationToken);

        if (day is not null)
        {
            var patterns = await TrainerTimetable.PatternsFromAsync(db, trainerId, date, cancellationToken);
            var patternSessions = TimetableRules.SessionsOn(date, patterns, changedDay: null).ToList();

            var allowed = day.CheckReset(patternSessions, today, timeOfDay);
            if (allowed.IsFailure)
            {
                return Result.Failure<TimetableResponse>(allowed.Error);
            }

            var before = await TrainerTimetable.DatesAsync(db, trainerId, date, date, cancellationToken);

            // A timetable is not a financial record: the change really goes, its sessions with it.
            db.TimetableDays.Remove(day);
            await db.SaveChangesAsync(cancellationToken);

            cancelled = await TimetableCancellations.RecordAsync(db, trainerId, date, date, before, today, timeOfDay, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        await sessionCancelledSms.QueueAsync(cancelled, cancellationToken);

        return await TimetableResponse.ForAsync(db, trainerId, today, cancellationToken);
    }
}
