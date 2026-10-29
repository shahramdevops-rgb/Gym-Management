using Gym.Application.Common;
using Gym.Domain.Coaching;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Coaching.CancelTrainerRequest;

/// <summary>
/// The member takes back the request waiting for a trainer's answer (BUSINESS_RULES.md §15). Under
/// the coaching lock: a trainer accepting at the same moment either comes first, and there is then
/// nothing to take back, or after, and finds it taken back.
/// </summary>
public sealed class CancelTrainerRequestHandler(IAppDbContext db)
{
    public async Task<Result<MyTrainerResponse>> Handle(Guid siteAccountId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockCoachingAsync(cancellationToken);

        var member = await CoachingAccess.MemberAsync(db, siteAccountId, cancellationToken);
        if (member.IsFailure)
        {
            return Result.Failure<MyTrainerResponse>(member.Error);
        }

        var waiting = await db.TrainerRequests.SingleOrDefaultAsync(
            request => request.MemberId == member.Value.Id && request.Status == TrainerRequestStatus.Pending,
            cancellationToken);
        if (waiting is null)
        {
            return Result.Failure<MyTrainerResponse>(CoachingErrors.NoPendingRequest);
        }

        waiting.Cancel();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await MyTrainerResponse.ForAsync(db, member.Value.Id, cancellationToken);
    }
}
