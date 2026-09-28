using System.Linq.Expressions;

using Gym.Application.Cafe;
using Gym.Application.ServiceCharges;
using Gym.Domain.Attendances;
using Gym.Domain.Lockers;
using Gym.Domain.Members;
using Gym.Domain.Subscriptions;

namespace Gym.Application.Attendances.ListCurrentlyInside;

/// <summary>One row of the front desk's "currently inside" board (BUSINESS_RULES.md §7).</summary>
/// <param name="LockerNumber"><c>null</c> when the visit holds a reserve place instead (<paramref name="UsesReservePlace"/>).</param>
/// <param name="UsesReservePlace">The visit holds one of the reserve places (BUSINESS_RULES.md §6); the board shows "رزرو".</param>
/// <param name="SubscriptionEndDate">
/// Of the subscription this visit consumed from, which the attendance names outright
/// (<c>Attendance.SubscriptionId</c>). Reading it from there rather than re-deriving "the one in
/// effect today" means the board can never show a different subscription than the one check-in used.
/// </param>
/// <param name="IsSingleSession">
/// One visit, today only (BUSINESS_RULES.md §4). The board shows "تک‌جلسه‌ای" for it instead of a
/// session bar that would always read "۱ از ۱", and never marks it as running out: it is spent by
/// design and expires tonight, so both marks would fire on every such row and mean nothing.
/// </param>
/// <param name="ServiceCharges">
/// The visit's non-voided charges (BUSINESS_RULES.md §7 <i>Gym services</i>), so the board can
/// show and take a هوازی amount without opening the member's profile. Attached by
/// <c>VisitServiceCharges</c> after this projection runs, for the reason given on
/// <see cref="AttendanceResponse"/>.
/// </param>
/// <param name="CafeOrders">
/// What the member bought from the cafe during this visit and has not had cancelled
/// (BUSINESS_RULES.md §8), attached by <c>VisitCafeOrders</c> the same way.
/// </param>
/// <param name="MemberBirthDate">
/// For the desk panel's birthday list (BUSINESS_RULES.md §6 <i>The desk panel</i>). Sent as the
/// stored Gregorian date; the frontend matches it against today by the Jalali month and day.
/// </param>
/// <param name="HasQueuedRenewal">
/// The member has already bought the next subscription: one that is not cancelled, not frozen, not
/// a single visit, and starts after today (§4 <c>Upcoming</c>). The desk panel leaves such a member
/// out of its renewal list (§6 <i>The desk panel</i>); the board does not use it.
/// </param>
public sealed record CurrentlyInsideResponse(
    Guid AttendanceId,
    Guid MemberId,
    string MemberFullName,
    Guid? LockerId,
    int? LockerNumber,
    bool UsesReservePlace,
    DateTimeOffset CheckedInAt,
    Guid SubscriptionId,
    int TotalSessions,
    int UsedSessions,
    int RemainingSessions,
    DateOnly SubscriptionEndDate,
    bool IsSingleSession,
    IReadOnlyList<ServiceChargeResponse> ServiceCharges,
    IReadOnlyList<CafeOrderResponse> CafeOrders,
    DateOnly? MemberBirthDate,
    bool HasQueuedRenewal)
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
    ///
    /// <c>HasQueuedRenewal</c> is <see cref="Subscription.GetStatus"/> returning <c>Upcoming</c>,
    /// written as the columns it reads so it becomes an SQL <c>EXISTS</c> inside the same statement.
    /// The check-in handler asks the same question in C# for its <c>NextStartsTomorrow</c> refusal.
    /// </remarks>
    public static Expression<Func<Attendance, CurrentlyInsideResponse>> Projection(
        IQueryable<Member> members, IQueryable<Locker> lockers, IQueryable<Subscription> subscriptions, DateOnly today) =>
        attendance => new CurrentlyInsideResponse(
            attendance.Id,
            attendance.MemberId,
            members.Where(m => m.Id == attendance.MemberId).Select(m => m.FullName).First(),
            attendance.LockerId,
            lockers.Where(l => l.Id == attendance.LockerId).Select(l => (int?)l.Number).FirstOrDefault(),
            attendance.ReserveSlot != null,
            attendance.CheckedInAt,
            attendance.SubscriptionId,
            subscriptions.Where(s => s.Id == attendance.SubscriptionId).Select(s => s.TotalSessions).First(),
            subscriptions.Where(s => s.Id == attendance.SubscriptionId).Select(s => s.UsedSessions).First(),
            subscriptions.Where(s => s.Id == attendance.SubscriptionId).Select(s => s.TotalSessions - s.UsedSessions).First(),
            subscriptions.Where(s => s.Id == attendance.SubscriptionId).Select(s => s.EndDate).First(),
            subscriptions.Where(s => s.Id == attendance.SubscriptionId).Select(s => s.IsSingleSession).First(),
            new List<ServiceChargeResponse>(),
            new List<CafeOrderResponse>(),
            members.Where(m => m.Id == attendance.MemberId).Select(m => m.BirthDate).First(),
            subscriptions.Any(s => s.MemberId == attendance.MemberId
                && s.CancelledAt == null
                && s.FrozenSince == null
                && !s.IsSingleSession
                && s.StartDate > today));
}
