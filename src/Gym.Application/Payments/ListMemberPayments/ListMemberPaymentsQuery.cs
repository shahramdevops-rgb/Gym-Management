using Gym.Application.Common.Paging;

namespace Gym.Application.Payments.ListMemberPayments;

/// <summary>Bound from the query string: <c>GET /api/members/{memberId}/payments?page=1&amp;pageSize=20</c>.</summary>
public sealed record ListMemberPaymentsQuery(int Page = 1, int PageSize = PagingRules.DefaultPageSize);
