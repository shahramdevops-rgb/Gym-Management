using System.Linq.Expressions;

using Gym.Domain.Attendances;
using Gym.Domain.Lockers;
using Gym.Domain.Members;

namespace Gym.Application.Attendances.ListCurrentlyInside;

/// <summary>One row of the front desk's "currently inside" board (BUSINESS_RULES.md §7).</summary>
/// <param name="LockerNumber"><c>null</c> when the member checked in with no locker free.</param>
public sealed record CurrentlyInsideResponse(
    Guid AttendanceId,
    Guid MemberId,
    string MemberFullName,
    Guid? LockerId,
    int? LockerNumber,
    DateTimeOffset CheckedInAt)
{
    /// <summary>
    /// Correlates each open attendance to its member and locker in one query, the same shape as
    /// <c>LockerResponse.Projection</c> and <c>AttendanceResponse.Projection</c>.
    /// </summary>
    public static Expression<Func<Attendance, CurrentlyInsideResponse>> Projection(
        IQueryable<Member> members, IQueryable<Locker> lockers) =>
        attendance => new CurrentlyInsideResponse(
            attendance.Id,
            attendance.MemberId,
            members.Where(m => m.Id == attendance.MemberId).Select(m => m.FullName).First(),
            attendance.LockerId,
            lockers.Where(l => l.Id == attendance.LockerId).Select(l => (int?)l.Number).FirstOrDefault(),
            attendance.CheckedInAt);
}
