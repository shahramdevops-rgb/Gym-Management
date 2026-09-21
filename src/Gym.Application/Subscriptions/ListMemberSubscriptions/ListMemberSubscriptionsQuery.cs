using Gym.Application.Common.Paging;

namespace Gym.Application.Subscriptions.ListMemberSubscriptions;

/// <summary>Bound from the query string: <c>GET /api/members/{memberId}/subscriptions?page=1&amp;pageSize=20</c>.</summary>
public sealed record ListMemberSubscriptionsQuery(int Page = 1, int PageSize = PagingRules.DefaultPageSize);
