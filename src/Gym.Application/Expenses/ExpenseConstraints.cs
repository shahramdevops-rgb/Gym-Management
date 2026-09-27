namespace Gym.Application.Expenses;

/// <summary>
/// Database constraint names the expense handlers react to, shared with the configurations that
/// create them (see <c>MemberConstraints</c> for why).
/// </summary>
public static class ExpenseConstraints
{
    public const string UniqueCategoryName = "ix_expense_categories_normalized_name";

    /// <summary>The foreign key from an expense to its category.</summary>
    public const string CategoryForeignKey = "fk_expenses_expense_categories_category_id";
}
