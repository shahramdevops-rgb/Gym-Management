using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Domain.Common.Text;
using Gym.Domain.Members;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Members.ListMembers;

/// <summary>
/// The member list and the front-desk search box, as one paged query (BUSINESS_RULES.md §2).
/// </summary>
/// <remarks>
/// <para>
/// The search box's rules are <see cref="MemberSearch"/>'s, shared with the «کاربران سایت» page.
/// </para>
/// <para>
/// <b>Site users are never here</b> (BUSINESS_RULES.md §16 <i>Site users</i>): not listed, not
/// counted, not found by any search, whatever the filters (<see cref="SiteUserRule"/>).
/// </para>
/// </remarks>
public sealed class ListMembersHandler(IAppDbContext db, MemberSearch search, IGymCalendar calendar)
{
    public async Task<PagedResponse<MemberResponse>> Handle(ListMembersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var today = calendar.Today();
        var members = db.Members.AsNoTracking().Where(SiteUserRule.IsGymMember(db));

        if (query.IsActive is { } isActive)
        {
            members = members.Where(member => member.IsActive == isActive);
        }

        if (query.DebtorsOnly)
        {
            // In the query, before the count and the paging, not on the page afterwards.
            members = members.Where(MemberDebt.OwesSomething(db));
        }

        if (query.FrozenOnly)
        {
            // A cancelled plan is never frozen in effect (§4 Cancellation), and a single visit cannot be.
            members = members.Where(member => db.Subscriptions.Any(s =>
                s.MemberId == member.Id && s.FrozenSince != null && s.CancelledAt == null));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            members = search.Apply(members, PersianText.Normalize(query.Search));
        }

        // The tags are filters too, so they are worked out in the query, before the count and the
        // paging (MemberListRows), and the same values are shown on the rows.
        var rows = MemberListRows.From(members, db, today);

        if (query.SingleSessionOnly)
        {
            rows = rows.Where(row => row.LastVisitWasSingleSession);
        }

        if (query.PlanEndedOnly)
        {
            rows = rows.Where(row => row.PlanEnded);
        }

        var totalCount = await rows.CountAsync(cancellationToken);

        // The latest visit first (BUSINESS_RULES.md §2): the people the desk sees are at the top. A
        // cancelled check-in was not a visit. Members who never came go last, by name; Id breaks
        // ties so nobody is shown twice or skipped from one page to the next. Postgres puts nulls
        // first in a descending order, so "never came" is sorted on explicitly.
        var pageRows = await rows
            .OrderBy(row => row.LastCheckInAt == null)
            .ThenByDescending(row => row.LastCheckInAt)
            .ThenBy(row => row.Member.NormalizedFullName)
            .ThenBy(row => row.Member.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(row => new { row.Member, row.LastVisitWasSingleSession, row.PlanEnded })
            .ToListAsync(cancellationToken);

        // Batched for the whole page (MemberDebt), not one query per row.
        var pageIds = pageRows.Select(row => row.Member.Id).ToList();
        var debtByMemberId = await MemberDebt.GetTotalsAsync(db, pageIds, cancellationToken);

        // The same test as CheckInHandler's "already inside": an attendance not yet checked out.
        // The partial unique index allows at most one per member.
        var visitByMemberId = await db.Attendances
            .Where(attendance => attendance.MemberId != null && pageIds.Contains(attendance.MemberId.Value) && attendance.CheckedOutAt == null)
            .Select(attendance => new
            {
                MemberId = attendance.MemberId!.Value,
                Visit = new MemberCurrentVisit(
                    attendance.Id,
                    db.Lockers.Where(locker => locker.Id == attendance.LockerId).Select(locker => (int?)locker.Number).FirstOrDefault(),
                    attendance.ReserveSlot != null,
                    attendance.CheckedInAt),
            })
            .ToDictionaryAsync(row => row.MemberId, row => row.Visit, cancellationToken);

        // The same test as Subscription.GetStatus's Frozen: frozen and not cancelled. No date is
        // involved, since a freeze lasts until someone ends it.
        var frozenMemberIds = await db.Subscriptions
            .Where(subscription => pageIds.Contains(subscription.MemberId)
                && subscription.FrozenSince != null
                && subscription.CancelledAt == null)
            .Select(subscription => subscription.MemberId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Whole entities, so the domain picks the plan the bar shows (SubscriptionSchedule). A page
        // holds few members, each with a handful of plans.
        var membershipsByMemberId = (await db.Subscriptions
                .AsNoTracking()
                .Where(subscription => pageIds.Contains(subscription.MemberId)
                    && subscription.CancelledAt == null
                    && !subscription.IsSingleSession)
                .ToListAsync(cancellationToken))
            .ToLookup(subscription => subscription.MemberId);

        var items = pageRows
            .Select(row =>
            {
                var member = row.Member;
                var plan = SubscriptionSchedule.CurrentPlan(today, membershipsByMemberId[member.Id]);

                return MemberResponse.From(member) with
                {
                    Debt = debtByMemberId.GetValueOrDefault(member.Id),
                    CurrentVisit = visitByMemberId.GetValueOrDefault(member.Id),
                    IsFrozen = frozenMemberIds.Contains(member.Id),
                    LastVisitWasSingleSession = row.LastVisitWasSingleSession,
                    PlanEnded = row.PlanEnded,
                    Plan = plan is null ? null : new MemberPlanSessions(plan.TotalSessions, plan.UsedSessions, plan.RemainingSessions),
                };
            })
            .ToList();

        return new PagedResponse<MemberResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
