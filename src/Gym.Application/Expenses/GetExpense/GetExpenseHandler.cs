using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Expenses;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Expenses.GetExpense;

/// <summary>One expense, voided or not, with its category's current name.</summary>
public sealed class GetExpenseHandler(IAppDbContext db)
{
    public async Task<Result<ExpenseResponse>> Handle(Guid id, CancellationToken cancellationToken)
    {
        var found = await db.Expenses.AsNoTracking()
            .Where(expense => expense.Id == id)
            .Join(
                db.ExpenseCategories,
                expense => expense.CategoryId,
                category => category.Id,
                (expense, category) => new { Expense = expense, CategoryName = category.Name })
            .SingleOrDefaultAsync(cancellationToken);

        return found is null
            ? Result.Failure<ExpenseResponse>(ExpenseErrors.NotFound)
            : ExpenseResponse.From(found.Expense, found.CategoryName);
    }
}
