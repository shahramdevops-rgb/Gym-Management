using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Trainers;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Trainers.RestartTrainer;

/// <summary>
/// The Owner makes a stopped trainer a trainer again: a new period starts today, and the earlier
/// ones stay as they were (BUSINESS_RULES.md §15). Former students are not linked back.
/// </summary>
public sealed class RestartTrainerHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<TrainerResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        var trainer = await db.Trainers.Include(row => row.Periods).SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (trainer is null)
        {
            return Result.Failure<TrainerResponse>(TrainerErrors.NotFound);
        }

        var restarted = trainer.Restart(calendar.Today());
        if (restarted.IsFailure)
        {
            return Result.Failure<TrainerResponse>(restarted.Error);
        }

        // Two presses at once both find the trainer stopped; the index on open periods refuses the second.
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception) when (exception.ConstraintName == TrainerConstraints.OneOpenPeriod)
        {
            return Result.Failure<TrainerResponse>(TrainerErrors.AlreadyActive);
        }

        return await TrainerResponse.ForAsync(db, trainer, cancellationToken);
    }
}
