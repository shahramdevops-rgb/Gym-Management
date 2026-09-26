using Gym.Application.Common;
using Gym.Application.Common.Paging;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Cafe.ListProductCategories;

/// <summary>
/// The cafe's headings, switched-on ones first and then by name. Paged like every list
/// (docs/ARCHITECTURE.md), even though a gym cafe has a handful: the shape of a list response is
/// not worth changing later.
/// </summary>
public sealed class ListProductCategoriesHandler(IAppDbContext db)
{
    public async Task<PagedResponse<ProductCategoryResponse>> Handle(
        ListProductCategoriesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var categories = db.ProductCategories.AsNoTracking();

        if (query.IsActive is { } isActive)
        {
            categories = categories.Where(category => category.IsActive == isActive);
        }

        var totalCount = await categories.CountAsync(cancellationToken);

        // Id breaks ties so paging never repeats or skips a row.
        var items = await categories
            .OrderByDescending(category => category.IsActive)
            .ThenBy(category => category.NormalizedName)
            .ThenBy(category => category.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ProductCategoryResponse.Projection)
            .ToListAsync(cancellationToken);

        return new PagedResponse<ProductCategoryResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
