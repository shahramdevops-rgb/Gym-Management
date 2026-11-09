using Gym.Application.Common;
using Gym.Domain.Coaching;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Coaching.SeeStudentNews;

/// <summary>
/// The trainer presses «دیدم» on «خبر شاگردان» (BUSINESS_RULES.md §15, task 15.4d): the ends the
/// card showed leave the home. Pressed twice, nothing changes.
/// </summary>
public sealed class SeeStudentNewsHandler(IAppDbContext db, IGymCalendar calendar, TimeProvider time)
{
    public async Task<Result<CoachingNewsResponse>> Handle(Guid siteAccountId, SeeCoachingNewsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockCoachingAsync(cancellationToken);

        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<CoachingNewsResponse>(trainer.Error);
        }

        var trainerId = trainer.Value.Id;
        var ids = command.DistinctIds();
        var now = time.GetUtcNow();

        var ends = await db.TrainerLinks
            .Where(link => ids.Contains(link.Id) && link.TrainerId == trainerId
                && link.EndReason == TrainerLinkEndReason.ByMember && link.EndSeenAt == null)
            .ToListAsync(cancellationToken);
        foreach (var link in ends)
        {
            link.MarkEndSeen(now);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await CoachingNewsResponse.ForTrainerAsync(db, trainerId, calendar.Today(), cancellationToken);
    }
}
