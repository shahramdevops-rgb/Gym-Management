using System.Linq.Expressions;

using Gym.Application.ServiceCharges;
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
/// <param name="AutoClosedAt"><c>null</c> unless the nightly job closed this visit instead of the member checking out (BUSINESS_RULES.md §7 Auto-checkout).</param>
/// <param name="ServiceCharges">
/// The visit's non-voided charges (BUSINESS_RULES.md §7 <i>Gym services</i>) — today at most one,
/// for هوازی. Voided ones are left out: the screen shows what this visit costs, and a void is read
/// in the audit log rather than in the front desk's list. Attached by
/// <see cref="VisitServiceCharges"/> rather than projected, because the charge's payment status is
/// calculated in C# and EF Core cannot translate it into SQL.
/// </param>
/// <param name="MemberDebt">
/// What the member owed when they walked in (BUSINESS_RULES.md §5 <i>Member debt</i>, §7). Money
/// owed never blocks a check-in, so this is a warning for the front desk to mention, the same way
/// a null locker is: the visit is already recorded by the time it is read. Filled in by check-in,
/// which is where the front desk needs it; the history and "currently inside" lists leave it
/// <c>0</c> and show the member's debt on their profile instead.
/// </param>
public sealed record AttendanceResponse(
    Guid Id,
    Guid MemberId,
    Guid SubscriptionId,
    Guid? LockerId,
    int? LockerNumber,
    DateTimeOffset CheckedInAt,
    DateTimeOffset? CheckedOutAt,
    DateTimeOffset? CancelledAt,
    DateTimeOffset? AutoClosedAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ServiceChargeResponse> ServiceCharges,
    decimal MemberDebt = 0)
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
            attendance.AutoClosedAt,
            attendance.CreatedAt,
            new List<ServiceChargeResponse>());

    public static AttendanceResponse From(
        Attendance attendance,
        int? lockerNumber,
        IReadOnlyList<ServiceChargeResponse>? serviceCharges = null,
        decimal memberDebt = 0)
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
            attendance.AutoClosedAt,
            attendance.CreatedAt,
            serviceCharges ?? [],
            memberDebt);
    }
}
