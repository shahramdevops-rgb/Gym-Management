using System.Linq.Expressions;

using Gym.Application.Common;
using Gym.Domain.Members;

namespace Gym.Application.Members;

/// <summary>
/// Who is a site user (BUSINESS_RULES.md §16 <i>Site users</i>): someone who signed up on the site,
/// was never brought in at the desk, and has never had a plan, a visit or a cafe order on their
/// name (the cafe: the developer, 1405/08/19, since someone who bought there has been to the gym
/// and may owe for it). One place, so the member
/// list, the broadcast and the «کاربران سایت» page all draw the same line, the way
/// <see cref="MemberDebt.OwesSomething"/> owns "owes something".
/// </summary>
/// <remarks>
/// <para>
/// <b>Worked out, not stored.</b> Only where the member came from is a column
/// (<see cref="Member.RegisteredAtDesk"/>); "never a plan, never a visit" is read from the plans
/// and the visits each time. So no sale and no check-in, today's or a future one, has to remember
/// to turn a site user into a member: the row it saves does it.
/// </para>
/// <para>
/// <b>Never back.</b> Plans, visits and cafe orders are cancelled, never deleted, and a cancelled one still
/// counts, so someone who became a member stays one (the developer, 1405/08/19).
/// </para>
/// <para>
/// Two expressions rather than one and a "not": EF Core translates each to plain SQL
/// (<c>NOT EXISTS</c> / <c>EXISTS</c>), and both use the indexes that start with <c>member_id</c>.
/// </para>
/// </remarks>
public static class SiteUserRule
{
    /// <summary>A site user: only the «کاربران سایت» page and the new-member form's phone look use it.</summary>
    public static Expression<Func<Member, bool>> IsSiteUser(IAppDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);

        return member =>
            !member.RegisteredAtDesk
            && !db.Subscriptions.Any(subscription => subscription.MemberId == member.Id)
            && !db.Attendances.Any(attendance => attendance.MemberId == member.Id)
            && !db.CafeOrders.Any(order => order.MemberId == member.Id);
    }

    /// <summary>Everyone the gym's own screens work with: every member who is not a site user.</summary>
    public static Expression<Func<Member, bool>> IsGymMember(IAppDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);

        return member =>
            member.RegisteredAtDesk
            || db.Subscriptions.Any(subscription => subscription.MemberId == member.Id)
            || db.Attendances.Any(attendance => attendance.MemberId == member.Id)
            || db.CafeOrders.Any(order => order.MemberId == member.Id);
    }
}
