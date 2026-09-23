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

    /// <summary>The <c>xmin</c> backstop: two people changed the same charge at the same moment.</summary>
    public static readonly Error ChangedConcurrently = Error.Conflict(
        "ServiceCharges.ChangedConcurrently",
        "The service charge changed at the same moment. Try again.");
}
