using Gym.Application.Common;
using Gym.Domain.Coaching;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Coaching.CancelRenewalRequest;

/// <summary>
/// «لغو درخواست»: the member takes back their «درخواست تمدید» waiting for the trainer (BUSINESS_RULES.md
/// §15 <i>The coaching period</i>, task 15.7o). Under the coaching lock: a trainer renewing at the same
/// moment either comes first, and there is then nothing to take back, or after, and finds it taken back.
/// </summary>
public sealed class CancelRenewalRequestHandler(IAppDbContext db, IGymCalendar calendar)
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

        var linkId = await db.TrainerLinks.AsNoTracking()
            .Where(row => row.MemberId == member.Value.Id && row.EndedOn == null)
            .Select(row => (Guid?)row.Id)
            .SingleOrDefaultAsync(cancellationToken);
        var waiting = linkId is { } id ? await RenewalRequests.WaitingAsync(db, id, cancellationToken) : null;
        if (waiting is null)
        {
            return Result.Failure<MyTrainerResponse>(CoachingErrors.NoRenewalRequest);
        }

        waiting.Cancel();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await MyTrainerResponse.ForAsync(db, member.Value.Id, calendar.Today(), cancellationToken);
    }
}
