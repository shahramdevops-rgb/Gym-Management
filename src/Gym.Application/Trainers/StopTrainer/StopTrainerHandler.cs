using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Trainers;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Trainers.StopTrainer;

/// <summary>
/// The Owner stops someone being a trainer: their period ends today (BUSINESS_RULES.md §15). The
/// trainer's area on the site closes at once for «me» and within the access token's 15 minutes for
/// everything else; a member on the same account keeps the member's area. Ending the links to their
/// students comes with the links themselves, in 15.4.
/// </summary>
public sealed class StopTrainerHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<TrainerResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        var trainer = await db.Trainers.Include(row => row.Periods).SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (trainer is null)
        {
            return Result.Failure<TrainerResponse>(TrainerErrors.NotFound);
        }

        var stopped = trainer.Stop(calendar.Today());
        if (stopped.IsFailure)
        {
            return Result.Failure<TrainerResponse>(stopped.Error);
        }

        await db.SaveChangesAsync(cancellationToken);

        return await TrainerResponse.ForAsync(db, trainer, cancellationToken);
    }
}
