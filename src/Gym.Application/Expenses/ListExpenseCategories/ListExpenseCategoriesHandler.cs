using Gym.Application.Common;
using Gym.Application.Common.Paging;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Expenses.ListExpenseCategories;

/// <summary>
/// Every expense category, by name. Paged like every list (docs/ARCHITECTURE.md), even though a
/// gym has a dozen: the shape of a list response is not worth changing later.
/// </summary>
public sealed class ListExpenseCategoriesHandler(IAppDbContext db)
{
    public async Task<PagedResponse<ExpenseCategoryResponse>> Handle(
        ListExpenseCategoriesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var categories = db.ExpenseCategories.AsNoTracking();

        var totalCount = await categories.CountAsync(cancellationToken);

        // Id breaks ties so paging never repeats or skips a row.
        var items = await categories
            .OrderBy(category => category.NormalizedName)
            .ThenBy(category => category.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ExpenseCategoryResponse.Projection)
            .ToListAsync(cancellationToken);

        return new PagedResponse<ExpenseCategoryResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
