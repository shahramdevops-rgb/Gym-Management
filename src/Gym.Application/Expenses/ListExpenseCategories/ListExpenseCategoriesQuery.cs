using Gym.Application.Common.Paging;

namespace Gym.Application.Expenses.ListExpenseCategories;

/// <summary>Bound from the query string: <c>GET /api/expenses/categories?page=1&amp;pageSize=20</c>.</summary>
public sealed record ListExpenseCategoriesQuery(
    int Page = 1,
    int PageSize = PagingRules.DefaultPageSize);
