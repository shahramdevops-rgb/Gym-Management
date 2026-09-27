namespace Gym.Application.Expenses.ListExpenses;

/// <summary>
/// One page of expenses, plus the total of every expense the filter matches — across all pages,
/// voided ones left out (BUSINESS_RULES.md §9).
/// </summary>
/// <remarks>
/// The same fields as <c>PagedResponse</c> with one more, rather than a wrapper around it, so the
/// frontend's pager reads this list exactly as it reads every other one.
/// </remarks>
/// <param name="TotalAmount">
/// What went out in the filtered range. The client cannot add it up itself: it sees one page.
/// </param>
public sealed record ExpenseListResponse(
    IReadOnlyList<ExpenseResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    decimal TotalAmount);
