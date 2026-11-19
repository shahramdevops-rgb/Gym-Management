using Gym.Application.Common;
using Gym.Domain.Coaching;
using Gym.Domain.Common;

namespace Gym.Application.Coaching.DeclineRenewalRequest;

/// <summary>
/// «رد کردن» a student's «درخواست تمدید» (BUSINESS_RULES.md §15 <i>The coaching period</i>, task
/// 15.7o): asks nothing, since the student may ask again; they read «درخواست تمدید رد شد» while the
/// coaching still ends on the day they asked about. Under the coaching lock, as every answer.
/// </summary>
public sealed class DeclineRenewalRequestHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<StudentsResponse>> Handle(Guid siteAccountId, Guid requestId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockCoachingAsync(cancellationToken);

        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<StudentsResponse>(trainer.Error);
        }

        var request = await RenewalRequests.OfTrainerAsync(db, trainer.Value.Id, requestId, cancellationToken);
        if (request is null)
        {
            return Result.Failure<StudentsResponse>(CoachingErrors.RenewalRequestNotFound);
        }

        var declined = request.Decline(calendar.Today());
        if (declined.IsFailure)
        {
            return Result.Failure<StudentsResponse>(declined.Error);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await StudentsResponse.ForAsync(db, trainer.Value.Id, cancellationToken);
    }
}
