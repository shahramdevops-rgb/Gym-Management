using Gym.Domain.Common;

namespace Gym.Domain.Coaching;

/// <summary>
/// A member being a trainer's student, from the day the trainer accepted to the day it ended
/// (BUSINESS_RULES.md §15 <i>A member and their trainer</i>). Kept after it ends, so the history of
/// who trained whom stays.
/// </summary>
/// <remarks>
/// <b>The trainer sees the member only while it is open.</b> A link ends on the gym's today, never
/// later, so a former trainer stops seeing the member that same day. A member has one open link at
/// most, held by a partial unique index too.
/// </remarks>
public sealed class TrainerLink : Entity
{
    // For EF Core.
    private TrainerLink()
    {
    }

    public Guid MemberId { get; private set; }

    public Guid TrainerId { get; private set; }

    /// <summary>The request the trainer accepted.</summary>
    public Guid RequestId { get; private set; }

    public DateOnly StartedOn { get; private set; }

    /// <summary><c>null</c> while the member is the trainer's student.</summary>
    public DateOnly? EndedOn { get; private set; }

    /// <summary><c>null</c> while open.</summary>
    public TrainerLinkEndReason? EndReason { get; private set; }

    /// <summary>Postgres <c>xmin</c>: the second of two changes at once fails instead of overwriting.</summary>
    public uint Version { get; private set; }

    public bool IsOpen => EndedOn is null;

    internal static TrainerLink Start(Guid memberId, Guid trainerId, Guid requestId, DateOnly today) =>
        new() { MemberId = memberId, TrainerId = trainerId, RequestId = requestId, StartedOn = today };

    /// <param name="today">The gym's today: the link ends on it.</param>
    public Result End(DateOnly today, TrainerLinkEndReason reason)
    {
        if (!IsOpen)
        {
            return Result.Failure(CoachingErrors.LinkAlreadyEnded);
        }

        // A clock that went back would end a link before it began; the database refuses that too.
        EndedOn = today < StartedOn ? StartedOn : today;
        EndReason = reason;

        return Result.Success();
    }
}
