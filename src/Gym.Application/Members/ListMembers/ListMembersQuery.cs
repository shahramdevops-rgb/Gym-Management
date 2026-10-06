using Gym.Application.Common.Paging;

namespace Gym.Application.Members.ListMembers;

/// <summary>
/// Bound from the query string: <c>GET /api/members?search=علی&amp;isActive=true&amp;debtorsOnly=true&amp;page=1&amp;pageSize=20</c>.
/// </summary>
/// <param name="Search">
/// A name, part of a name, a phone number, or at least four of its digits. Blank means "everyone".
/// </param>
/// <param name="IsActive">Only active (<c>true</c>) or inactive (<c>false</c>) members; omitted means both.</param>
/// <param name="DebtorsOnly">
/// Only members who owe something (BUSINESS_RULES.md §5 <i>Member debt</i>); omitted or
/// <c>false</c> means everyone. Combines with <paramref name="IsActive"/> and the search.
/// </param>
/// <param name="SingleSessionOnly">
/// Only members tagged «تک‌جلسه»: their latest visit was a single visit (BUSINESS_RULES.md §2,
/// <see cref="MemberListRow.LastVisitWasSingleSession"/>). Combines with the other filters.
/// </param>
/// <param name="PlanEndedOnly">
/// Only members tagged «پلن تمام‌شده» (BUSINESS_RULES.md §2, <see cref="MemberListRow.PlanEnded"/>).
/// Combines with the other filters.
/// </param>
public sealed record ListMembersQuery(
    string? Search = null,
    bool? IsActive = null,
    bool DebtorsOnly = false,
    bool SingleSessionOnly = false,
    bool PlanEndedOnly = false,
    int Page = 1,
    int PageSize = PagingRules.DefaultPageSize);
