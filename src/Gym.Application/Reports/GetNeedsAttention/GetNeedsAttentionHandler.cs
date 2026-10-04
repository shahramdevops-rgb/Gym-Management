using Gym.Application.Common;
using Gym.Application.History.ListSales;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Reports.GetNeedsAttention;

/// <summary>
/// The dashboard's «نیاز به اقدام» lists (BUSINESS_RULES.md §12 <i>Needs attention</i>, roadmap
/// 9.2). Owner only; the endpoint's policy says so.
/// </summary>
/// <remarks>
/// <para>
/// Only membership plans are read: a single visit is one day for one visit (§4) and says nothing
/// about whether a member is staying. The plan lists leave out deactivated members, whom the gym has
/// already let go. The debt list does not: money owed is owed whoever owes it.
/// </para>
/// <para>
/// A plan's status is calculated, never stored (§4), so the conditions of
/// <c>Subscription.GetStatus</c> are written out as SQL here, as the snapshot does.
/// </para>
/// </remarks>
public sealed class GetNeedsAttentionHandler(IAppDbContext db, IGymCalendar calendar, SaleRows saleRows)
{
    public async Task<NeedsAttentionResponse> Handle(CancellationToken cancellationToken)
    {
        var today = calendar.Today();

        var runningOut = await RunningOutAsync(today, cancellationToken);
        var left = await LeftAsync(today, cancellationToken);
        var absent = await AbsentAsync(today, cancellationToken);
        var (oldDebts, withoutMember) = await OldDebtsAsync(today, cancellationToken);

        return new NeedsAttentionResponse(today, runningOut, left, absent, oldDebts, withoutMember);
    }

    /// <summary>
    /// A plan in its dates and not frozen, with 3 sessions left or fewer (none at all included) or
    /// ending within 5 days, and nothing bought after it: the board's thresholds, for every member
    /// rather than only those inside (§6 <i>The desk panel</i>).
    /// </summary>
    private async Task<List<RunningOutResponse>> RunningOutAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var expiringBy = today.AddDays(ReportThresholds.ExpiringWithinDays);

        return await (
                from plan in db.Subscriptions.AsNoTracking()
                join member in db.Members.AsNoTracking() on plan.MemberId equals member.Id
                where member.IsActive && !plan.IsSingleSession && plan.CancelledAt == null &&
                    plan.FrozenSince == null && plan.StartDate <= today && plan.EndDate >= today &&
                    (plan.TotalSessions - plan.UsedSessions <= ReportThresholds.LowSessions || plan.EndDate <= expiringBy) &&
                    !db.Subscriptions.Any(next => next.MemberId == plan.MemberId && !next.IsSingleSession &&
                        next.CancelledAt == null && next.StartDate > today)
                orderby plan.EndDate, plan.TotalSessions - plan.UsedSessions, member.FullName
                select new RunningOutResponse(
                    member.Id, member.FullName, member.PhoneNumber, plan.TotalSessions - plan.UsedSessions, plan.EndDate))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// A member whose latest plan ended in the last 30 days, yesterday included and today not (a plan
    /// still covers its last day), with no plan after it. A frozen plan's end date can pass while it
    /// is frozen; its member is away on purpose, not gone, so they are left out.
    /// </summary>
    private async Task<List<LeftResponse>> LeftAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var since = today.AddDays(-ReportThresholds.RenewalWindowDays);

        var lastPlans = db.Subscriptions
            .AsNoTracking()
            .Where(plan => !plan.IsSingleSession && plan.CancelledAt == null)
            .GroupBy(plan => plan.MemberId)
            .Select(plans => new
            {
                MemberId = plans.Key,
                EndedOn = plans.Max(plan => plan.EndDate),
                Frozen = plans.Count(plan => plan.FrozenSince != null),
            })
            .Where(last => last.Frozen == 0 && last.EndedOn >= since && last.EndedOn < today);

