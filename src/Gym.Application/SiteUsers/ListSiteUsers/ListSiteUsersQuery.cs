using Gym.Application.Common.Paging;

namespace Gym.Application.SiteUsers.ListSiteUsers;

/// <summary>
/// Bound from the query string: <c>GET /api/site-users?search=4567&amp;page=1&amp;pageSize=20</c>.
/// </summary>
/// <param name="Search">
/// A name, part of a name, a whole phone number, or its last 4 digits, as the member list's box
/// (<see cref="Members.MemberSearch"/>). Blank means every site user.
/// </param>
public sealed record ListSiteUsersQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = PagingRules.DefaultPageSize);
