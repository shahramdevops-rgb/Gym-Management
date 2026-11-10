using Gym.Domain.Common;
using Gym.Domain.Timetables;

namespace Gym.Domain.Bookings;

public static class BookingErrors
{
    /// <summary>A fixed schedule starts today or later and ends on or after its start, within the timetable's 12 weeks.</summary>
    public static readonly Error ScheduleDatesOutOfRange = Error.BusinessRule(
        "Bookings.ScheduleDatesOutOfRange",
        "A fixed schedule runs from today or later to the timetable's last day, and ends on or after its start.");

    /// <summary>
    /// A fixed schedule ends on the coaching period's last day or before (15.7h); a period that is
    /// over takes no schedule until the trainer renews it.
    /// </summary>
    public static readonly Error ScheduleAfterPeriod = Error.BusinessRule(
        "Bookings.ScheduleAfterPeriod",
        "A fixed schedule ends on the coaching period's last day or before.");

    public static readonly Error ScheduleWithoutSessions = Error.Validation(
        "Bookings.ScheduleWithoutSessions",
        "A fixed schedule has one weekly session at least.");

    public static readonly Error TooManySlots = Error.Validation(
        "Bookings.TooManySlots",
        $"A fixed schedule has {BookingRules.MaxSlots} weekly sessions at most.");

    public static readonly Error DuplicateSlot = Error.Validation(
        "Bookings.DuplicateSlot",
        "A fixed schedule names the same weekly session twice.");

    /// <summary>A weekly session's day and times, as a timetable session's: on the steps, the same day.</summary>
    public static readonly Error InvalidSlot = Error.Validation(
        "Bookings.InvalidSlot",
        $"A weekly session is on one of the seven days, on {TimetableRules.MinuteStep}-minute steps, from 07:00 to 22:00, and ends after it starts.");

    /// <summary>A fixed schedule's weekly session that is on none of its dates: not one of the trainer's sessions.</summary>
    public static readonly Error NoSuchSession = Error.BusinessRule(
        "Bookings.NoSuchSession",
        "That session is not in the trainer's timetable on that date.");

    /// <summary>The fixed schedule or the move reaches a session with no room; the capacity is a ceiling.</summary>
    public static readonly Error SessionFull = Error.BusinessRule(
        "Bookings.SessionFull",
        "A session the student would be in has no room.");

    public static readonly Error NotAStudent = Error.BusinessRule(
        "Bookings.NotAStudent",
        "The member is not the trainer's student now.");

    public static readonly Error NoSchedule = Error.NotFound(
        "Bookings.NoSchedule",
        "The student has no fixed schedule now.");

    public static readonly Error NotInSession = Error.NotFound(
        "Bookings.NotInSession",
        "The member is not in that session on that date.");

    /// <summary>A date is left, moved or taken back until the session starts (decided by the developer, 1405/08/09).</summary>
    public static readonly Error SessionStarted = Error.BusinessRule(
        "Bookings.SessionStarted",
        "The session has started; it can no longer be left, moved or taken back.");

    /// <summary>«می‌آیم» takes back only a date the member left themselves, not one moved or taken out.</summary>
    public static readonly Error NotLeft = Error.NotFound(
        "Bookings.NotLeft",
        "The member has not left that session on that date.");

    public static readonly Error AlreadyInSession = Error.BusinessRule(
        "Bookings.AlreadyInSession",
        "The member is already in that session.");

    /// <summary>A move goes to a date from today to the fixed schedule's last one.</summary>
    public static readonly Error MoveOutOfSchedule = Error.BusinessRule(
        "Bookings.MoveOutOfSchedule",
        "A session can be moved to a date up to the fixed schedule's last one.");

    /// <summary>A student's attendance is read from a date to one on or after it, <see cref="PresenceRules.MaxRangeDays"/> days at most.</summary>
    public static readonly Error AttendanceRangeInvalid = Error.Validation(
        "Bookings.AttendanceRangeInvalid",
        $"Attendance is read from a date to one on or after it, {PresenceRules.MaxRangeDays} days at most.");

    /// <summary>The trainer's day of attendance is today or an earlier day: a later one has none (task 15.7d).</summary>
    public static readonly Error AttendanceDayInFuture = Error.Validation(
        "Bookings.AttendanceDayInFuture",
        "Attendance is read for today or an earlier day.");

    /// <summary>Leave is asked for by the day before the session (decided by the developer, 1405/08/19, task 15.7i).</summary>
    public static readonly Error LeaveTooLate = Error.BusinessRule(
        "Bookings.LeaveTooLate",
        "Leave is asked for by the day before the session.");

    /// <summary>A waiting or accepted request for that session already exists.</summary>
    public static readonly Error LeaveAlreadyAsked = Error.Conflict(
        "Bookings.LeaveAlreadyAsked",
        "A leave request for that session is already waiting or accepted.");

    public static readonly Error LeaveRequestNotFound = Error.NotFound(
        "Bookings.LeaveRequestNotFound",
        "No leave request with that id waits for this trainer.");

    /// <summary>Answered, taken back, or the session started meanwhile (not answered by then is declined).</summary>
    public static readonly Error LeaveNotWaiting = Error.Conflict(
        "Bookings.LeaveNotWaiting",
        "The leave request is no longer waiting for an answer.");
}
