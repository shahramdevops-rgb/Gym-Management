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

    /// <summary>
    /// Backstop for the partial unique indexes on member and locker (BUSINESS_RULES.md §7):
    /// two check-ins raced past the precondition checks above.
    /// </summary>
    public static readonly Error ChangedConcurrently = Error.Conflict(
        "Attendance.ChangedConcurrently",
        "Attendance changed at the same moment. Try again.");
}
