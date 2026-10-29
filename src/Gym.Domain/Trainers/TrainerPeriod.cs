using Gym.Domain.Common;

namespace Gym.Domain.Trainers;

/// <summary>
/// One stretch of time someone was a trainer, from the day the Owner made them one to the day the
/// Owner stopped it (BUSINESS_RULES.md §15 <i>Trainers</i>). A trainer made one again gets a new
/// period; the earlier ones are never changed.
/// </summary>
public sealed class TrainerPeriod : Entity
{
    // For EF Core.
    private TrainerPeriod()
    {
    }

    public Guid TrainerId { get; private set; }

    public DateOnly StartedOn { get; private set; }

    /// <summary><c>null</c> while the period is open: the trainer is a trainer now.</summary>
    public DateOnly? EndedOn { get; private set; }

    public bool IsOpen => EndedOn is null;

    internal static TrainerPeriod Start(Guid trainerId, DateOnly today) =>
        new() { TrainerId = trainerId, StartedOn = today };

    internal void End(DateOnly today) => EndedOn = today;
}
