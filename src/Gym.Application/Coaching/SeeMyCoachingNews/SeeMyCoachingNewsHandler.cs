using Gym.Application.Common;
using Gym.Domain.Coaching;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Coaching.SeeMyCoachingNews;

/// <summary>
/// The member presses «دیدم» on «خبر مربی» (BUSINESS_RULES.md §15, task 15.4d): the answers and
/// ends the card showed leave the home. Pressed twice, nothing changes.
/// </summary>
/// <remarks>
/// Under the coaching lock, as every write of requests and links: two presses at once come one after
/// the other instead of one failing on the row's version.
/// </remarks>
public sealed class SeeMyCoachingNewsHandler(IAppDbContext db, IGymCalendar calendar, TimeProvider time)
{
    public async Task<Result<CoachingNewsResponse>> Handle(Guid siteAccountId, SeeCoachingNewsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockCoachingAsync(cancellationToken);

        var member = await CoachingAccess.MemberAsync(db, siteAccountId, cancellationToken);
        if (member.IsFailure)
        {
            return Result.Failure<CoachingNewsResponse>(member.Error);
        }

        var memberId = member.Value.Id;
        var ids = command.DistinctIds();
        var now = time.GetUtcNow();

        var answers = await db.TrainerRequests
            .Where(request => ids.Contains(request.Id) && request.MemberId == memberId
                && request.AnsweredOn != null && request.AnswerSeenAt == null)
            .ToListAsync(cancellationToken);
        foreach (var request in answers)
        {
            request.MarkAnswerSeen(now);
        }

        var ends = await db.TrainerLinks
            .Where(link => ids.Contains(link.Id) && link.MemberId == memberId
                && link.EndReason == TrainerLinkEndReason.ByTrainer && link.EndSeenAt == null)
            .ToListAsync(cancellationToken);
        foreach (var link in ends)
        {
            link.MarkEndSeen(now);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await CoachingNewsResponse.ForMemberAsync(db, memberId, calendar.Today(), cancellationToken);
    }
}
