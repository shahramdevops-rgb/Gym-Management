using Gym.Domain.Common;

namespace Gym.Domain.Payments;

/// <summary>BUSINESS_RULES.md §5 <i>Settling several items at once</i>.</summary>
public static class SettlementErrors
{
    public static readonly Error NoItems = Error.Validation(
        "Settlements.NoItems",
        "Choose at least one item to settle.");

    public static readonly Error DuplicateItem = Error.Validation(
        "Settlements.DuplicateItem",
        "The same item appears more than once in the settlement.");

    /// <summary>
    /// An item the desk ticked is no longer owed, or no longer owes the figure the desk was shown:
    /// it was paid elsewhere, voided, cancelled, or is not this member's. Nothing was written.
    /// </summary>
    public static readonly Error DebtChanged = Error.Conflict(
        "Settlements.DebtChanged",
        "The member's debt changed since it was shown. Review it and try again.");
}
