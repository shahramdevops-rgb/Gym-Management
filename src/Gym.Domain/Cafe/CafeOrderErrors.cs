using Gym.Domain.Common;

namespace Gym.Domain.Cafe;

public static class CafeOrderErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "CafeOrders.NotFound",
        "No cafe order has that id.");

    public static readonly Error NoItems = Error.Validation(
        "CafeOrders.NoItems",
        "An order must have at least one item.");

    public static readonly Error TooManyItems = Error.Validation(
        "CafeOrders.TooManyItems",
        $"An order can have at most {CafeOrder.MaxItems} lines.");

    public static readonly Error QuantityInvalid = Error.Validation(
        "CafeOrders.QuantityInvalid",
        $"Quantity must be between 1 and {CafeOrderItem.MaxQuantity}.");

    public static readonly Error DuplicateProduct = Error.Validation(
        "CafeOrders.DuplicateProduct",
        "The same product appears on the order twice; combine it into one line.");

    public static readonly Error ProductNotFound = Error.NotFound(
        "CafeOrders.ProductNotFound",
        "One of the products on the order no longer exists.");

    /// <summary>
    /// BUSINESS_RULES.md §8: a walk-in order names no member, so there is no account to leave a
    /// balance on and it is paid in full at creation.
    /// </summary>
    public static readonly Error WalkInMustBePaidInFull = Error.BusinessRule(
        "CafeOrders.WalkInMustBePaidInFull",
        "An order with no member must be paid in full when it is created.");

    /// <summary>The payment taken at creation is more than the order is worth.</summary>
    public static readonly Error PaidMoreThanTheOrder = Error.BusinessRule(
        "CafeOrders.PaidMoreThanTheOrder",
        "The amount paid is more than the order total.");

    public static readonly Error AlreadyCancelled = Error.BusinessRule(
        "CafeOrders.AlreadyCancelled",
        "The order is already cancelled.");

    public static readonly Error CancelReasonRequired = Error.Validation(
        "CafeOrders.CancelReasonRequired",
        "A reason is required to cancel an order.");

    public static readonly Error CancelReasonTooLong = Error.Validation(
        "CafeOrders.CancelReasonTooLong",
        $"The cancellation reason can be at most {CafeOrder.CancelReasonMaxLength} characters.");

    /// <summary>The order history was asked for a range that ends before it starts.</summary>
    public static readonly Error InvalidDateRange = Error.Validation(
        "CafeOrders.InvalidDateRange",
        "The start date must be on or before the end date.");

    /// <summary>Two people changed the same order at the same moment; the second save is refused.</summary>
    public static readonly Error ChangedConcurrently = Error.Conflict(
        "CafeOrders.ChangedConcurrently",
        "The order was changed by someone else at the same moment. Reload and try again.");
}
