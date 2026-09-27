using Gym.Application.Common;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Expenses.ListExpenses;

/// <summary>
/// Expenses newest first, filtered by date range and category, with the total of what the filter
/// matches (BUSINESS_RULES.md §9). Owner only.
/// </summary>
public sealed class ListExpensesHandler(IAppDbContext db)
{
    public async Task<ExpenseListResponse> Handle(ListExpensesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var expenses = db.Expenses.AsNoTracking();

        if (query.From is { } from)
        {
            expenses = expenses.Where(expense => expense.ExpenseDate >= from);
        }

        if (query.To is { } to)
        {
            expenses = expenses.Where(expense => expense.ExpenseDate <= to);
        }

        if (query.CategoryId is { } categoryId)
        {
            expenses = expenses.Where(expense => expense.CategoryId == categoryId);
        }

        // Voided expenses never count toward what went out, whether or not they are listed.
        var totalAmount = await expenses
            .Where(expense => expense.VoidedAt == null)
            .SumAsync(expense => expense.Amount, cancellationToken);

        if (!query.IncludeVoided)
        {
            expenses = expenses.Where(expense => expense.VoidedAt == null);
        }

        var totalCount = await expenses.CountAsync(cancellationToken);

        // Newest day first, and within a day the latest entry first; Id breaks the last tie so
        // paging never repeats or skips a row.
        var rows = await expenses
            .Join(
                db.ExpenseCategories,
                expense => expense.CategoryId,
                category => category.Id,
                (expense, category) => new { Expense = expense, CategoryName = category.Name })
            .OrderByDescending(row => row.Expense.ExpenseDate)
            .ThenByDescending(row => row.Expense.CreatedAt)
            .ThenByDescending(row => row.Expense.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => ExpenseResponse.From(row.Expense, row.CategoryName)).ToList();

        return new ExpenseListResponse(items, query.Page, query.PageSize, totalCount, totalAmount);
    }
}
