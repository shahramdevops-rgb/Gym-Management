using Gym.Domain.Common;

namespace Gym.Domain.Expenses;

public static class ExpenseErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Expenses.NotFound",
        "No expense has that id.");

    public static readonly Error AmountNotPositive = Error.Validation(
        "Expenses.AmountNotPositive",
        "Expense amount must be greater than zero.");

    public static readonly Error AmountTooLarge = Error.Validation(
        "Expenses.AmountTooLarge",
        "Expense amount is too large.");

    public static readonly Error AmountTooManyDecimals = Error.Validation(
        "Expenses.AmountTooManyDecimals",
        $"Expense amount can have at most {Expense.AmountDecimals} decimal places.");

    public static readonly Error CategoryRequired = Error.Validation(
        "Expenses.CategoryRequired",
        "An expense category is required.");

    /// <summary>The category id is well formed but names no category.</summary>
    public static readonly Error CategoryNotFound = Error.NotFound(
        "Expenses.CategoryNotFound",
        "No expense category has that id.");

    /// <summary>
    /// BUSINESS_RULES.md §9: money cannot have gone out on a day that has not happened yet. Any
    /// past day is fine, so an old bill can still be entered.
    /// </summary>
    public static readonly Error DateInFuture = Error.Validation(
        "Expenses.DateInFuture",
        "The expense date cannot be after today.");

    public static readonly Error DescriptionRequired = Error.Validation(
        "Expenses.DescriptionRequired",
        "A description is required.");

    public static readonly Error DescriptionTooLong = Error.Validation(
        "Expenses.DescriptionTooLong",
        "The description is too long.");

    public static readonly Error ReferenceNumberTooLong = Error.Validation(
        "Expenses.ReferenceNumberTooLong",
        "The reference number is too long.");

    /// <summary>
    /// BUSINESS_RULES.md §9: a voided expense is final. It is not edited and not voided twice; a
    /// correction is a fresh expense.
    /// </summary>
    public static readonly Error AlreadyVoided = Error.BusinessRule(
        "Expenses.AlreadyVoided",
        "The expense is voided and can no longer be changed.");

    public static readonly Error VoidReasonRequired = Error.Validation(
        "Expenses.VoidReasonRequired",
        "A reason is required to void an expense.");

    public static readonly Error VoidReasonTooLong = Error.Validation(
        "Expenses.VoidReasonTooLong",
        "The void reason is too long.");

    public static readonly Error InvalidDateRange = Error.Validation(
        "Expenses.InvalidDateRange",
        "The start of the date range is after its end.");

    /// <summary>Two people changed the same expense at the same moment; the second save is refused.</summary>
    public static readonly Error ChangedConcurrently = Error.Conflict(
        "Expenses.ChangedConcurrently",
        "The expense was changed by someone else at the same moment. Reload and try again.");
}
