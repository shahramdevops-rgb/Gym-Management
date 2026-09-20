using Gym.Domain.Common;

namespace Gym.Domain.Attendances;

public static class AttendanceErrors
{
    /// <summary>BUSINESS_RULES.md §7: check-in needs a subscription to consume a session from.</summary>
    public static readonly Error NoSubscription = Error.BusinessRule(
        "Attendance.NoSubscription",
        "The member has no subscription.");

    /// <summary>BUSINESS_RULES.md §7: a member cannot check in twice without checking out.</summary>
    public static readonly Error AlreadyCheckedIn = Error.BusinessRule(
        "Attendance.AlreadyCheckedIn",
        "The member is already checked in.");

    public static readonly Error NotFound = Error.NotFound(
        "Attendance.NotFound",
        "Attendance not found.");

    /// <summary>BUSINESS_RULES.md §7: check-out and cancel both require an open attendance.</summary>
    public static readonly Error NotOpen = Error.BusinessRule(
        "Attendance.NotOpen",
        "The attendance is already closed.");

    /// <summary>BUSINESS_RULES.md §7: cancel is only allowed within Gym:CancelCheckInWindowMinutes of check-in.</summary>
    public static readonly Error CancelWindowExpired = Error.BusinessRule(
        "Attendance.CancelWindowExpired",
        "The cancel window has passed.");

    /// <summary>
    /// Backstop for the partial unique indexes on member and locker (BUSINESS_RULES.md §7):
    /// two check-ins raced past the precondition checks above.
    /// </summary>
    public static readonly Error ChangedConcurrently = Error.Conflict(
        "Attendance.ChangedConcurrently",
        "Attendance changed at the same moment. Try again.");

    /// <summary>The member history filter (BUSINESS_RULES.md §12: date ranges are inclusive).</summary>
    public static readonly Error InvalidDateRange = Error.Validation(
        "Attendance.InvalidDateRange",
        "'from' must not be after 'to'.");
}
