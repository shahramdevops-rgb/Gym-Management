namespace Gym.Application.Expenses.VoidExpense;

/// <param name="Reason">
/// Why the expense is being taken back. Required: a voided expense is kept, not erased, and the
/// reason is the whole record of what happened (BUSINESS_RULES.md §9).
/// </param>
public sealed record VoidExpenseCommand(string Reason);
