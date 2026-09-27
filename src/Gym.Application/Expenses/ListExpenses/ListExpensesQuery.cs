using Gym.Application.Common.Paging;

namespace Gym.Application.Expenses.ListExpenses;

/// <summary>
/// Bound from the query string:
/// <c>GET /api/expenses?from=2026-09-01&amp;to=2026-09-30&amp;categoryId=...&amp;includeVoided=false&amp;page=1&amp;pageSize=20</c>.
/// </summary>
/// <param name="From">
/// Inclusive, compared with <c>ExpenseDate</c>. <c>null</c> means no lower bound
/// (BUSINESS_RULES.md §12: date ranges are inclusive, in the gym's time zone).
/// </param>
/// <param name="To">Inclusive. <c>null</c> means no upper bound.</param>
/// <param name="CategoryId">One category's expenses; omitted means every category.</param>
/// <param name="IncludeVoided">
/// Whether voided expenses are listed too, marked as such. On by default: the list is the record
/// of what was entered, the same choice the cafe's order history makes. The total never counts
/// them either way.
/// </param>
public sealed record ListExpensesQuery(
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? CategoryId = null,
    bool IncludeVoided = true,
    int Page = 1,
    int PageSize = PagingRules.DefaultPageSize);
