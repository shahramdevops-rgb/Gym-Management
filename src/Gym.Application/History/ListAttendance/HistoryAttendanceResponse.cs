namespace Gym.Application.History.ListAttendance;

/// <summary>One visit in the gym's history (BUSINESS_RULES.md §12 <i>History</i>).</summary>
/// <param name="LockerNumber">
/// <c>null</c> on a reserve place (<paramref name="UsesReservePlace"/>), or on a visit closed
/// before roadmap 6.5.5 when no locker was free.
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
    Guid MemberId,
    string MemberFullName,
    int? LockerNumber,
    bool UsesReservePlace,
    DateTimeOffset CheckedInAt,
    DateTimeOffset? CheckedOutAt,
    DateTimeOffset? CancelledAt,
    DateTimeOffset? AutoClosedAt,
    string? CheckedInByFullName);
