using Gym.Domain.Common;

namespace Gym.Domain.Expenses;

public static class ExpenseCategoryErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "ExpenseCategories.NotFound",
        "No expense category has that id.");

    /// <summary>
    /// Names are unique among all categories, compared in normalized form: two headings that look
    /// alike would split one kind of spending across two lines of the report.
    /// </summary>
    public static readonly Error NameAlreadyExists = Error.Conflict(
        "ExpenseCategories.NameAlreadyExists",
        "Another expense category already uses that name.");

    public static readonly Error NameRequired = Error.Validation(
        "ExpenseCategories.NameRequired",
        "Category name is required.");

    public static readonly Error NameTooLong = Error.Validation(
        "ExpenseCategories.NameTooLong",
        "Category name is too long.");

    /// <summary>Two people renamed the same category at the same moment; the second save is refused.</summary>
    public static readonly Error ChangedConcurrently = Error.Conflict(
        "ExpenseCategories.ChangedConcurrently",
        "The category was changed by someone else at the same moment. Reload and try again.");
}
