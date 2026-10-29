using Gym.Application.Common;
using Gym.Domain.Coaching;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Coaching.DeclineTrainerRequest;

/// <summary>
/// The trainer declines a member's request (BUSINESS_RULES.md §15). The member keeps their trainer
/// now, if any, and may ask again, this trainer too.
/// </summary>
public sealed class DeclineTrainerRequestHandler(IAppDbContext db)
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

        var request = await db.TrainerRequests.SingleOrDefaultAsync(
            row => row.Id == requestId && row.TrainerId == trainer.Value.Id,
            cancellationToken);
        if (request is null)
        {
            return Result.Failure<StudentsResponse>(CoachingErrors.RequestNotFound);
        }

        var declined = request.Decline();
        if (declined.IsFailure)
        {
            return Result.Failure<StudentsResponse>(declined.Error);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await StudentsResponse.ForAsync(db, trainer.Value.Id, cancellationToken);
    }
}
