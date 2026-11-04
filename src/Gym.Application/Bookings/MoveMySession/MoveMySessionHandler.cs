using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Application.Timetables;
using Gym.Domain.Bookings;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Bookings.MoveMySession;

/// <summary>
/// «جابه‌جایی» (BUSINESS_RULES.md §15 <i>Students in sessions</i>): the member moves one date to
/// another session of their trainer, until the first starts. The new one has not started, has
/// room, and is not after the fixed schedule's last date. Leaving the first and taking the second
/// are one transaction: both or neither.
/// </summary>
/// <remarks>Under the trainer's row lock, so the room is counted by one change at a time.</remarks>
public sealed class MoveMySessionHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<MySessionsResponse>> Handle(Guid siteAccountId, MoveMySessionCommand command, CancellationToken cancellationToken)
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
            return Result.Failure<MySessionsResponse>(BookingErrors.NotInSession);
        }

        await db.LockTrainerAsync(link.TrainerId, cancellationToken);

        var place = await StudentSessions.PlaceAsync(db, link.TrainerId, memberId, command.Date, command.Start, command.End, cancellationToken);
        if (place.IsFailure)
        {
            return Result.Failure<MySessionsResponse>(place.Error);
        }

        var today = calendar.Today();
        var timeOfDay = calendar.TimeOfDay();
        var lastDate = await MySessionsResponse.LastDateAsync(db, link, today, cancellationToken);
        if (lastDate is null)
        {
            return Result.Failure<MySessionsResponse>(BookingErrors.NoSchedule);
        }

        var timetable = await TrainerTimetable.DatesAsync(db, link.TrainerId, command.ToDate, command.ToDate, cancellationToken);
        var target = timetable.SessionsOn(command.ToDate).FirstOrDefault(session => session.Start == command.ToStart && session.End == command.ToEnd);
        var targetPlaces = await BookedPlaces.OfTrainerAsync(db, link.TrainerId, command.ToDate, command.ToDate, cancellationToken);

        var allowed = BookingRules.CheckJoin(memberId, command.ToDate, target, targetPlaces.ToList(), lastDate.Value, today, timeOfDay);
        if (allowed.IsFailure)
        {
            return Result.Failure<MySessionsResponse>(allowed.Error);
        }

        var takenOut = await StudentSessions.TakeOutAsync(db, place.Value, SkipReason.Moved, today, timeOfDay, cancellationToken);
        if (takenOut.IsFailure)
        {
            return Result.Failure<MySessionsResponse>(takenOut.Error);
        }

        var booking = SessionBooking.Create(
            link.TrainerId, memberId, link.Id, command.ToDate, target!.Start, target.End, BookedBy.Member, today, timeOfDay);
        if (booking.IsFailure)
        {
            return Result.Failure<MySessionsResponse>(booking.Error);
        }

        db.SessionBookings.Add(booking.Value);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await MySessionsResponse.ForAsync(db, memberId, today, timeOfDay, cancellationToken);
    }
}
