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

    /// <summary>
    /// BUSINESS_RULES.md §7 <i>Cancel check-in</i>: the request must say what happens to the
    /// visit's purchases — the هوازی and which cafe orders — because the server does not guess.
    /// A client mistake, never something the desk can cause from the box.
    /// </summary>
    public static readonly Error CancelChoiceRequired = Error.Validation(
        "Attendance.CancelChoiceRequired",
        "Say whether the visit's cardio is voided and which of its cafe orders are cancelled.");

    /// <summary>
    /// BUSINESS_RULES.md §7 <i>Cancel check-in</i>: a cafe order named for cancelling is not a
    /// standing order of this visit (another visit's, or cancelled since the box was opened).
    /// The whole cancellation is refused so nothing is half done.
    /// </summary>
    public static readonly Error CafeOrderNotOnVisit = Error.BusinessRule(
        "Attendance.CafeOrderNotOnVisit",
        "A cafe order to cancel is not a standing order of this visit.");

    /// <summary>
    /// BUSINESS_RULES.md §7 <i>Cancel check-in</i>: a ticked miscellaneous sale is not a standing
    /// sale of this visit (another visit's, or voided since the box was opened). The whole
    /// cancellation is refused so nothing is half done.
    /// </summary>
    public static readonly Error MiscellaneousSaleNotOnVisit = Error.BusinessRule(
        "Attendance.MiscellaneousSaleNotOnVisit",
        "A miscellaneous sale to void is not a standing sale of this visit.");

    /// <summary>BUSINESS_RULES.md §7 <i>Guest visit</i>: a guest's full name is required.</summary>
    public static readonly Error GuestNameRequired = Error.Validation(
        "Attendance.GuestNameRequired",
        "The guest's full name is required.");

    /// <summary>BUSINESS_RULES.md §7 <i>Guest visit</i>: the same limit as a member's name.</summary>
    public static readonly Error GuestNameTooLong = Error.Validation(
        "Attendance.GuestNameTooLong",
        $"The guest's full name must be at most {Attendance.GuestNameMaxLength} characters.");

    /// <summary>
    /// BUSINESS_RULES.md §7 <i>Guest visit</i>: a guest has no account to leave a debt on, so the
    /// visit is not closed (checked out, or cancelled with an order left standing) while a cafe
    /// order of it is unpaid.
    /// </summary>
    public static readonly Error GuestHasUnpaidCafe = Error.BusinessRule(
        "Attendance.GuestHasUnpaidCafe",
        "The guest has unpaid cafe orders. Settle them first.");

    /// <summary>
    /// BUSINESS_RULES.md §7 <i>Cardio-only visit</i>: no session was consumed, so the هوازی is what
    /// the visit is charged for, and it is recorded (paid or left as debt) before the key comes back.
    /// </summary>
    public static readonly Error CardioChargeMissing = Error.BusinessRule(
        "Attendance.CardioChargeMissing",
        "Record the cardio amount for this cardio-only visit before checking out.");

    /// <summary>Settling a guest's cafe in one step is for a guest's visit only; a member settles their own debt.</summary>
    public static readonly Error NotGuestVisit = Error.BusinessRule(
        "Attendance.NotGuestVisit",
        "This visit is not a guest's.");

    /// <summary>The member history filter (BUSINESS_RULES.md §12: date ranges are inclusive).</summary>
    public static readonly Error InvalidDateRange = Error.Validation(
        "Attendance.InvalidDateRange",
        "'from' must not be after 'to'.");
}
