using Gym.Application.Common;
using Gym.Domain.Coaching;
using Gym.Domain.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Coaching.EndStudent;

/// <summary>
/// The trainer ends a student's link (BUSINESS_RULES.md §15, decided by the developer 1405/08/07):
/// it ends today, the trainer stops seeing the member today, and the member is left with no trainer.
/// </summary>
public sealed class EndStudentHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<StudentsResponse>> Handle(Guid siteAccountId, Guid memberId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockCoachingAsync(cancellationToken);

        var trainer = await CoachingAccess.TrainerAsync(db, siteAccountId, cancellationToken);
        if (trainer.IsFailure)
        {
            return Result.Failure<StudentsResponse>(trainer.Error);
        }

        var link = await db.TrainerLinks.SingleOrDefaultAsync(
            row => row.MemberId == memberId && row.TrainerId == trainer.Value.Id && row.EndedOn == null,
            cancellationToken);
        if (link is null)
        {
            return Result.Failure<StudentsResponse>(CoachingErrors.StudentNotFound);
        }

        link.End(calendar.Today(), TrainerLinkEndReason.ByTrainer);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await StudentsResponse.ForAsync(db, trainer.Value.Id, cancellationToken);
    }
}
