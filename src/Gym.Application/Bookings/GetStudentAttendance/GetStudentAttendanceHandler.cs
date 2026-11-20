using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Domain.Bookings;
using Gym.Domain.Common;

namespace Gym.Application.Bookings.GetStudentAttendance;

/// <summary>
/// The trainer opens a student's attendance (BUSINESS_RULES.md §15 <i>Attendance for the trainer</i>):
/// only a student of theirs now, but every session of the range with any trainer, since a new
/// trainer sees the earlier attendance (<i>A member and their trainer</i>). The reading itself is
/// <see cref="AttendanceReading"/>, shared with the member's own «حضور من» (15.7j).
/// </summary>
public sealed class GetStudentAttendanceHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<StudentAttendanceResponse>> Handle(
        Guid siteAccountId, Guid memberId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<StudentAttendanceResponse>(trainer.Error);
        }

        var link = await StudentSessions.LinkAsync(db, trainer.Value.Id, memberId, cancellationToken);
        if (link.IsFailure)
        {
            return Result.Failure<StudentAttendanceResponse>(link.Error);
        }

        if (!AttendanceReading.IsValidRange(from, to))
        {
            return Result.Failure<StudentAttendanceResponse>(BookingErrors.AttendanceRangeInvalid);
        }

        // «روز دیگر» from the day they became this trainer's student, not their gym visits before it.
        var read = await AttendanceReading.ReadAsync(
            db, calendar, memberId, from, to, viewingTrainerId: trainer.Value.Id, otherDaysSince: link.Value.StartedOn, cancellationToken);

        return new StudentAttendanceResponse(memberId, from, to, read.Items, read.Totals, read.OtherDays);
    }
}
