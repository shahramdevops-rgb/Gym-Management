using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Domain.Bookings;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Bookings.LeaveMySession;

/// <summary>
/// «نمی‌آیم» (BUSINESS_RULES.md §15 <i>Students in sessions</i>): the member leaves one date of their
/// trainer's sessions, until it starts. The other weeks stay as they are.
/// </summary>
/// <remarks>Under the trainer's row lock, as every change of the places.</remarks>
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
        var trainerId = await db.TrainerLinks
            .Where(link => link.MemberId == memberId && link.EndedOn == null)
            .Select(link => (Guid?)link.TrainerId)
            .SingleOrDefaultAsync(cancellationToken);
        if (trainerId is null)
        {
            return Result.Failure<MySessionsResponse>(BookingErrors.NotInSession);
        }

        await db.LockTrainerAsync(trainerId.Value, cancellationToken);

        var place = await StudentSessions.PlaceAsync(db, trainerId.Value, memberId, command.Date, command.Start, command.End, cancellationToken);
        if (place.IsFailure)
        {
            return Result.Failure<MySessionsResponse>(place.Error);
        }

        var today = calendar.Today();
        var timeOfDay = calendar.TimeOfDay();
        var takenOut = await StudentSessions.TakeOutAsync(db, place.Value, BookedBy.Member, today, timeOfDay, cancellationToken);
        if (takenOut.IsFailure)
        {
            return Result.Failure<MySessionsResponse>(takenOut.Error);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await MySessionsResponse.ForAsync(db, memberId, today, timeOfDay, cancellationToken);
    }
}
