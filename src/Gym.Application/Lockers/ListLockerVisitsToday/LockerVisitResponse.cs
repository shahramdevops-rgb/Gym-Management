namespace Gym.Application.Lockers.ListLockerVisitsToday;

/// <summary>
/// One visit that held a locker today (BUSINESS_RULES.md §6 <i>Who had a locker today</i>): who,
/// and from when to when. Only what the desk's list shows, so it carries the member's name rather
/// than making the screen fetch each member.
/// </summary>
/// <param name="CheckedOutAt"><c>null</c> while the visit is still open.</param>
/// <param name="CancelledAt">
/// Set when the check-in was cancelled (BUSINESS_RULES.md §7). Such a visit is still listed: the
/// member held the key, however briefly.
/// </param>
public sealed record LockerVisitResponse(
    Guid AttendanceId,
    Guid MemberId,
    string MemberFullName,
    DateTimeOffset CheckedInAt,
    DateTimeOffset? CheckedOutAt,
    DateTimeOffset? CancelledAt);
