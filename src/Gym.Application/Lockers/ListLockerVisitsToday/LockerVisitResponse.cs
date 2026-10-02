namespace Gym.Application.Lockers.ListLockerVisitsToday;

/// <summary>
/// One visit that held a locker today (BUSINESS_RULES.md §6 <i>Who had a locker today</i>): who,
/// and from when to when. Only what the desk's list shows, so it carries the member's name rather
/// than making the screen fetch each member.
/// </summary>
/// <param name="MemberId">
/// <c>null</c> for a guest (§7 <i>Guest visit</i>), listed by <paramref name="GuestName"/> and marked
/// «مهمان», with no profile to link to.
/// </param>
/// <param name="IsCardioOnly">A member's visit for هوازی only (§7 <i>Cardio-only visit</i>), marked «فقط هوازی».</param>
/// <param name="CheckedOutAt"><c>null</c> while the visit is still open.</param>
/// <param name="CancelledAt">
/// Set when the check-in was cancelled (BUSINESS_RULES.md §7). Such a visit is still listed: the
/// member held the key, however briefly.
/// </param>
public sealed record LockerVisitResponse(
    Guid AttendanceId,
    Guid? MemberId,
    string? MemberFullName,
    string? GuestName,
    bool IsCardioOnly,
    DateTimeOffset CheckedInAt,
    DateTimeOffset? CheckedOutAt,
    DateTimeOffset? CancelledAt);
