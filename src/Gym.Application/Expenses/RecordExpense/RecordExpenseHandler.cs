using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Expenses;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Expenses.RecordExpense;

/// <summary>Writes down money the gym paid out (BUSINESS_RULES.md §9). Owner only.</summary>
public sealed class RecordExpenseHandler(IAppDbContext db, IGymCalendar calendar, ICurrentUser currentUser)
{
    public async Task<Result<ExpenseResponse>> Handle(RecordExpenseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Recording an expense was called without an authenticated user.");

        var recorded = Expense.Record(
            command.Amount,
            command.CategoryId,
            command.ExpenseDate,
            command.Description,
            command.ReferenceNumber,
            userId,
            calendar.Today());
        if (recorded.IsFailure)
        {
            return Result.Failure<ExpenseResponse>(recorded.Error);
        }

        var category = await db.ExpenseCategories.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == command.CategoryId, cancellationToken);
        if (category is null)
        {
            return Result.Failure<ExpenseResponse>(ExpenseErrors.CategoryNotFound);
        }

        var expense = recorded.Value;
        db.Expenses.Add(expense);
        await db.SaveChangesAsync(cancellationToken);

        return ExpenseResponse.From(expense, category.Name);
    }
}
