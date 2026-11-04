using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Application.Notifications.SendSessionCancelledSms;
using Gym.Domain.Common;
using Gym.Domain.Timetables;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Timetables.SetTimetableDay;

/// <summary>
/// The trainer changes one date on its own (BUSINESS_RULES.md §15 <i>The trainer's sessions</i>):
/// today or later within the 12 weeks; on today, a session that has started stays as it was. The
/// change outlives later patterns until «برگرداندن به الگو». The students of a session it removes or
/// moves are told (task 15.6c).
/// </summary>
/// <remarks>Under the trainer's row lock, as <c>SetWeekPatternHandler</c>.</remarks>
public sealed class SetTimetableDayHandler(IAppDbContext db, IGymCalendar calendar, SessionCancelledSms sessionCancelledSms)
{
    public async Task<Result<TimetableResponse>> Handle(Guid siteAccountId, DateOnly date, SetTimetableDayCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

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
        var sessions = command.Sessions.Select(item => new PlannedSession(item.Start, item.End, item.Capacity, item.IsVip)).ToList();

        var before = await TrainerTimetable.DatesAsync(db, trainerId, date, date, cancellationToken);
        var day = await db.TimetableDays.Include(row => row.Sessions)
            .SingleOrDefaultAsync(row => row.TrainerId == trainerId && row.Date == date, cancellationToken);

        Result changed;
        if (day is null)
        {
            // The first change of this date: what it had came from the pattern.
            var patterns = await TrainerTimetable.PatternsFromAsync(db, trainerId, date, cancellationToken);
            var current = TimetableRules.SessionsOn(date, patterns, changedDay: null).ToList();

            var created = TimetableDay.Create(trainerId, date, current, sessions, today, timeOfDay);
            if (created.IsSuccess)
            {
                db.TimetableDays.Add(created.Value);
            }

            changed = created;
        }
        else
        {
            changed = day.Change(sessions, today, timeOfDay);
        }

        if (changed.IsFailure)
        {
            return Result.Failure<TimetableResponse>(changed.Error);
        }

        await db.SaveChangesAsync(cancellationToken);

        var cancelled = await TimetableCancellations.RecordAsync(db, trainerId, date, date, before, today, timeOfDay, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await sessionCancelledSms.QueueAsync(cancelled, cancellationToken);

        return await TimetableResponse.ForAsync(db, trainerId, today, cancellationToken);
    }
}
