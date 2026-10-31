using Gym.Domain.Common;

namespace Gym.Domain.Bookings;

/// <summary>
/// One date of a <see cref="FixedSchedule"/> the student is out of (BUSINESS_RULES.md §15 <i>Students
/// in sessions</i>): they left it («نمی‌آیم»), moved it, or the trainer took them out of it. The
/// other weeks stay as they are.
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

    public BookedBy By { get; private set; }

    /// <summary>Takes the student out of <paramref name="place"/>, a date of their fixed schedule, until the session starts.</summary>
    /// <param name="today">The gym's today.</param>
    /// <param name="timeOfDay">The time of day now, in the gym's time zone.</param>
    public static Result<SessionSkip> Create(BookedPlace place, BookedBy by, DateOnly today, TimeOnly timeOfDay)
    {
        ArgumentNullException.ThrowIfNull(place);

        if (place.ScheduleId is not { } scheduleId)
        {
            throw new ArgumentException("A date moved to is removed, not skipped.", nameof(place));
        }

        if (BookingRules.HasStarted(place.Date, place.Start, today, timeOfDay))
        {
            return Result.Failure<SessionSkip>(BookingErrors.SessionStarted);
        }

        return new SessionSkip { ScheduleId = scheduleId, Date = place.Date, Start = place.Start, End = place.End, By = by };
    }
}
