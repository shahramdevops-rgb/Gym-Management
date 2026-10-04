using Gym.Domain.Common;

namespace Gym.Domain.ServiceCharges;

public static class ServiceChargeErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "ServiceCharges.NotFound",
        "Service charge not found.");

    public static readonly Error AmountNotPositive = Error.Validation(
        "ServiceCharges.AmountNotPositive",
        "Service charge amount must be greater than zero.");

    public static readonly Error KindInvalid = Error.Validation(
        "ServiceCharges.KindInvalid",
        "Service kind is not valid.");

    public static readonly Error AmountTooLarge = Error.Validation(
        "ServiceCharges.AmountTooLarge",
        "Service charge amount is too large.");

    public static readonly Error AmountTooManyDecimals = Error.Validation(
        "ServiceCharges.AmountTooManyDecimals",
        $"Service charge amount can have at most {ServiceCharge.AmountDecimals} decimal places.");

    /// <summary>
    /// BUSINESS_RULES.md §7 <i>Gym services</i>: a charge is recorded against an open visit. A
    /// checked-out, auto-closed or cancelled visit is history, and history is not added to.
    /// </summary>
    public static readonly Error VisitNotOpen = Error.BusinessRule(
        "ServiceCharges.VisitNotOpen",
        "The visit is closed, so a service charge cannot be recorded or changed.");

    /// <summary>
    /// BUSINESS_RULES.md §7 <i>Gym services</i>: one non-voided charge per visit per kind. The
    /// partial unique index says the same thing, so a race lands here too.
    /// </summary>
    public static readonly Error AlreadyCharged = Error.Conflict(
        "ServiceCharges.AlreadyCharged",
        "This visit already has a service charge of this kind.");

    public static readonly Error AlreadyVoided = Error.BusinessRule(
        "ServiceCharges.AlreadyVoided",
        "The service charge is already voided.");

    /// <summary>
    /// BUSINESS_RULES.md §7 <i>Gym services</i>: once money has been taken against a charge it is
    /// a financial record, corrected with a void plus a reason rather than edited (§5).
    /// </summary>
    public static readonly Error AlreadyPaid = Error.BusinessRule(
        "ServiceCharges.AlreadyPaid",
        "The service charge has been paid, so its amount cannot be changed. Void it instead.");

    public static readonly Error VoidReasonRequired = Error.Validation(
        "ServiceCharges.VoidReasonRequired",
        "A reason is required to void a service charge.");

    public static readonly Error VoidReasonTooLong = Error.Validation(
        "ServiceCharges.VoidReasonTooLong",
        "The void reason is too long.");

    public static readonly Error DescriptionRequired = Error.Validation(
        "ServiceCharges.DescriptionRequired",
        "The name of what was sold is required.");

    public static readonly Error DescriptionTooLong = Error.Validation(
        "ServiceCharges.DescriptionTooLong",
        $"The name of what was sold can be at most {ServiceCharge.DescriptionMaxLength} characters.");

    public static readonly Error QuantityInvalid = Error.Validation(
        "ServiceCharges.QuantityInvalid",
        $"Quantity must be between 1 and {ServiceCharge.MaxQuantity}.");

    public static readonly Error ShopItemsRequired = Error.Validation(
        "ServiceCharges.ShopItemsRequired",
        "A shop sale needs at least one item.");

    public static readonly Error TooManyShopItems = Error.Validation(
        "ServiceCharges.TooManyShopItems",
        $"A shop sale can have at most {ServiceCharge.MaxShopItemsPerSale} items.");

    /// <summary>
    /// BUSINESS_RULES.md §7 <i>Sale at the desk</i>: never edited, voided with a reason and
    /// entered again, as a cafe order is (§8).
    /// </summary>
    public static readonly Error SaleNotEditable = Error.BusinessRule(
        "ServiceCharges.SaleNotEditable",
        "A sale cannot be changed. Void it and enter it again.");

    /// <summary>The <c>xmin</c> backstop: two people changed the same charge at the same moment.</summary>
    public static readonly Error ChangedConcurrently = Error.Conflict(
        "ServiceCharges.ChangedConcurrently",
        "The service charge changed at the same moment. Try again.");

    public static readonly Error InvalidDateRange = Error.Validation(
        "ServiceCharges.InvalidDateRange",
        "'from' must not be after 'to'.");
}
