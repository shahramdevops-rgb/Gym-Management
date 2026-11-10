using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Domain.Bookings;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Bookings.CancelMyLeave;

/// <summary>
/// «لغو درخواست» (BUSINESS_RULES.md §15 <i>Leave by request</i>, task 15.7i): the member takes back a
/// leave request the trainer has not answered, until the session starts. An accepted one is taken
/// back with «می‌آیم», which also gives the place back.
/// </summary>
/// <remarks>Under the trainer's row lock, so the trainer's answer and this come one after the other.</remarks>
public sealed class CancelMyLeaveHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<MySessionsResponse>> Handle(Guid siteAccountId, CancelMyLeaveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var member = await CoachingAccess.MemberAsync(db, siteAccountId, cancellationToken);
        if (member.IsFailure)
        {
            return Result.Failure<MySessionsResponse>(member.Error);
        }

        var memberId = member.Value.Id;
        var trainerId = await db.TrainerLinks
            .Where(link => link.MemberId == memberId && link.EndedOn == null)
            .Select(link => (Guid?)link.TrainerId)
            .SingleOrDefaultAsync(cancellationToken);
        if (trainerId is null)
        {
            return Result.Failure<MySessionsResponse>(BookingErrors.LeaveRequestNotFound);
        }

        await db.LockTrainerAsync(trainerId.Value, cancellationToken);

        var request = await db.LeaveRequests.SingleOrDefaultAsync(
            row => row.MemberId == memberId && row.TrainerId == trainerId && row.Status == LeaveRequestStatus.Pending
                && row.Date == command.Date && row.Start == command.Start && row.End == command.End,
            cancellationToken);
        if (request is null)
        {
            return Result.Failure<MySessionsResponse>(BookingErrors.LeaveRequestNotFound);
        }

        var today = calendar.Today();
        var timeOfDay = calendar.TimeOfDay();
        var takenBack = request.TakeBack(today, timeOfDay);
        if (takenBack.IsFailure)
        {
            return Result.Failure<MySessionsResponse>(takenBack.Error);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await MySessionsResponse.ForAsync(db, memberId, today, timeOfDay, cancellationToken);
    }
}
