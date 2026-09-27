using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Expenses;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Expenses.VoidExpense;

/// <summary>
/// Takes an expense out of the totals without erasing it (BUSINESS_RULES.md §9, §5: financial
/// records are never deleted). Owner only.
/// </summary>
/// <remarks>
/// There is no money to give back, unlike a voided service charge: the gym paid this out, and
/// voiding it only says the entry was wrong. So nothing else is written, and <c>xmin</c> alone
/// settles two voids, or a void and an edit, racing each other.
/// </remarks>
public sealed class VoidExpenseHandler(IAppDbContext db, TimeProvider time, ICurrentUser currentUser)
{
    public async Task<Result<ExpenseResponse>> Handle(
        Guid id, VoidExpenseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var expense = await db.Expenses.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (expense is null)
        {
            return Result.Failure<ExpenseResponse>(ExpenseErrors.NotFound);
        }

        // The endpoint's policy requires an authenticated user, so this is a wiring bug if hit.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException("Voiding an expense was called without an authenticated user.");

        var voided = expense.Void(command.Reason, time.GetUtcNow(), userId);
        if (voided.IsFailure)
        {
            return Result.Failure<ExpenseResponse>(voided.Error);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ExpenseResponse>(ExpenseErrors.ChangedConcurrently);
        }

        var category = await db.ExpenseCategories.AsNoTracking()
            .SingleAsync(c => c.Id == expense.CategoryId, cancellationToken);

        return ExpenseResponse.From(expense, category.Name);
    }
}
