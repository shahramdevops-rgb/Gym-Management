namespace Gym.Application.Expenses.UpdateExpenseCategory;

/// <param name="Version">The <c>version</c> from the category as it was read before editing.</param>
public sealed record UpdateExpenseCategoryCommand(string Name, uint Version);
