using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Application.Timetables;
using Gym.Domain.Bookings;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Bookings.ReturnToMySession;

/// <summary>
/// «می‌آیم» (BUSINESS_RULES.md §15 <i>Students in sessions</i>, <i>Leave by request</i>): the member
/// takes back their own accepted leave (or a «نمی‌آیم» from before 15.7i) on one date, until the
/// session starts, while the session is still there and has room: the place was freed meanwhile.
/// A date moved, or taken out by the trainer, does not come back this way.
/// </summary>
/// <remarks>Under the trainer's row lock, so the room is counted by one change at a time.</remarks>
public sealed class ReturnToMySessionHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<MySessionsResponse>> Handle(Guid siteAccountId, ReturnToMySessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var member = await CoachingAccess.MemberAsync(db, siteAccountId, cancellationToken);
        if (member.IsFailure)
        {
            return Result.Failure<MySessionsResponse>(member.Error);
        }

        var memberId = member.Value.Id;
        var link = await db.TrainerLinks.AsNoTracking()
            .SingleOrDefaultAsync(row => row.MemberId == memberId && row.EndedOn == null, cancellationToken);
        if (link is null)
        {
            return Result.Failure<MySessionsResponse>(BookingErrors.NotLeft);
        }

        await db.LockTrainerAsync(link.TrainerId, cancellationToken);

        // The skip of one of the link's schedules that still reaches that date.
        var found = await db.SessionSkips
            .Join(db.FixedSchedules, skip => skip.ScheduleId, schedule => schedule.Id, (skip, schedule) => new { skip, schedule })
            .Where(row => row.schedule.LinkId == link.Id && row.schedule.EndsOn >= command.Date)
            .Where(row => row.skip.Date == command.Date && row.skip.Start == command.Start && row.skip.End == command.End)
            .SingleOrDefaultAsync(cancellationToken);
        if (found is null)
        {
            return Result.Failure<MySessionsResponse>(BookingErrors.NotLeft);
        }

        var today = calendar.Today();
        var timeOfDay = calendar.TimeOfDay();
        var allowed = found.skip.CheckReturn(today, timeOfDay);
        if (allowed.IsFailure)
        {
            return Result.Failure<MySessionsResponse>(allowed.Error);
        }

        var timetable = await TrainerTimetable.DatesAsync(db, link.TrainerId, command.Date, command.Date, cancellationToken);
        var session = timetable.SessionsOn(command.Date).FirstOrDefault(row => row.Start == command.Start && row.End == command.End);
        var places = await BookedPlaces.OfTrainerAsync(db, link.TrainerId, command.Date, command.Date, cancellationToken);

        var room = BookingRules.CheckJoin(memberId, command.Date, session, places.ToList(), found.schedule.EndsOn, today, timeOfDay);
        if (room.IsFailure)
        {
            return Result.Failure<MySessionsResponse>(room.Error);
        }

        db.SessionSkips.Remove(found.skip);

        // The accepted request it came from (15.7i) is taken back with it; a «نمی‌آیم» from before
        // 15.7i has none.
        var accepted = await db.LeaveRequests.SingleOrDefaultAsync(
            request => request.MemberId == memberId && request.Status == LeaveRequestStatus.Accepted
                && request.Date == command.Date && request.Start == command.Start && request.End == command.End,
            cancellationToken);
        accepted?.TakeBack(today, timeOfDay);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await MySessionsResponse.ForAsync(db, memberId, today, timeOfDay, cancellationToken);
    }
}
