using System.Linq.Expressions;

using Gym.Domain.Members;

namespace Gym.Application.Members;

/// <param name="PhoneNumber">E.164. The frontend formats it for display.</param>
/// <param name="Version">
/// Sent back with an update. If someone else saved the member after this was read, the update
/// is refused instead of silently overwriting their change.
/// </param>
public sealed record MemberResponse(
    Guid Id,
    string FullName,
    string PhoneNumber,
    string? Notes,
    bool IsActive,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt)
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
