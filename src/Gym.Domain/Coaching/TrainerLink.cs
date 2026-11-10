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
    private readonly List<CoachingPeriod> _periods = [];

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

    /// <summary>
    /// A moment (UTC): when the side told of the end pressed «دیدم» on it (task 15.4d), the member
    /// when the trainer ended it, the trainer when the member did; <c>null</c> until then, and always
    /// for an end nobody is told of (<see cref="CoachingNews.TellsMember"/>,
    /// <see cref="CoachingNews.TellsTrainer"/>).
    /// </summary>
    public DateTimeOffset? EndSeenAt { get; private set; }

    /// <summary>Postgres <c>xmin</c>: the second of two changes at once fails instead of overwriting.</summary>
    public uint Version { get; private set; }

    /// <summary>
    /// The coaching periods (15.7h), in the order given. Empty for a link made before 15.7h whose
    /// trainer has not typed the days left yet. Loaded only when included.
    /// </summary>
    public IReadOnlyList<CoachingPeriod> Periods => _periods;

    /// <summary>
    /// The last day of the last period; <c>null</c> while the link has none (made before 15.7h).
    /// Read only with <see cref="Periods"/> loaded.
    /// </summary>
    public DateOnly? PeriodEndsOn => _periods.Count == 0 ? null : _periods.Max(period => period.EndsOn);

    public bool IsOpen => EndedOn is null;

    /// <summary>The link starts today with its first period of <paramref name="days"/> days, already checked by the caller.</summary>
    internal static TrainerLink Start(Guid memberId, Guid trainerId, Guid requestId, DateOnly today, int days)
    {
        var link = new TrainerLink { MemberId = memberId, TrainerId = trainerId, RequestId = requestId, StartedOn = today };
        link._periods.Add(CoachingPeriod.Of(link.Id, today, days));

        return link;
    }

    /// <summary>
    /// «تمدید»: a new period of <paramref name="days"/> days right after the last one, or from today
    /// when it is over, so no day is lost. For a link made before 15.7h, the days left from today,
    /// as few as <see cref="CoachingPeriod.MinDaysLeft"/>. Needs <see cref="Periods"/> loaded.
    /// </summary>
    /// <param name="today">The gym's today.</param>
    public Result<CoachingPeriod> Renew(int days, DateOnly today)
    {
        if (!IsOpen)
        {
            return Result.Failure<CoachingPeriod>(CoachingErrors.LinkAlreadyEnded);
        }

        var minDays = _periods.Count == 0 ? CoachingPeriod.MinDaysLeft : CoachingPeriod.MinDays;
        if (!CoachingPeriod.IsAllowed(days, minDays))
        {
            return Result.Failure<CoachingPeriod>(CoachingErrors.PeriodDaysOutOfRange);
        }

        var startsOn = PeriodEndsOn is { } lastDay && lastDay >= today ? lastDay.AddDays(1) : today;
        var period = CoachingPeriod.Of(Id, startsOn, days);
        _periods.Add(period);

        return period;
    }

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

    /// <summary>The side told of the end pressed «دیدم» (task 15.4d). A second press keeps the first moment.</summary>
    /// <exception cref="InvalidOperationException">The link is still open.</exception>
    public void MarkEndSeen(DateTimeOffset now)
    {
        if (IsOpen)
        {
            throw new InvalidOperationException("Only an ended link has an end to see.");
        }

        EndSeenAt ??= now;
    }
}
