using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Expenses;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Expenses.UpdateExpenseCategory;

/// <summary>
/// Renames a category, a seeded one included. No expense copies the name, so the new one shows on
/// every expense at once (BUSINESS_RULES.md §9).
/// </summary>
/// <remarks>
/// Same two concurrency layers as <c>UpdatePlanHandler</c>: the client's <c>Version</c> refuses an
/// edit made on stale data, and <c>xmin</c> refuses a save that races another.
/// </remarks>
public sealed class UpdateExpenseCategoryHandler(IAppDbContext db)
{
    public async Task<Result<ExpenseCategoryResponse>> Handle(
        Guid id, UpdateExpenseCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await db.ExpenseCategories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return Result.Failure<ExpenseCategoryResponse>(ExpenseCategoryErrors.NotFound);
        }

        if (category.Version != command.Version)
        {
            return Result.Failure<ExpenseCategoryResponse>(ExpenseCategoryErrors.ChangedConcurrently);
        }

        var renamed = category.Rename(command.Name);
        if (renamed.IsFailure)
        {
            return Result.Failure<ExpenseCategoryResponse>(renamed.Error);
        }

        // Keeping its own name is fine; taking another category's is not.
        var normalizedName = category.NormalizedName;
        if (await db.ExpenseCategories.AnyAsync(
                c => c.NormalizedName == normalizedName && c.Id != id, cancellationToken))
        {
            return Result.Failure<ExpenseCategoryResponse>(ExpenseCategoryErrors.NameAlreadyExists);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception)
            when (exception.ConstraintName == ExpenseConstraints.UniqueCategoryName)
        {
            return Result.Failure<ExpenseCategoryResponse>(ExpenseCategoryErrors.NameAlreadyExists);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ExpenseCategoryResponse>(ExpenseCategoryErrors.ChangedConcurrently);
        }

        return ExpenseCategoryResponse.From(category);
    }
}
