using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Domain.Bookings;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Bookings.AnswerLeaveRequest;

/// <summary>
/// The trainer's «قبول» or «رد» on a leave request (BUSINESS_RULES.md §15 <i>Leave by request</i>,
/// task 15.7i), until the session starts. Accepting takes the student out of that date, so the place
/// is free for someone else (decided by the developer, 1405/08/19); declining leaves them in it.
/// </summary>
/// <remarks>
/// Under the trainer's row lock, as every change of the places, so the student taking the request
/// back, or moving the date, at the same moment comes before or after this, never half-way.
/// </remarks>
public sealed class AnswerLeaveRequestHandler(IAppDbContext db, IGymCalendar calendar)
{
    public Task<Result<LeaveRequestsResponse>> AcceptAsync(Guid siteAccountId, Guid requestId, CancellationToken cancellationToken) =>
        AnswerAsync(siteAccountId, requestId, accept: true, cancellationToken);

    public Task<Result<LeaveRequestsResponse>> DeclineAsync(Guid siteAccountId, Guid requestId, CancellationToken cancellationToken) =>
        AnswerAsync(siteAccountId, requestId, accept: false, cancellationToken);

    private async Task<Result<LeaveRequestsResponse>> AnswerAsync(Guid siteAccountId, Guid requestId, bool accept, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<LeaveRequestsResponse>(trainer.Error);
        }

        var trainerId = trainer.Value.Id;
        await db.LockTrainerAsync(trainerId, cancellationToken);

        var request = await db.LeaveRequests.SingleOrDefaultAsync(row => row.Id == requestId && row.TrainerId == trainerId, cancellationToken);
        if (request is null)
        {
            return Result.Failure<LeaveRequestsResponse>(BookingErrors.LeaveRequestNotFound);
        }

        // The student's place that date: gone when they moved it, the trainer took them out, or the
        // session was cancelled meanwhile; the trainer's list no longer shows such a request.
        var place = await StudentSessions.PlaceAsync(db, trainerId, request.MemberId, request.Date, request.Start, request.End, cancellationToken);
        if (place.IsFailure)
        {
            return Result.Failure<LeaveRequestsResponse>(BookingErrors.LeaveRequestNotFound);
        }

        var today = calendar.Today();
        var timeOfDay = calendar.TimeOfDay();
        var answered = accept ? request.Accept(today, timeOfDay) : request.Decline(today, timeOfDay);
        if (answered.IsFailure)
        {
            return Result.Failure<LeaveRequestsResponse>(answered.Error);
        }

        if (accept)
        {
            // As «نمی‌آیم» did: a date of the fixed schedule is skipped (so «می‌آیم» can bring it
            // back), a date moved to is removed.
            var takenOut = await StudentSessions.TakeOutAsync(db, place.Value, SkipReason.Left, today, timeOfDay, cancellationToken);
            if (takenOut.IsFailure)
            {
                return Result.Failure<LeaveRequestsResponse>(takenOut.Error);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await LeaveRequestsResponse.ForAsync(db, trainerId, today, timeOfDay, cancellationToken);
    }
}
