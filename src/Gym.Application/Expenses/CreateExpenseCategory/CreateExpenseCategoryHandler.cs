using Gym.Application.Common;
using Gym.Domain.Common;
using Gym.Domain.Expenses;

using Microsoft.EntityFrameworkCore;

namespace Gym.Application.Expenses.CreateExpenseCategory;

/// <summary>Adds a heading beside the eight seeded ones (BUSINESS_RULES.md §9). Owner only.</summary>
public sealed class CreateExpenseCategoryHandler(IAppDbContext db)
{
    public async Task<Result<ExpenseCategoryResponse>> Handle(
        CreateExpenseCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // The entity normalizes the name, so build the category first and compare its normalized form.
        var created = ExpenseCategory.Create(command.Name);
        if (created.IsFailure)
        {
            return Result.Failure<ExpenseCategoryResponse>(created.Error);
        }

        var category = created.Value;
        if (await db.ExpenseCategories.AnyAsync(c => c.NormalizedName == category.NormalizedName, cancellationToken))
        {
            return Result.Failure<ExpenseCategoryResponse>(ExpenseCategoryErrors.NameAlreadyExists);
        }

        // Two requests can pass the check above at once; the unique index decides.
        db.ExpenseCategories.Add(category);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException exception)
            when (exception.ConstraintName == ExpenseConstraints.UniqueCategoryName)
        {
            return Result.Failure<ExpenseCategoryResponse>(ExpenseCategoryErrors.NameAlreadyExists);
        }

        return ExpenseCategoryResponse.From(category);
    }
}
