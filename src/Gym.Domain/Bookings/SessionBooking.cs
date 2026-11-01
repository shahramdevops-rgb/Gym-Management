using Gym.Domain.Common;

namespace Gym.Domain.Bookings;

/// <summary>
/// A student in one session on one date, outside their fixed schedule (BUSINESS_RULES.md §15
/// <i>Students in sessions</i>): the date they moved one of their sessions to (by the member), or
/// today's started session kept when the trainer changed the schedule (by the trainer, <see cref="Keep"/>).
/// Taking them out of it removes the row: a timetable is not a financial record.
/// </summary>
/// <remarks>
/// Tied to the link it was made under, as <see cref="FixedSchedule"/> is. One per member, date and
/// session, held by a unique index too.
/// </remarks>
public sealed class SessionBooking : Entity
{
    // For EF Core.
    private SessionBooking()
    {
    }

    public Guid TrainerId { get; private set; }

    public Guid MemberId { get; private set; }

    public Guid LinkId { get; private set; }

    public DateOnly Date { get; private set; }

    public TimeOnly Start { get; private set; }

    public TimeOnly End { get; private set; }

    public BookedBy By { get; private set; }

    /// <summary>
    /// Puts the student in a session of <paramref name="date"/> that has not started. Whether the
    /// session is the trainer's and has room is read by the caller, from the timetable and the
    /// places taken (<see cref="BookingRules.CheckJoin"/>).
    /// </summary>
    /// <param name="today">The gym's today.</param>
    /// <param name="timeOfDay">The time of day now, in the gym's time zone.</param>
    public static Result<SessionBooking> Create(
        Guid trainerId,
        Guid memberId,
        Guid linkId,
        DateOnly date,
        TimeOnly start,
        TimeOnly end,
        BookedBy by,
        DateOnly today,
        TimeOnly timeOfDay)
    {
        if (BookingRules.HasStarted(date, start, today, timeOfDay))
        {
            return Result.Failure<SessionBooking>(BookingErrors.SessionStarted);
        }

        return new SessionBooking
        {
            TrainerId = trainerId,
            MemberId = memberId,
            LinkId = linkId,
            Date = date,
            Start = start,
            End = end,
            By = by,
        };
    }

    /// <summary>
    /// Keeps the student in today's session of their fixed schedule that has started, as the trainer
    /// changes or ends the schedule and the new one does not have it (<see cref="ScheduleHandOver.Keep"/>):
    /// what has started stays as it was, for attendance (15.7). By the trainer, who changed it.
    /// </summary>
    /// <param name="place">The place the schedule gave, before the change.</param>
    /// <param name="linkId">The link the schedule was made under.</param>
    public static SessionBooking Keep(BookedPlace place, Guid linkId)
    {
        ArgumentNullException.ThrowIfNull(place);

        if (place.ScheduleId is null)
        {
            throw new ArgumentException("A date moved to is a booking already.", nameof(place));
        }

        return new SessionBooking
        {
            TrainerId = place.TrainerId,
            MemberId = place.MemberId,
            LinkId = linkId,
            Date = place.Date,
            Start = place.Start,
            End = place.End,
            By = BookedBy.Trainer,
        };
    }

    /// <summary>Whether the student may still be taken out of it: until the session starts.</summary>
    public Result CheckRemove(DateOnly today, TimeOnly timeOfDay) =>
        BookingRules.HasStarted(Date, Start, today, timeOfDay) ? Result.Failure(BookingErrors.SessionStarted) : Result.Success();
}
