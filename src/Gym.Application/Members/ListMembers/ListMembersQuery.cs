using Gym.Application.Common.Paging;

namespace Gym.Application.Members.ListMembers;

/// <summary>
/// Bound from the query string: <c>GET /api/members?search=علی&amp;isActive=true&amp;page=1&amp;pageSize=20</c>.
/// </summary>
/// <param name="Search">
/// A name, part of a name, a phone number, or at least four of its digits. Blank means "everyone".
/// </param>
/// <param name="IsActive">Only active (<c>true</c>) or inactive (<c>false</c>) members; omitted means both.</param>
public sealed record ListMembersQuery(
    string? Search = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = PagingRules.DefaultPageSize);
