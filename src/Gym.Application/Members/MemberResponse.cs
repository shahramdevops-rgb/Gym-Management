using System.Linq.Expressions;

using Gym.Domain.Members;

namespace Gym.Application.Members;

/// <param name="PhoneNumber">E.164. The frontend formats it for display.</param>
/// <param name="BirthDate">
/// Gregorian, as every date the API speaks; the frontend shows it in the Jalali calendar. Null for
/// most members (BUSINESS_RULES.md §2), and shown on the profile only — never searched or listed.
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
public sealed record MemberResponse(
    Guid Id,
    string FullName,
    string PhoneNumber,
    string? Notes,
    DateOnly? BirthDate,
    bool IsActive,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    decimal Debt = 0)
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
