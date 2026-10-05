namespace Gym.Application.Payables;

/// <summary>
/// Database constraint names the payable handlers react to, shared with the configurations that
/// create them (see <c>MemberConstraints</c> for why).
/// </summary>
public static class PayableConstraints
{
    /// <summary>
    /// The partial unique index on <c>expenses(payable_id) WHERE voided_at IS NULL</c>: at most one
    /// standing expense per cheque or instalment, so two «پرداخت شد» racing each other cannot both
    /// record one.
    /// </summary>
    public const string OneStandingExpense = "ix_expenses_payable_id_standing";

    /// <summary>The foreign key from a payable to its expense category.</summary>
    public const string CategoryForeignKey = "fk_payables_expense_categories_category_id";
}
