using Gym.Application.Common;
using Gym.Application.Common.Paging;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Cafe.ListProducts;

/// <summary>
/// The price list, paged like every list (docs/ARCHITECTURE.md), sellable products first and then
/// grouped by category — the order both the till and the products screen want.
/// </summary>
public sealed class ListProductsHandler(IAppDbContext db)
{
    public async Task<PagedResponse<ProductResponse>> Handle(
        ListProductsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Joined before filtering, because "sellable" is a question about both rows
        // (BUSINESS_RULES.md §8): a product in a category that is switched off cannot be sold,
        // however switched on the product itself is.
        var rows = db.Products
            .AsNoTracking()
            .Join(
                db.ProductCategories.AsNoTracking(),
                product => product.CategoryId,
                category => category.Id,
                (product, category) => new { Product = product, Category = category });

        if (query.CategoryId is { } categoryId)
        {
            rows = rows.Where(row => row.Product.CategoryId == categoryId);
        }

        if (query.IsActive is { } isActive)
        {
            rows = rows.Where(row => (row.Product.IsActive && row.Category.IsActive) == isActive);
        }

        var totalCount = await rows.CountAsync(cancellationToken);

        // Sellable first, because those are the ones being sold; then by category and name. Id
        // breaks ties so paging never repeats or skips a row.
        var items = await rows
            .OrderByDescending(row => row.Product.IsActive && row.Category.IsActive)
            .ThenBy(row => row.Category.NormalizedName)
            .ThenBy(row => row.Product.NormalizedName)
            .ThenBy(row => row.Product.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(row => new ProductResponse(
                row.Product.Id,
                row.Product.Name,
                row.Product.CategoryId,
                row.Category.Name,
                row.Product.Price,
                row.Product.IsActive,
                row.Category.IsActive,
                row.Product.Version,
                row.Product.CreatedAt,
                row.Product.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResponse<ProductResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
