using Gym.Application.Common.Paging;

namespace Gym.Application.Notifications.Broadcasts.ListBroadcasts;

/// <summary>Bound from the query string: <c>GET /api/sms/broadcasts?page=1</c>.</summary>
public sealed record ListBroadcastsQuery(int Page = 1, int PageSize = PagingRules.DefaultPageSize);
