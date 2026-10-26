using Gym.Domain.Members;

namespace Gym.Application.SiteAuth.GetMe;

/// <summary>
/// The signed-in member as the site shows them (BUSINESS_RULES.md §15): the panel's own details,
/// never changed from the site. <see cref="IsActive"/> false is the «غیرفعال» notice, and the
/// site then shows nothing else.
/// </summary>
public sealed record SiteMeResponse(Guid MemberId, string FullName, string PhoneNumber, DateOnly BirthDate, bool IsActive)
{
    public static SiteMeResponse From(Member member)
    {
        ArgumentNullException.ThrowIfNull(member);

        return new SiteMeResponse(member.Id, member.FullName, member.PhoneNumber, member.BirthDate, member.IsActive);
    }
}
