using Gym.Domain.Attendances;

namespace Gym.Application.Attendances;

/// <param name="LockerId">
/// <c>null</c> means no locker was free at check-in (BUSINESS_RULES.md §7) — the front desk's
/// no-locker warning. There is no separate warning flag; a null locker is the warning.
/// </param>
/// <param name="LockerNumber">Alongside <paramref name="LockerId"/>, so the front desk can show it without a second call.</param>
public sealed record AttendanceResponse(
    Guid Id,
    Guid MemberId,
    Guid SubscriptionId,
    Guid? LockerId,
    int? LockerNumber,
    DateTimeOffset CheckedInAt,
    DateTimeOffset CreatedAt)
{
    public static AttendanceResponse From(Attendance attendance, int? lockerNumber)
    {
        ArgumentNullException.ThrowIfNull(attendance);

        return new AttendanceResponse(
            attendance.Id,
            attendance.MemberId,
            attendance.SubscriptionId,
            attendance.LockerId,
            lockerNumber,
            attendance.CheckedInAt,
            attendance.CreatedAt);
    }
}
