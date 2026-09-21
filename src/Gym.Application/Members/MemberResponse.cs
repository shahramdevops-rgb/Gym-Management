using System.Linq.Expressions;

using Gym.Domain.Members;

namespace Gym.Application.Members;

/// <param name="PhoneNumber">E.164. The frontend formats it for display.</param>
/// <param name="Version">
/// Sent back with an update. If someone else saved the member after this was read, the update
/// is refused instead of silently overwriting their change.
/// </param>
/// <param name="HasUnpaidSubscription">
/// True when any of this member's non-cancelled subscriptions has net paid below its price
/// (BUSINESS_RULES.md §4 payment status). Computed only by <see cref="ListMembers.ListMembersHandler"/>,
/// which is the only caller that needs it (the member list, task 4.6 follow-up: front-desk staff
/// scanning for who still owes money); every other path (create, update, get by id, activate)
/// leaves it <see langword="false"/> since a single-member response already shows the real
/// subscription and payment history where it matters.
/// </param>
public sealed record MemberResponse(
    Guid Id,
    string FullName,
    string PhoneNumber,
    string? Notes,
    bool IsActive,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool HasUnpaidSubscription = false)
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
            member.IsActive,
            member.Version,
            member.CreatedAt,
            member.UpdatedAt);
    }
}
