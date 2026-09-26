using Gym.Application.Common.Paging;

namespace Gym.Application.Cafe.ListProductCategories;

/// <summary>
/// Bound from the query string:
/// <c>GET /api/cafe/categories?isActive=true&amp;page=1&amp;pageSize=20</c>.
/// </summary>
/// <param name="IsActive">
/// Only the switched-on (<c>true</c>) or switched-off (<c>false</c>) headings; omitted means both.
/// The till asks for <c>true</c>, so a ناموجود shelf is never offered (BUSINESS_RULES.md §8).
/// </param>
public sealed record ListProductCategoriesQuery(
    bool? IsActive = null,
    int Page = 1,
    int PageSize = PagingRules.DefaultPageSize);
