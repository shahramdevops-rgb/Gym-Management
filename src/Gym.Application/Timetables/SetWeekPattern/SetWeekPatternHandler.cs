using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Application.Notifications.SendSessionCancelledSms;
using Gym.Domain.Common;
using Gym.Domain.Timetables;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Timetables.SetWeekPattern;

/// <summary>
/// The trainer saves their weekly pattern from a date (BUSINESS_RULES.md §15 <i>The trainer's
/// sessions</i>): tomorrow at the earliest, and the days before it keep what they had. Any pattern
/// the trainer saved from that date or a later one is replaced by this one; the dates they changed
/// on their own keep their change. The students of a session it removes or moves are told (task 15.6c).
/// </summary>
/// <remarks>
/// Under the trainer's row lock, so a save from a second phone at the same moment waits, then
/// replaces this one, rather than both inserting a pattern for the same date.
/// </remarks>
public sealed class SetWeekPatternHandler(IAppDbContext db, IGymCalendar calendar, SessionCancelledSms sessionCancelledSms)
{
    public async Task<Result<TimetableResponse>> Handle(Guid siteAccountId, SetWeekPatternCommand command, CancellationToken cancellationToken)
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
        var sessions = command.Sessions.Select(item => new WeeklySession(item.Day, item.Start, item.End, item.Capacity, item.IsVip)).ToList();
        var created = WeekPattern.Create(trainerId, command.StartsOn, sessions, today);
        if (created.IsFailure)
        {
            return Result.Failure<TimetableResponse>(created.Error);
        }

        var lastDay = TimetableRules.LastDay(today);
        var before = await TrainerTimetable.DatesAsync(db, trainerId, command.StartsOn, lastDay, cancellationToken);

        // Deleted before the new one goes in: the index allows one pattern per trainer and date at
        // every write. Their sessions go with them, by the foreign key's cascade.
        var replaced = await db.WeekPatterns
            .Where(pattern => pattern.TrainerId == trainerId && pattern.StartsOn >= command.StartsOn)
            .ToListAsync(cancellationToken);
        if (replaced.Count > 0)
        {
            db.WeekPatterns.RemoveRange(replaced);
            await db.SaveChangesAsync(cancellationToken);
        }

        db.WeekPatterns.Add(created.Value);
        await db.SaveChangesAsync(cancellationToken);

        var cancelled = await TimetableCancellations.RecordAsync(
            db, trainerId, command.StartsOn, lastDay, before, today, calendar.TimeOfDay(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await sessionCancelledSms.QueueAsync(cancelled, cancellationToken);

        return await TimetableResponse.ForAsync(db, trainerId, today, cancellationToken);
    }
}
