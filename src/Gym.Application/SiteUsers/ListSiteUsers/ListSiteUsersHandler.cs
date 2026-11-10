using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Application.Members;
using Gym.Domain.Common.Text;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.SiteUsers.ListSiteUsers;

/// <summary>
/// The «کاربران سایت» page (BUSINESS_RULES.md §16 <i>Site users</i>): everyone who signed up on the
/// site and has had no plan and no visit, newest first, paged. The only list that shows them.
/// </summary>
public sealed class ListSiteUsersHandler(IAppDbContext db, MemberSearch search)
{
    public async Task<PagedResponse<SiteUserResponse>> Handle(ListSiteUsersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var siteUsers = db.Members.AsNoTracking().Where(SiteUserRule.IsSiteUser(db));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            siteUsers = search.Apply(siteUsers, PersianText.Normalize(query.Search));
        }

        var totalCount = await siteUsers.CountAsync(cancellationToken);

        // Newest sign-up first; Id (a version 7 GUID, in creation order) breaks ties so nobody is
        // shown twice or skipped from one page to the next.
        var items = await siteUsers
            .OrderByDescending(member => member.CreatedAt)
            .ThenByDescending(member => member.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(SiteUserResponse.Projection)
            .ToListAsync(cancellationToken);

        return new PagedResponse<SiteUserResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
