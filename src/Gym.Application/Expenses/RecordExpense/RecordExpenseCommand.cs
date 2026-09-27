namespace Gym.Application.Expenses.RecordExpense;

/// <param name="ExpenseDate">The day the money went out, in the gym's time zone. Not after today.</param>
/// <param name="ReferenceNumber">An invoice or transfer number; optional.</param>
public sealed record RecordExpenseCommand(
    decimal Amount,
    Guid CategoryId,
    DateOnly ExpenseDate,
    string Description,
    string? ReferenceNumber);
