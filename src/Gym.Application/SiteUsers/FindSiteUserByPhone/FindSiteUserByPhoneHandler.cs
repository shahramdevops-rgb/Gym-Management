using Gym.Application.Common;
using Gym.Application.Members;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.SiteUsers.FindSiteUserByPhone;

/// <summary>
/// The new-member form's look (BUSINESS_RULES.md §16 <i>Site users</i>): does this whole number
/// belong to someone who signed up on the site? Only that exact number finds them, never a part.
/// </summary>
/// <remarks>
/// The form asks while the desk types, so a number that is not a whole valid one yet is simply
/// "nobody", not an error: the form's own validation speaks about the number when it is saved.
/// </remarks>
public sealed class FindSiteUserByPhoneHandler(IAppDbContext db, IPhoneNormalizer phones)
{
    public async Task<SiteUserByPhoneResponse> Handle(string? phoneNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return new SiteUserByPhoneResponse(null);
        }

        var phone = phones.Normalize(phoneNumber);
        if (phone.IsFailure)
        {
            return new SiteUserByPhoneResponse(null);
        }

        var siteUser = await db.Members
            .AsNoTracking()
            .Where(member => member.PhoneNumber == phone.Value)
            .Where(SiteUserRule.IsSiteUser(db))
            .Select(SiteUserResponse.Projection)
            .SingleOrDefaultAsync(cancellationToken);

        return new SiteUserByPhoneResponse(siteUser);
    }
}
