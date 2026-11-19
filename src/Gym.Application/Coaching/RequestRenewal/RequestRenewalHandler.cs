using Gym.Application.Common;
using Gym.Domain.Coaching;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Coaching.RequestRenewal;

/// <summary>
/// «درخواست تمدید» (BUSINESS_RULES.md §15 <i>The coaching period</i>, task 15.7o): the member asks
/// their trainer to renew the coaching, from its last days (the SMS settings' number for them) or
/// after it is over. The trainer sees it in the app only: no SMS, no confirmation, no daily limit.
/// </summary>
/// <remarks>
/// Under the coaching lock, so two presses at once make one request and the link cannot end
/// meanwhile; the partial unique index holds one waiting per link too.
/// </remarks>
public sealed class RequestRenewalHandler(IAppDbContext db, IGymCalendar calendar)
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

        var link = await db.TrainerLinks
            .Include(row => row.Periods)
            .SingleOrDefaultAsync(row => row.MemberId == member.Value.Id && row.EndedOn == null, cancellationToken);
        if (link is null)
        {
            return Result.Failure<MyTrainerResponse>(CoachingErrors.NoTrainer);
        }

        var today = calendar.Today();

        // A second press, or the app sent again: the request is already there.
        if (await RenewalRequests.WaitingAsync(db, link.Id, cancellationToken) is null)
        {
            var created = RenewalRequest.Create(link, today, await CoachingPeriods.SoonDaysAsync(db, cancellationToken));
            if (created.IsFailure)
            {
                return Result.Failure<MyTrainerResponse>(created.Error);
            }

            db.RenewalRequests.Add(created.Value);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return await MyTrainerResponse.ForAsync(db, member.Value.Id, today, cancellationToken);
    }
}
