namespace Gym.Application.History.ListAttendance;

/// <summary>One visit in the gym's history (BUSINESS_RULES.md §12 <i>History</i>).</summary>
/// <param name="MemberId">
/// <c>null</c> for a guest's visit (BUSINESS_RULES.md §7 <i>Guest visit</i>), which has no member and
/// no profile to link to: <paramref name="GuestName"/> says who it was.
/// </param>
/// <param name="MemberFullName"><c>null</c> for a guest's visit.</param>
/// <param name="GuestName">The name typed at the desk for a guest's visit; <c>null</c> for a member's.</param>
/// <param name="LockerNumber">
/// <c>null</c> on a reserve place (<paramref name="UsesReservePlace"/>), or on a visit closed
/// before roadmap 6.5.5 when no locker was free.
/// </param>
/// <param name="IsCardioOnly">
/// A member's visit for هوازی only, which consumed no session (BUSINESS_RULES.md §7 <i>Cardio-only
/// visit</i>); the screen marks it «فقط هوازی».
/// </param>
/// <param name="CheckedOutAt"><c>null</c> while the visit is still open.</param>
/// <param name="CancelledAt">Set when the check-in was cancelled. The row is listed and marked, never hidden.</param>
/// <param name="AutoClosedAt">
/// Set when the nightly job closed the visit, which no user did: the screen says «خودکار».
/// </param>
/// <param name="CheckedInByFullName">
/// Who checked the member in (the row's <c>CreatedBy</c>), or <c>null</c> for a row nobody's
/// request wrote.
/// </param>
public sealed record HistoryAttendanceResponse(
    Guid Id,
    Guid? MemberId,
    string? MemberFullName,
    string? GuestName,
    int? LockerNumber,
    bool UsesReservePlace,
    bool IsCardioOnly,
    DateTimeOffset CheckedInAt,
    DateTimeOffset? CheckedOutAt,
    DateTimeOffset? CancelledAt,
    DateTimeOffset? AutoClosedAt,
    string? CheckedInByFullName);
