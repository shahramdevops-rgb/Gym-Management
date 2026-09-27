namespace Gym.Application.Expenses.UpdateExpense;

/// <param name="Version">The <c>version</c> from the expense as it was read before editing.</param>
public sealed record UpdateExpenseCommand(
    decimal Amount,
    Guid CategoryId,
    DateOnly ExpenseDate,
    string Description,
    string? ReferenceNumber,
    uint Version);
