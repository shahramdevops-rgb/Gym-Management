using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Domain.Bookings;
using Gym.Domain.Common;

namespace Gym.Application.Bookings.EndStudentSchedule;

/// <summary>
/// «پایان برنامه» (BUSINESS_RULES.md §15 <i>Students in sessions</i>): the student's fixed schedule
/// stops after today; today's sessions stay, for attendance (15.7). One not started yet is removed.
/// The dates the student moved to stay: each is its own booking.
/// </summary>
/// <remarks>Under the trainer's row lock, as <see cref="SetStudentSchedule.SetStudentScheduleHandler"/>.</remarks>
public sealed class EndStudentScheduleHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<StudentScheduleResponse>> Handle(Guid siteAccountId, Guid memberId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<StudentScheduleResponse>(trainer.Error);
        }

        await db.LockTrainerAsync(trainer.Value.Id, cancellationToken);

        var link = await StudentSessions.LinkAsync(db, trainer.Value.Id, memberId, cancellationToken);
        if (link.IsFailure)
        {
            return Result.Failure<StudentScheduleResponse>(link.Error);
        }

        var today = calendar.Today();
        var current = await StudentSessions.FromTodayAsync(db, link.Value.Id, today, cancellationToken);
        if (!current.Any(schedule => schedule.EndsOn > today || !schedule.HasStartedBy(today)))
        {
            return Result.Failure<StudentScheduleResponse>(BookingErrors.NoSchedule);
        }

        StudentSessions.EndAfterToday(db, current, today);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await StudentScheduleResponse.ForAsync(db, link.Value, today, calendar.TimeOfDay(), cancellationToken);
    }
}
