using System.Linq.Expressions;

using Gym.Domain.Members;

namespace Gym.Application.SiteUsers;

/// <summary>
/// What someone entered when they signed up on the site (BUSINESS_RULES.md §16 <i>Site users</i>),
/// for reading only.
/// </summary>
/// <param name="PhoneNumber">E.164. The frontend formats it for display.</param>
/// <param name="BirthDate">Gregorian; the frontend shows it in the Jalali calendar.</param>
/// <param name="SignedUpAt">When they signed up on the site.</param>
public sealed record SiteUserResponse(
    Guid Id,
    string FullName,
    string PhoneNumber,
    DateOnly BirthDate,
    DateTimeOffset SignedUpAt)
{
    /// <summary>An expression EF Core translates to SQL, so only these columns are read.</summary>
    public static readonly Expression<Func<Member, SiteUserResponse>> Projection = member => new SiteUserResponse(
        member.Id,
        member.FullName,
        member.PhoneNumber,
        member.BirthDate,
        member.CreatedAt);
}
