using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Domain.Bookings;
using Gym.Domain.Common;

namespace Gym.Application.Bookings.EndStudentSchedule;

/// <summary>
/// «پایان برنامه» (BUSINESS_RULES.md §15 <i>Students in sessions</i>): the student's fixed schedule
/// stops from now; today's sessions that have started stay, for attendance (15.7), each as its own
/// booking. One not started yet is removed. The dates the student moved to stay: each is its own
/// booking already.
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
        var timeOfDay = calendar.TimeOfDay();
        var current = await StudentSessions.FromTodayAsync(db, link.Value.Id, today, cancellationToken);
        if (current.Count == 0)
        {
            return Result.Failure<StudentScheduleResponse>(BookingErrors.NoSchedule);
        }

        var started = await StudentSessions.StartedTodayAsync(db, current, today, timeOfDay, cancellationToken);
        StudentSessions.StopFromToday(db, current, today);
        var handOver = BookingRules.HandOver(started, passedOver: []);
        db.SessionBookings.AddRange(handOver.Keep.Select(place => SessionBooking.Keep(place, link.Value.Id)));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await StudentScheduleResponse.ForAsync(db, link.Value, today, timeOfDay, cancellationToken);
    }
}
