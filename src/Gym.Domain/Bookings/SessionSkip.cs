using Gym.Domain.Common;

namespace Gym.Domain.Bookings;

/// <summary>
/// One date of a <see cref="FixedSchedule"/> the student is out of (BUSINESS_RULES.md §15 <i>Students
/// in sessions</i>): they left it («نمی‌آیم»), moved it, the trainer took them out of it, or the
/// schedule passed it over as it was saved or ended (<see cref="Reason"/>). The other weeks stay as
/// they are.
/// </summary>
/// <remarks>One per schedule, date and session, held by a unique index too.</remarks>
public sealed class SessionSkip : Entity
{
    // For EF Core.
    private SessionSkip()
    {
    }

    public Guid ScheduleId { get; private set; }

    public DateOnly Date { get; private set; }

    public TimeOnly Start { get; private set; }

    public TimeOnly End { get; private set; }

    /// <summary>The member for <see cref="SkipReason.Left"/> and <see cref="SkipReason.Moved"/>, else the trainer.</summary>
    public BookedBy By { get; private set; }

    public SkipReason Reason { get; private set; }

    /// <summary>Takes the student out of <paramref name="place"/>, a date of their fixed schedule, until the session starts.</summary>
    /// <param name="reason">Left, moved or taken out; a date passed over is <see cref="PassOver"/>'s.</param>
    /// <param name="today">The gym's today.</param>
    /// <param name="timeOfDay">The time of day now, in the gym's time zone.</param>
    public static Result<SessionSkip> Create(BookedPlace place, SkipReason reason, DateOnly today, TimeOnly timeOfDay)
    {
        ArgumentNullException.ThrowIfNull(place);

        if (place.ScheduleId is not { } scheduleId)
        {
            throw new ArgumentException("A date moved to is removed, not skipped.", nameof(place));
        }

        if (reason is not (SkipReason.Left or SkipReason.Moved or SkipReason.TakenOut))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "A date passed over is made by PassOver.");
        }

        if (BookingRules.HasStarted(place.Date, place.Start, today, timeOfDay))
        {
            return Result.Failure<SessionSkip>(BookingErrors.SessionStarted);
        }

        return New(scheduleId, place, reason);
    }

    /// <summary>
    /// A date the schedule passes over as the trainer saves or ends it (<see cref="ScheduleHandOver.PassOver"/>):
    /// it had no such session then, or the session had started. By the trainer, who saved it.
    /// </summary>
    /// <remarks>
    /// It stays out if that session comes back later: the trainer saves the schedule again to put the
    /// student in it, where its room is counted.
    /// </remarks>
    public static SessionSkip PassOver(BookedPlace place)
    {
        ArgumentNullException.ThrowIfNull(place);

        if (place.ScheduleId is not { } scheduleId)
        {
            throw new ArgumentException("Only a date of a fixed schedule is passed over.", nameof(place));
        }

        return New(scheduleId, place, SkipReason.PassedOver);
    }

    /// <summary>
    /// Whether the member may take back their «نمی‌آیم» («می‌آیم»): a date they left themselves, until
    /// the session starts. Whether the session is still there and has room is read by the caller
    /// (<see cref="BookingRules.CheckJoin"/>).
    /// </summary>
    public Result CheckReturn(DateOnly today, TimeOnly timeOfDay)
    {
        if (Reason != SkipReason.Left)
        {
            return Result.Failure(BookingErrors.NotLeft);
        }

        return BookingRules.HasStarted(Date, Start, today, timeOfDay) ? Result.Failure(BookingErrors.SessionStarted) : Result.Success();
    }

    private static SessionSkip New(Guid scheduleId, BookedPlace place, SkipReason reason) => new()
    {
        ScheduleId = scheduleId,
        Date = place.Date,
        Start = place.Start,
        End = place.End,
        By = reason is SkipReason.Left or SkipReason.Moved ? BookedBy.Member : BookedBy.Trainer,
        Reason = reason,
    };
}
