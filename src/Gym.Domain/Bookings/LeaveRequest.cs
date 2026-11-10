using Gym.Domain.Common;

namespace Gym.Domain.Bookings;

/// <summary>
/// A student asking their trainer for leave from one date of a session (مرخصی با درخواست,
/// BUSINESS_RULES.md §15 <i>Leave by request</i>, task 15.7i). It replaces the free «نمی‌آیم»: the
/// student asks by the day before, the trainer accepts or declines, and only an accepted leave takes
/// the student out of the session, which frees the place.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not answered by the start is declined.</b> No job closes it: a request still
/// <see cref="LeaveRequestStatus.Pending"/> once the session has started reads as declined
/// (<see cref="StateOn"/>), and the student is in the session as before.
/// </para>
/// <para>
/// <b>One live request per place.</b> A waiting or accepted request per member, date and session
/// times, held by a partial unique index too. Kept, never deleted, so what was asked and answered
/// stays.
/// </para>
/// </remarks>
public sealed class LeaveRequest : Entity
{
    // For EF Core.
    private LeaveRequest()
    {
    }

    public Guid TrainerId { get; private set; }

    public Guid MemberId { get; private set; }

    /// <summary>The trainer and student's link it was asked under.</summary>
    public Guid LinkId { get; private set; }

    public DateOnly Date { get; private set; }

    public TimeOnly Start { get; private set; }

    public TimeOnly End { get; private set; }

    public LeaveRequestStatus Status { get; private set; }

    /// <summary>The gym's day it was asked on.</summary>
    public DateOnly RequestedOn { get; private set; }

    /// <summary>Postgres <c>xmin</c>: the second of two changes at once fails instead of overwriting.</summary>
    public uint Version { get; private set; }

    /// <summary>
    /// The student asks for leave from <paramref name="place"/>: only for a later date, so the day
    /// before at the latest (decided by the developer, 1405/08/19).
    /// </summary>
    /// <param name="today">The gym's today.</param>
    public static Result<LeaveRequest> Create(BookedPlace place, Guid linkId, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(place);

        if (place.Date <= today)
        {
            return Result.Failure<LeaveRequest>(BookingErrors.LeaveTooLate);
        }

        return new LeaveRequest
        {
            TrainerId = place.TrainerId,
            MemberId = place.MemberId,
            LinkId = linkId,
            Date = place.Date,
            Start = place.Start,
            End = place.End,
            Status = LeaveRequestStatus.Pending,
            RequestedOn = today,
        };
    }

    /// <summary>Waiting for the trainer, and the session has not started.</summary>
    public bool IsWaiting(DateOnly today, TimeOnly timeOfDay) =>
        Status == LeaveRequestStatus.Pending && !BookingRules.HasStarted(Date, Start, today, timeOfDay);

    /// <summary>What the request is now: one still waiting once the session started is declined.</summary>
    public LeaveRequestStatus StateOn(DateOnly today, TimeOnly timeOfDay) =>
        Status == LeaveRequestStatus.Pending && !IsWaiting(today, timeOfDay) ? LeaveRequestStatus.Declined : Status;

    /// <summary>The trainer accepts, until the session starts. Taking the student out of the date is the caller's.</summary>
    public Result Accept(DateOnly today, TimeOnly timeOfDay) => Answer(LeaveRequestStatus.Accepted, today, timeOfDay);

    /// <summary>The trainer declines, until the session starts: the student is in it as before.</summary>
    public Result Decline(DateOnly today, TimeOnly timeOfDay) => Answer(LeaveRequestStatus.Declined, today, timeOfDay);

    /// <summary>
    /// The student takes it back, until the session starts: a waiting request («لغو درخواست»), or an
    /// accepted one with «می‌آیم», whose place the caller gives back if there is room.
    /// </summary>
    public Result TakeBack(DateOnly today, TimeOnly timeOfDay)
    {
        if (Status is not (LeaveRequestStatus.Pending or LeaveRequestStatus.Accepted))
        {
            return Result.Failure(BookingErrors.LeaveNotWaiting);
        }

        if (BookingRules.HasStarted(Date, Start, today, timeOfDay))
        {
            return Result.Failure(BookingErrors.SessionStarted);
        }

        Status = LeaveRequestStatus.Cancelled;

        return Result.Success();
    }

    private Result Answer(LeaveRequestStatus answer, DateOnly today, TimeOnly timeOfDay)
    {
        if (!IsWaiting(today, timeOfDay))
        {
            return Result.Failure(BookingErrors.LeaveNotWaiting);
        }

        Status = answer;

        return Result.Success();
    }
}
