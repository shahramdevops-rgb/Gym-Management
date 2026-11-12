using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Domain.Bookings;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Bookings.GetMyAttendance;

/// <summary>
/// «حضور من» (BUSINESS_RULES.md §15 <i>The member's own attendance</i>, task 15.7j): the member's
/// own sessions of a range with any trainer, each with its state as their trainer sees it, and the
/// totals. Read only. The visits outside their sessions are the trainer's alone: the member sees
/// their visits in «سوابق من».
/// </summary>
public sealed class GetMyAttendanceHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<MyAttendanceResponse>> Handle(Guid siteAccountId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var member = await CoachingAccess.MemberAsync(db, siteAccountId, cancellationToken);
        if (member.IsFailure)
        {
            return Result.Failure<MyAttendanceResponse>(member.Error);
        }

        if (!AttendanceReading.IsValidRange(from, to))
        {
            return Result.Failure<MyAttendanceResponse>(BookingErrors.AttendanceRangeInvalid);
        }

        // The trainer now goes unnamed, as «سانس‌های من» names them once at the top.
        var trainerNow = await db.TrainerLinks
            .Where(link => link.MemberId == member.Value.Id && link.EndedOn == null)
            .Select(link => (Guid?)link.TrainerId)
            .SingleOrDefaultAsync(cancellationToken);

        var read = await AttendanceReading.ReadAsync(
            db, calendar, member.Value.Id, from, to, viewingTrainerId: trainerNow, outsideSince: null, cancellationToken);

        return new MyAttendanceResponse(from, to, read.Items, read.Totals);
    }
}
