using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Expenses;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Expenses.UpdateExpense;

/// <summary>
/// Corrects a standing expense (BUSINESS_RULES.md §9). The audit log keeps what it said before, so
/// the edit replaces the row rather than adding a correction beside it. A voided expense is final.
/// </summary>
/// <remarks>
/// Same two concurrency layers as <c>UpdatePlanHandler</c>: the client's <c>Version</c> refuses an
/// edit made on stale data, and <c>xmin</c> refuses a save that races another edit or a void.
/// </remarks>
public sealed class UpdateExpenseHandler(IAppDbContext db, IGymCalendar calendar)
{
    public async Task<Result<ExpenseResponse>> Handle(
        Guid id, UpdateExpenseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var expense = await db.Expenses.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (expense is null)
        {
            return Result.Failure<ExpenseResponse>(ExpenseErrors.NotFound);
        }

        if (expense.Version != command.Version)
        {
            return Result.Failure<ExpenseResponse>(ExpenseErrors.ChangedConcurrently);
        }

        var updated = expense.Update(
            command.Amount,
            command.CategoryId,
            command.ExpenseDate,
            command.Description,
            command.ReferenceNumber,
            calendar.Today());
        if (updated.IsFailure)
        {
            return Result.Failure<ExpenseResponse>(updated.Error);
        }

        var category = await db.ExpenseCategories.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == command.CategoryId, cancellationToken);
        if (category is null)
        {
            return Result.Failure<ExpenseResponse>(ExpenseErrors.CategoryNotFound);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ExpenseResponse>(ExpenseErrors.ChangedConcurrently);
        }

        return ExpenseResponse.From(expense, category.Name);
    }
}
