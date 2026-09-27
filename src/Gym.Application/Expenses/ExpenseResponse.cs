using Gym.Domain.Expenses;

namespace Gym.Application.Expenses;

/// <param name="CategoryName">The category's current name: nothing copies it, so a rename shows everywhere.</param>
/// <param name="Version">Sent back with an edit, so a stale edit is refused.</param>
public sealed record ExpenseResponse(
    Guid Id,
    decimal Amount,
    Guid CategoryId,
    string CategoryName,
    DateOnly ExpenseDate,
    string Description,
    string? ReferenceNumber,
    Guid RecordedByUserId,
    bool IsVoided,
    DateTimeOffset? VoidedAt,
    string? VoidReason,
    Guid? VoidedByUserId,
    uint Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt)
{
    public static ExpenseResponse From(Expense expense, string categoryName)
    {
        ArgumentNullException.ThrowIfNull(expense);
        ArgumentNullException.ThrowIfNull(categoryName);

        return new ExpenseResponse(
            expense.Id,
            expense.Amount,
            expense.CategoryId,
            categoryName,
            expense.ExpenseDate,
            expense.Description,
            expense.ReferenceNumber,
            expense.RecordedByUserId,
            expense.IsVoided,
            expense.VoidedAt,
            expense.VoidReason,
            expense.VoidedByUserId,
            expense.Version,
            expense.CreatedAt,
            expense.UpdatedAt);
    }
}
