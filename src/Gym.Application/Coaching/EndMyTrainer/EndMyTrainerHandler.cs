using Gym.Application.Common;
using Gym.Domain.Coaching;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Coaching.EndMyTrainer;

/// <summary>
/// The member ends their link to their trainer, with no new one (BUSINESS_RULES.md §15, decided by
/// the developer 1405/08/07): it ends today, and the trainer stops seeing them today. A request
/// waiting for another trainer is left as it is.
/// </summary>
public sealed class EndMyTrainerHandler(IAppDbContext db, IGymCalendar calendar)
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

        var link = await db.TrainerLinks.SingleOrDefaultAsync(
            row => row.MemberId == member.Value.Id && row.EndedOn == null,
            cancellationToken);
        if (link is null)
        {
            return Result.Failure<MyTrainerResponse>(CoachingErrors.NoTrainer);
        }

        link.End(calendar.Today(), TrainerLinkEndReason.ByMember);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await MyTrainerResponse.ForAsync(db, member.Value.Id, cancellationToken);
    }
}
