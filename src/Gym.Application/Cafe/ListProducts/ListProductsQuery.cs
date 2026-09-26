using Gym.Application.Common.Paging;

namespace Gym.Application.Cafe.ListProducts;

/// <summary>
/// Bound from the query string:
/// <c>GET /api/cafe/products?categoryId=...&amp;isActive=true&amp;page=1&amp;pageSize=20</c>.
/// </summary>
/// <param name="CategoryId">One category's products; omitted means every category.</param>
/// <param name="IsActive">
/// Whether the product is <b>sellable</b>: switched on and in a category that is switched on
/// (BUSINESS_RULES.md §8). Omitted means both. The till and the product search on an order ask
/// for <c>true</c>, so a ناموجود item is never offered; the management screen omits it and sees
/// everything with its two flags.
/// </param>
public sealed record ListProductsQuery(
    Guid? CategoryId = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = PagingRules.DefaultPageSize);
