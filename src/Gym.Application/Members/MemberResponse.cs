using System.Linq.Expressions;

using Gym.Domain.Members;

namespace Gym.Application.Members;

/// <param name="PhoneNumber">E.164. The frontend formats it for display.</param>
/// <param name="BirthDate">
/// Gregorian, as every date the API speaks; the frontend shows it in the Jalali calendar. Every
/// member has one (BUSINESS_RULES.md §2); shown on the profile only — never searched or listed.
/// </param>
/// <param name="Version">
/// Sent back with an update. If someone else saved the member after this was read, the update
/// is refused instead of silently overwriting their change.
/// </param>
/// <param name="Debt">
/// What the member still owes over all their non-cancelled subscriptions (BUSINESS_RULES.md §5
/// <i>Member debt</i>), so front-desk staff scanning the list can see who owes money and how much.
/// Computed only by <see cref="ListMembers.ListMembersHandler"/>; every other path (create, update,
/// get by id, activate) leaves it <c>0</c>, because a single member's debt has its own endpoint
/// (<see cref="GetMemberDebt.GetMemberDebtHandler"/>) that also says what it is made of.
/// </param>
/// <param name="CurrentVisit">
/// The member's open visit, checked in and not yet out (BUSINESS_RULES.md §7), or <c>null</c> when
/// they are not inside. The same test check-in uses to refuse a second entry. The front-desk search
/// uses it to offer check-out instead of a check-in that could only be refused, and to name the
/// locker to take back. Like <paramref name="Debt"/>, computed only by
/// <see cref="ListMembers.ListMembersHandler"/>; every other path leaves it <c>null</c>.
/// </param>
/// <param name="IsFrozen">
/// The member has a subscription that is frozen right now (BUSINESS_RULES.md §4 <i>Freeze</i>), so
/// the list can say so next to the member's status. Like <paramref name="Debt"/>, computed only by
/// <see cref="ListMembers.ListMembersHandler"/>; every other path leaves it <c>false</c>.
/// </param>
/// <param name="LastVisitWasSingleSession">
/// The member's latest visit, cancelled ones left out, used a single-session subscription
/// (<see cref="ListMembers.MemberListRow.LastVisitWasSingleSession"/>), so the list can tag them «تک‌جلسه». Like <paramref name="Debt"/>, computed
/// only by <see cref="ListMembers.ListMembersHandler"/>.
/// </param>
/// <param name="PlanEnded">
/// An active member whose plan is over and who has bought nothing after it
/// (<see cref="ListMembers.MemberListRow.PlanEnded"/>), tagged «پلن تمام‌شده».
/// Computed only by <see cref="ListMembers.ListMembersHandler"/>.
/// </param>
/// <param name="Plan">
/// The sessions of the plan the list shows a bar for
/// (<see cref="Domain.Subscriptions.SubscriptionSchedule.CurrentPlan"/>), or <c>null</c> when the
/// member has no plan. Computed only by <see cref="ListMembers.ListMembersHandler"/>.
/// </param>
public sealed record MemberResponse(
    Guid Id,
    string FullName,
    string PhoneNumber,
    string? Notes,
    DateOnly BirthDate,
    bool IsActive,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    decimal Debt = 0,
    MemberCurrentVisit? CurrentVisit = null,
    bool IsFrozen = false,
    bool LastVisitWasSingleSession = false,
    bool PlanEnded = false,
    MemberPlanSessions? Plan = null)
{
    /// <summary>
    /// The same mapping as <see cref="From"/>, as an expression EF Core translates to SQL, so a
    /// read query selects only these columns instead of loading and tracking whole entities.
    /// </summary>
    public static readonly Expression<Func<Member, MemberResponse>> Projection = member => new MemberResponse(
        member.Id,
        member.FullName,
        member.PhoneNumber,
        member.Notes,
        member.BirthDate,
        member.IsActive,
        member.Version,
        member.CreatedAt,
        member.UpdatedAt);

    public static MemberResponse From(Member member)
    {
        ArgumentNullException.ThrowIfNull(member);

        return new MemberResponse(
            member.Id,
            member.FullName,
            member.PhoneNumber,
            member.Notes,
            member.BirthDate,
            member.IsActive,
            member.Version,
            member.CreatedAt,
            member.UpdatedAt);
    }
}

/// <summary>A plan's sessions as the member list's bar shows them: used of total.</summary>
public sealed record MemberPlanSessions(int TotalSessions, int UsedSessions, int RemainingSessions);

/// <summary>An open visit, as the front-desk search needs it to check the member out.</summary>
/// <param name="LockerNumber"><c>null</c> when the visit holds a reserve place (<paramref name="UsesReservePlace"/>).</param>
/// <param name="UsesReservePlace">The visit holds one of the reserve places (BUSINESS_RULES.md §6).</param>
public sealed record MemberCurrentVisit(Guid AttendanceId, int? LockerNumber, bool UsesReservePlace, DateTimeOffset CheckedInAt);
