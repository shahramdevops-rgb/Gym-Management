using System.Linq.Expressions;

using Gym.Domain.Attendances;
using Gym.Domain.Lockers;

namespace Gym.Application.Attendances;

/// <param name="LockerId">
/// <c>null</c> means no locker was free at check-in (BUSINESS_RULES.md §7) — the front desk's
/// no-locker warning. There is no separate warning flag; a null locker is the warning.
/// </param>
/// <param name="LockerNumber">Alongside <paramref name="LockerId"/>, so the front desk can show it without a second call.</param>
/// <param name="CheckedOutAt"><c>null</c> while the visit is still open.</param>
/// <param name="CancelledAt"><c>null</c> unless the check-in was cancelled (BUSINESS_RULES.md §7).</param>
public sealed record AttendanceResponse(
    Guid Id,
    Guid MemberId,
    Guid SubscriptionId,
    Guid? LockerId,
    int? LockerNumber,
    DateTimeOffset CheckedInAt,
    DateTimeOffset? CheckedOutAt,
    DateTimeOffset? CancelledAt,
    DateTimeOffset CreatedAt)
{
    /// <summary>
    /// The same mapping as <see cref="From"/>, as an expression EF Core translates to SQL. Takes
    /// the lockers as a queryable, not <c>IAppDbContext</c> directly, the same shape as
    /// <c>LockerResponse.Projection</c>, so the caller controls what "lockers" means once.
    /// </summary>
    public static Expression<Func<Attendance, AttendanceResponse>> Projection(IQueryable<Locker> lockers) =>
        attendance => new AttendanceResponse(
            attendance.Id,
            attendance.MemberId,
            attendance.SubscriptionId,
            attendance.LockerId,
            lockers.Where(l => l.Id == attendance.LockerId).Select(l => (int?)l.Number).FirstOrDefault(),
            attendance.CheckedInAt,
            attendance.CheckedOutAt,
            attendance.CancelledAt,
            attendance.CreatedAt);

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
            attendance.CheckedOutAt,
            attendance.CancelledAt,
            attendance.CreatedAt);
    }
}
