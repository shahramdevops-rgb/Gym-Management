using System.Linq.Expressions;

using Gym.Application.ServiceCharges;
using Gym.Domain.Attendances;
using Gym.Domain.Lockers;
using Gym.Domain.Members;
using Gym.Domain.Subscriptions;

namespace Gym.Application.Attendances.ListCurrentlyInside;

/// <summary>One row of the front desk's "currently inside" board (BUSINESS_RULES.md §7).</summary>
/// <param name="LockerNumber"><c>null</c> when the member checked in with no locker free.</param>
/// <param name="TotalSessions"><c>null</c> means unlimited, and there is then nothing to count against.</param>
/// <param name="RemainingSessions"><c>null</c> means unlimited.</param>
/// <param name="SubscriptionEndDate">
/// Of the subscription this visit consumed from, which the attendance names outright
/// (<c>Attendance.SubscriptionId</c>). Reading it from there rather than re-deriving "the one in
/// effect today" means the board can never show a different subscription than the one check-in used.
/// </param>
/// <param name="ServiceCharges">
/// The visit's non-voided charges (BUSINESS_RULES.md §7 <i>Gym services</i>), so the board can
/// show and take a هوازی amount without opening the member's profile. Attached by
/// <c>VisitServiceCharges</c> after this projection runs, for the reason given on
/// <see cref="AttendanceResponse"/>.
/// </param>
public sealed record CurrentlyInsideResponse(
    Guid AttendanceId,
    Guid MemberId,
    string MemberFullName,
    Guid? LockerId,
    int? LockerNumber,
    DateTimeOffset CheckedInAt,
    Guid SubscriptionId,
    int? TotalSessions,
    int UsedSessions,
    int? RemainingSessions,
    DateOnly SubscriptionEndDate,
    IReadOnlyList<ServiceChargeResponse> ServiceCharges)
{
    /// <summary>
    /// Correlates each open attendance to its member, locker and subscription in one query, the
    /// same shape as <c>LockerResponse.Projection</c> and <c>AttendanceResponse.Projection</c>.
    /// </summary>
    /// <remarks>
    /// Every subscription value here is a stored column, so the whole row is one SQL statement.
    /// The subscription's <i>status</i> is deliberately absent: it is calculated in C# and could
    /// not be part of this, and the board has no use for it — check-in refuses a subscription that
    /// is not usable today, so the badge would read "Active" on every row (BUSINESS_RULES.md §7,
    /// <i>The "currently inside" board</i>). What the desk needs is how close the subscription is
    /// to running out, which these four values answer.
    /// </remarks>
    public static Expression<Func<Attendance, CurrentlyInsideResponse>> Projection(
        IQueryable<Member> members, IQueryable<Locker> lockers, IQueryable<Subscription> subscriptions) =>
        attendance => new CurrentlyInsideResponse(
            attendance.Id,
            attendance.MemberId,
            members.Where(m => m.Id == attendance.MemberId).Select(m => m.FullName).First(),
            attendance.LockerId,
            lockers.Where(l => l.Id == attendance.LockerId).Select(l => (int?)l.Number).FirstOrDefault(),
            attendance.CheckedInAt,
            attendance.SubscriptionId,
            subscriptions.Where(s => s.Id == attendance.SubscriptionId).Select(s => s.TotalSessions).First(),
            subscriptions.Where(s => s.Id == attendance.SubscriptionId).Select(s => s.UsedSessions).First(),
            subscriptions.Where(s => s.Id == attendance.SubscriptionId).Select(s => s.TotalSessions - s.UsedSessions).First(),
            subscriptions.Where(s => s.Id == attendance.SubscriptionId).Select(s => s.EndDate).First(),
            new List<ServiceChargeResponse>());
}