        return await (
                from last in lastPlans
                join member in db.Members.AsNoTracking() on last.MemberId equals member.Id
                where member.IsActive
                orderby last.EndedOn descending, member.FullName
                select new LeftResponse(member.Id, member.FullName, member.PhoneNumber, last.EndedOn))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// A member with a plan usable today (§4 <c>Active</c>) and no visit for 10 days or more. Days
    /// away count from the last visit, or from the plan's start when they have not come on it: a plan
    /// bought yesterday is not a member who stopped coming. A cancelled check-in is not a visit; a
    /// cardio-only one is.
    /// </summary>
    private async Task<List<AbsentResponse>> AbsentAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var candidates = await (
                from plan in db.Subscriptions.AsNoTracking()
                join member in db.Members.AsNoTracking() on plan.MemberId equals member.Id
                where member.IsActive && !plan.IsSingleSession && plan.CancelledAt == null &&
                    plan.FrozenSince == null && plan.StartDate <= today && plan.EndDate >= today &&
                    plan.UsedSessions < plan.TotalSessions
                select new
                {
                    member.Id,
                    member.FullName,
                    member.PhoneNumber,
                    plan.StartDate,
                    LastVisitAt = db.Attendances
                        .Where(visit => visit.MemberId == member.Id && visit.CancelledAt == null)
                        .Max(visit => (DateTimeOffset?)visit.CheckedInAt),
                })
            .ToListAsync(cancellationToken);

        return candidates
            .Select(candidate =>
            {
                DateOnly? lastVisitOn = candidate.LastVisitAt is { } moment ? calendar.DayOf(moment) : null;
                var awaySince = lastVisitOn is { } day && day > candidate.StartDate ? day : candidate.StartDate;

                return new AbsentResponse(
                    candidate.Id,
                    candidate.FullName,
                    candidate.PhoneNumber,
                    lastVisitOn,
                    today.DayNumber - awaySince.DayNumber);
            })
            .Where(row => row.DaysAway >= ReportThresholds.AbsentDays)
            .OrderByDescending(row => row.DaysAway)
            .ThenBy(row => row.FullName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// What each buyer owes on sales recorded more than 30 days ago: the receivables' oldest age
    /// (§12 <i>Receivables</i>), read from the same <see cref="SaleRows"/>, so the list and that
    /// figure always add up to the same amount.
    /// </summary>
    private async Task<(List<OldDebtResponse> Members, decimal WithoutMember)> OldDebtsAsync(
        DateOnly today, CancellationToken cancellationToken)
    {
        var oldBefore = calendar.StartOfDayUtc(today.AddDays(-ReportThresholds.OldDebtDays));

        var owed = await saleRows.Matching(new EverySale())
            .Where(sale => sale.UndoneAt == null && sale.NetPaid < sale.Amount && sale.SoldAt < oldBefore)
            .GroupBy(sale => sale.MemberId)
            .Select(sales => new
            {
                MemberId = sales.Key,
                Owed = sales.Sum(sale => sale.Amount - sale.NetPaid),
                OldestSoldAt = sales.Min(sale => sale.SoldAt),
            })
            .ToListAsync(cancellationToken);

        var memberIds = owed.Where(row => row.MemberId is not null).Select(row => row.MemberId!.Value).ToList();
        var members = await db.Members
            .AsNoTracking()
            .Where(member => memberIds.Contains(member.Id))
            .ToDictionaryAsync(member => member.Id, cancellationToken);

        var rows = owed
            .Where(row => row.MemberId is not null)
            .Select(row =>
            {
                var member = members[row.MemberId!.Value];

                return new OldDebtResponse(
                    member.Id, member.FullName, member.PhoneNumber, row.Owed, calendar.DayOf(row.OldestSoldAt));
            })
            .OrderByDescending(row => row.Owed)
            .ThenBy(row => row.FullName, StringComparer.Ordinal)
            .ToList();

        return (rows, owed.Where(row => row.MemberId is null).Sum(row => row.Owed));
    }

    /// <summary>No range, no member, every kind: everything ever sold.</summary>
    private sealed record EverySale(
        DateOnly? From = null,
        DateOnly? To = null,
        Guid? MemberId = null,
        SaleSource? Source = null,
        SalePaidFilter? Paid = null) : ISalesFilter;
}
