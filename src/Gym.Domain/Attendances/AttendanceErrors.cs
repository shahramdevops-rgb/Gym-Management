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
    /// BUSINESS_RULES.md §7: the locker the desk chose already has an open visit. The same error
    /// whether it was taken long ago or a moment ago by another desk (the partial unique index on
    /// the locker decides that race), because the desk's next step is the same: choose another.
    /// </summary>
    public static readonly Error LockerTaken = Error.Conflict(
        "Attendance.LockerTaken",
        "Someone else already holds that locker.");

    /// <summary>BUSINESS_RULES.md §6: a reserve place is only for when no locker is both in service and free.</summary>
    public static readonly Error LockersStillFree = Error.BusinessRule(
        "Attendance.LockersStillFree",
        "A locker is still free, so a reserve place cannot be used.");

    /// <summary>BUSINESS_RULES.md §6: at most Attendance.ReservePlaceCount visits with no locker at once.</summary>
    public static readonly Error ReserveFull = Error.BusinessRule(
        "Attendance.ReserveFull",
        "Every reserve place is in use.");

    /// <summary>BUSINESS_RULES.md §7 <i>Moving to another locker</i>: the visit already holds that locker.</summary>
    public static readonly Error SameLocker = Error.BusinessRule(
        "Attendance.SameLocker",
        "The visit already holds that locker.");

    /// <summary>
    /// Backstop for the partial unique indexes on member and reserve place (BUSINESS_RULES.md §6,
    /// §7), and for the visit's own <c>xmin</c>: two requests raced past the checks before them.
    /// </summary>
    public static readonly Error ChangedConcurrently = Error.Conflict(
        "Attendance.ChangedConcurrently",
        "Attendance changed at the same moment. Try again.");

    /// <summary>
    /// The sale sent with a check-in (roadmap 6.5.7) is neither a single visit with no numbers nor
    /// a plan. A client mistake, never something the desk can cause from the box.
    /// </summary>
    public static readonly Error SaleInvalid = Error.Validation(
        "Attendance.SaleInvalid",
        "A sale at check-in is a single visit with no days or sessions, or a plan with both.");

    /// <summary>The member history filter (BUSINESS_RULES.md §12: date ranges are inclusive).</summary>
    public static readonly Error InvalidDateRange = Error.Validation(
        "Attendance.InvalidDateRange",
        "'from' must not be after 'to'.");
}
