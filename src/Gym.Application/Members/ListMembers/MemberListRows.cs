using Gym.Application.Common;
using Gym.Domain.Members;

namespace Gym.Application.Members.ListMembers;

/// <summary>
/// A member as the list sorts, filters and tags them (BUSINESS_RULES.md §2), every value worked out
/// by the database.
/// </summary>
/// <remarks>
/// A class with settable properties rather than a positional record: EF Core can filter and sort on
/// the properties of an object it built with an initializer, but not on one it built through a
/// constructor.
/// </remarks>
public sealed class MemberListRow
{
    public required Member Member { get; init; }

    /// <summary>The latest visit that was not cancelled; <c>null</c> for a member who never came.</summary>
    public DateTimeOffset? LastCheckInAt { get; init; }

    /// <summary>Tagged «تک‌جلسه».</summary>
    public bool LastVisitWasSingleSession { get; init; }

    /// <summary>Tagged «پلن تمام‌شده».</summary>
    public bool PlanEnded { get; init; }
}

/// <summary>Builds <see cref="MemberListRow"/>s from the members a query has already narrowed.</summary>
/// <remarks>
/// <para>
/// The tags are filters too, and a filter must run in the query, before the count and the paging, or
/// page 2 would not continue where page 1 stopped. So each tag is written once, here, and the handler
/// both filters and shows the same property: a filter lists exactly the members its tag is on.
/// </para>
/// <para>
/// A member never carries both tags (developer, 1405/07/15): when their latest visit was a single
/// visit and their plan has also ended, only what happened more recently is shown.
/// </para>
/// </remarks>
public static class MemberListRows
{
    public static IQueryable<MemberListRow> From(IQueryable<Member> members, IAppDbContext db, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(db);

        return members
            .Select(member => new
            {
                Member = member,
                LastCheckInAt = db.Attendances
                    .Where(attendance => attendance.MemberId == member.Id && attendance.CancelledAt == null)
                    .Max(attendance => (DateTimeOffset?)attendance.CheckedInAt),

                // The day of the latest visit when that visit used a single visit, else null. A
                // single visit covers only the day it was sold and used (§4), so its StartDate is the
                // visit's day, a business date with no time zone to convert.
                SingleVisitOn = db.Attendances
                    .Where(attendance => attendance.MemberId == member.Id && attendance.CancelledAt == null)
                    .OrderByDescending(attendance => attendance.CheckedInAt)
                    .Select(attendance => db.Subscriptions
                        .Where(subscription => subscription.Id == attendance.SubscriptionId && subscription.IsSingleSession)
                        .Select(subscription => (DateOnly?)subscription.StartDate)
                        .FirstOrDefault())
                    .FirstOrDefault(),

                // Plans only: single visits are not plans, and cancelled subscriptions are nothing.
                HadPlan = db.Subscriptions.Any(subscription =>
                    subscription.MemberId == member.Id
                    && subscription.CancelledAt == null
                    && !subscription.IsSingleSession),

                // Still live: frozen, or its end date not passed with sessions left. That covers
                // Active and Upcoming (§4 status order), since a queued plan cannot have used a session.
                HasLivePlan = db.Subscriptions.Any(subscription =>
                    subscription.MemberId == member.Id
                    && subscription.CancelledAt == null
                    && !subscription.IsSingleSession
                    && (subscription.FrozenSince != null
                        || (subscription.EndDate >= today && subscription.UsedSessions < subscription.TotalSessions))),

                // The plan that ended last, when they have all ended.
                LastPlanEndDate = db.Subscriptions
                    .Where(subscription => subscription.MemberId == member.Id
                        && subscription.CancelledAt == null
                        && !subscription.IsSingleSession)
                    .OrderByDescending(subscription => subscription.EndDate)
                    .Select(subscription => (DateOnly?)subscription.EndDate)
                    .FirstOrDefault(),
                LastPlanHadSessionsLeft = db.Subscriptions
                    .Where(subscription => subscription.MemberId == member.Id
                        && subscription.CancelledAt == null
                        && !subscription.IsSingleSession)
                    .OrderByDescending(subscription => subscription.EndDate)
                    .Select(subscription => subscription.UsedSessions < subscription.TotalSessions)
                    .FirstOrDefault(),
            })
            .Select(row => new
            {
                row.Member,
                row.LastCheckInAt,
                row.SingleVisitOn,

                // A deactivated member has been let go, so nobody is chasing their renewal (§9).
                Ended = row.Member.IsActive && row.HadPlan && !row.HasLivePlan,

                // Which came later, when both apply. A plan that ran out of sessions ended at its last
                // session, a visit before the single visit (the latest visit). A plan that still had
                // sessions ended with its last day, which is after the single visit when that day is
                // the visit's day or later.
                EndedAfterSingleVisit = row.LastPlanHadSessionsLeft && row.LastPlanEndDate >= row.SingleVisitOn,
            })
            .Select(row => new MemberListRow
            {
                Member = row.Member,
                LastCheckInAt = row.LastCheckInAt,
                LastVisitWasSingleSession = row.SingleVisitOn != null && !(row.Ended && row.EndedAfterSingleVisit),
                PlanEnded = row.Ended && (row.SingleVisitOn == null || row.EndedAfterSingleVisit),
            });
    }
}
