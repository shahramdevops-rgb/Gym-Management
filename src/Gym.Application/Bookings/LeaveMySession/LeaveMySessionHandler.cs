using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Domain.Bookings;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Bookings.LeaveMySession;

/// <summary>
/// «درخواست مرخصی» (BUSINESS_RULES.md §15 <i>Leave by request</i>, task 15.7i): the member asks
/// their trainer for leave from one date of a session, by the day before. Nothing changes in the
/// session until the trainer accepts; the free «نمی‌آیم» of 15.6b is gone.
/// </summary>
/// <remarks>
/// Under the trainer's row lock, as every change of the places, so the trainer's answer and this
/// come one after the other. An app still on the page before 15.7i calls the same address, and
/// gets a request in place of the leave.
/// </remarks>
public sealed class LeaveMySessionHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<MySessionsResponse>> Handle(Guid siteAccountId, LeaveMySessionCommand command, CancellationToken cancellationToken)
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

        var asked = await db.LeaveRequests.AnyAsync(
            request => request.MemberId == memberId
                && request.Date == command.Date && request.Start == command.Start && request.End == command.End
                && (request.Status == LeaveRequestStatus.Pending || request.Status == LeaveRequestStatus.Accepted),
            cancellationToken);
        if (asked)
        {
            return Result.Failure<MySessionsResponse>(BookingErrors.LeaveAlreadyAsked);
        }

        var today = calendar.Today();
        var timeOfDay = calendar.TimeOfDay();
        var request = LeaveRequest.Create(place.Value, link.Id, today);
        if (request.IsFailure)
        {
            return Result.Failure<MySessionsResponse>(request.Error);
        }

        db.LeaveRequests.Add(request.Value);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await MySessionsResponse.ForAsync(db, memberId, today, timeOfDay, cancellationToken);
    }
}
