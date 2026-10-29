using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Trainers;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Trainers.StopTrainer;

/// <summary>
/// The Owner stops someone being a trainer: their period ends today (BUSINESS_RULES.md §15). The
/// trainer's area on the site closes at once for «me» and within the access token's 15 minutes for
/// everything else; a member on the same account keeps the member's area. Their students' links end
/// today and the requests waiting for them end too (15.4).
/// </summary>
/// <remarks>
/// Under the coaching lock, so a member asking for this trainer, or the trainer accepting someone,
/// at the same moment either comes first and is ended here, or after and finds them stopped.
/// </remarks>
public sealed class StopTrainerHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<TrainerResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        await db.LockCoachingAsync(cancellationToken);

        var trainer = await db.Trainers.Include(row => row.Periods).SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (trainer is null)
        {
            return Result.Failure<TrainerResponse>(TrainerErrors.NotFound);
        }

        var today = calendar.Today();
        var stopped = trainer.Stop(today);
        if (stopped.IsFailure)
        {
            return Result.Failure<TrainerResponse>(stopped.Error);
        }

        await TrainerStudents.ReleaseAsync(db, trainer.Id, today, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await TrainerResponse.ForAsync(db, trainer, cancellationToken);
    }
}
