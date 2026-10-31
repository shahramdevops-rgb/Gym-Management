using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Domain.Bookings;
using Gym.Domain.Common;

namespace Gym.Application.Bookings.SetStudentSchedule;

/// <summary>
/// The trainer puts a student in a fixed weekly slot with an end date (BUSINESS_RULES.md §15 <i>Who
/// puts a member in a session</i>): saved whole, or not at all when one of its dates has no room.
/// The trainer's check before it (<c>CheckStudentScheduleHandler</c>) names those dates.
/// </summary>
/// <remarks>Under the trainer's row lock, as the timetable's changes, so the room is counted by one change at a time.</remarks>
public sealed class SetStudentScheduleHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<StudentScheduleResponse>> Handle(
        Guid siteAccountId, Guid memberId, SetStudentScheduleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<StudentScheduleResponse>(trainer.Error);
        }

        await db.LockTrainerAsync(trainer.Value.Id, cancellationToken);

        var today = calendar.Today();
        var applied = await FixedScheduleChange.ApplyAsync(db, trainer.Value.Id, memberId, command, today, cancellationToken);
        if (applied.IsFailure)
        {
            return Result.Failure<StudentScheduleResponse>(applied.Error);
        }

        // Not committed: the transaction rolls the schedule back as it closes.
        if (applied.Value.Fit.Full.Count > 0)
        {
            return Result.Failure<StudentScheduleResponse>(BookingErrors.SessionFull);
        }

        await transaction.CommitAsync(cancellationToken);

        return await StudentScheduleResponse.ForAsync(db, applied.Value.Link, today, calendar.TimeOfDay(), cancellationToken);
    }
}
