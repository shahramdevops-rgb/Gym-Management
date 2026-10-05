using Gym.Application.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Reports.GetMembersReport;

/// <summary>
/// The renewal rate, the new members and the single visits that became plans, over a range
/// (BUSINESS_RULES.md §12 <i>Operational reports</i>, roadmap 9.2). Owner only; the endpoint's
/// policy says so.
/// </summary>
/// <remarks>
/// <para>
/// Only membership plans count. A single visit is one day for one visit (§4): it is not renewed and
/// it does not make anyone a member.
/// </para>
/// <para>
/// Whether a plan was renewed depends on when the next one was sold, as a day in the gym's time
/// zone, so the plans that ended and their members' other plans are read as small rows and matched
/// here. A range is at most a year of one gym's plans.
/// </para>
/// </remarks>
public sealed class GetMembersReportHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<MembersReportResponse> Handle(GetMembersReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var from = query.From!.Value;
        var to = query.To!.Value;
        var length = to.DayNumber - from.DayNumber + 1;
        var today = calendar.Today();

        var ended = await EndedAsync(from, to, today, cancellationToken);
        var newMembers = await NewMembersAsync(from, to, cancellationToken);
        var trials = await TrialsAsync(from, to, today, cancellationToken);

        var endedByDay = ended.ToLookup(plan => plan.EndDate);
        var newByDay = newMembers.GroupBy(day => day).ToDictionary(group => group.Key, group => group.Count());

        var days = Enumerable.Range(0, length)
            .Select(offset => from.AddDays(offset))
            .Select(day => new MembersDayResponse(
                day,
                endedByDay[day].Count(),
                endedByDay[day].Count(plan => plan.Renewed),
                endedByDay[day].Count(plan => plan.Waiting),
                newByDay.GetValueOrDefault(day)))
            .ToList();

        return new MembersReportResponse(
            from,
            to,
            ended.Count,
            ended.Count(plan => plan.Renewed),
            ended.Count(plan => plan.Waiting),
            newMembers.Count,
            trials.Count,
            trials.Count(trial => trial.Converted),
            trials.Count(trial => trial.Waiting),
            days);
    }

    /// <summary>
    /// The range's plans that have ended: their last day is in the range and before today (a plan
    /// still covers its last day). A frozen plan has not ended, whatever its end date says (§4).
    /// <para>
    /// A plan is renewed when the member has a later plan, not cancelled, sold no more than 30 days
    /// after the end: before it, as a queued renewal, or after it, as a member coming back. A plan that
    /// is not renewed yet but still has days left in that window is waiting.
    /// </para>
    /// </summary>
    private async Task<List<EndedPlan>> EndedAsync(
        DateOnly from, DateOnly to, DateOnly today, CancellationToken cancellationToken)
    {
        var lastEnded = today.AddDays(-1) < to ? today.AddDays(-1) : to;
        if (lastEnded < from)
        {
            return [];
        }

        var plans = await db.Subscriptions
            .AsNoTracking()
            .Where(plan => !plan.IsSingleSession && plan.CancelledAt == null && plan.FrozenSince == null &&
                plan.EndDate >= from && plan.EndDate <= lastEnded)
            .Select(plan => new { plan.MemberId, plan.StartDate, plan.EndDate })
            .ToListAsync(cancellationToken);

        var memberIds = plans.Select(plan => plan.MemberId).Distinct().ToList();
        var others = await db.Subscriptions
            .AsNoTracking()
            .Where(plan => !plan.IsSingleSession && plan.CancelledAt == null && memberIds.Contains(plan.MemberId))
            .Select(plan => new { plan.MemberId, plan.StartDate, plan.CreatedAt })
            .ToListAsync(cancellationToken);
        var othersByMember = others.ToLookup(plan => plan.MemberId);

        return plans
            .Select(plan =>
            {
                var windowEnd = plan.EndDate.AddDays(ReportThresholds.RenewalWindowDays);
                var renewed = othersByMember[plan.MemberId].Any(next =>
                    next.StartDate > plan.StartDate && calendar.DayOf(next.CreatedAt) <= windowEnd);

                return new EndedPlan(plan.EndDate, renewed, !renewed && today <= windowEnd);
            })
            .ToList();
    }

    /// <summary>
    /// The day each new member's first plan was sold («تاریخ فروش», §4), for the members whose first
    /// falls in the range. A plan sold by mistake and cancelled made nobody a member.
    /// </summary>
    private async Task<List<DateOnly>> NewMembersAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var start = calendar.StartOfDayUtc(from);
        var end = calendar.StartOfDayUtc(to.AddDays(1));

        var firsts = await db.Subscriptions
            .AsNoTracking()
            .Where(plan => !plan.IsSingleSession && plan.CancelledAt == null)
            .GroupBy(plan => plan.MemberId)
            .Select(plans => plans.Min(plan => plan.CreatedAt))
            .Where(first => first >= start && first < end)
            .ToListAsync(cancellationToken);

        return firsts.Select(calendar.DayOf).ToList();
    }

    /// <summary>
    /// New people's single visits and whether they turned into a plan (§12 <i>Operational
    /// reports</i>, decided with the developer, 1405/07/14).
    /// <para>
    /// Each person once, by their first single visit sold in the range, not cancelled. Only people
    /// with no membership plan sold before it count: a former member back for one day is not
    /// trying the gym out. They converted when a membership plan, not cancelled, was sold to them
    /// from that visit on and no more than 30 days after its day. One not converted yet whose 30
    /// days are not over is waiting, like a plan waiting to be renewed.
    /// </para>
    /// </summary>
    private async Task<List<Trial>> TrialsAsync(
        DateOnly from, DateOnly to, DateOnly today, CancellationToken cancellationToken)
    {
        var start = calendar.StartOfDayUtc(from);
        var end = calendar.StartOfDayUtc(to.AddDays(1));

        var firsts = await db.Subscriptions
            .AsNoTracking()
            .Where(visit => visit.IsSingleSession && visit.CancelledAt == null &&
                visit.CreatedAt >= start && visit.CreatedAt < end)
            .GroupBy(visit => visit.MemberId)
            .Select(visits => new { MemberId = visits.Key, SoldAt = visits.Min(visit => visit.CreatedAt) })
            .ToListAsync(cancellationToken);

        var memberIds = firsts.Select(first => first.MemberId).ToList();
        var plans = await db.Subscriptions
            .AsNoTracking()
            .Where(plan => !plan.IsSingleSession && plan.CancelledAt == null && memberIds.Contains(plan.MemberId))
            .Select(plan => new { plan.MemberId, plan.CreatedAt })
            .ToListAsync(cancellationToken);
        var plansByMember = plans.ToLookup(plan => plan.MemberId);

        return firsts
            .Where(first => !plansByMember[first.MemberId].Any(plan => plan.CreatedAt < first.SoldAt))
            .Select(first =>
            {
                var windowEnd = calendar.DayOf(first.SoldAt).AddDays(ReportThresholds.TrialWindowDays);
                var converted = plansByMember[first.MemberId].Any(plan =>
                    plan.CreatedAt >= first.SoldAt && calendar.DayOf(plan.CreatedAt) <= windowEnd);

                return new Trial(converted, !converted && today <= windowEnd);
            })
            .ToList();
    }

    private sealed record EndedPlan(DateOnly EndDate, bool Renewed, bool Waiting);

    private sealed record Trial(bool Converted, bool Waiting);
}
