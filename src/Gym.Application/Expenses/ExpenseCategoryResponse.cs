using System.Linq.Expressions;

using Gym.Domain.Expenses;

namespace Gym.Application.Expenses;

/// <param name="Version">Sent back with a rename, so a stale edit is refused.</param>
public sealed record ExpenseCategoryResponse(
    Guid Id,
    string Name,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt)
{
    /// <summary>The same mapping as <see cref="From"/>, as an expression EF Core translates to SQL.</summary>
    public static readonly Expression<Func<ExpenseCategory, ExpenseCategoryResponse>> Projection =
        category => new ExpenseCategoryResponse(
            category.Id,
            category.Name,
            category.Version,
            category.CreatedAt,
            category.UpdatedAt);

    public static ExpenseCategoryResponse From(ExpenseCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);

        return new ExpenseCategoryResponse(
            category.Id,
            category.Name,
            category.Version,
            category.CreatedAt,
            category.UpdatedAt);
    }
}
