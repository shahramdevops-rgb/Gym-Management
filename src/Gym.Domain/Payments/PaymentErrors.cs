using Gym.Domain.Common;

namespace Gym.Domain.Payments;

public static class PaymentErrors
{
    public static readonly Error AmountNotPositive = Error.Validation(
        "Payments.AmountNotPositive",
        "Payment amount must be greater than zero.");

    public static readonly Error AmountTooLarge = Error.Validation(
        "Payments.AmountTooLarge",
        "Payment amount is too large.");

    public static readonly Error AmountTooManyDecimals = Error.Validation(
        "Payments.AmountTooManyDecimals",
        $"Payment amount can have at most {Payment.AmountDecimals} decimal places.");

    public static readonly Error ReferenceNumberTooLong = Error.Validation(
        "Payments.ReferenceNumberTooLong",
        "Reference number is too long.");

    public static readonly Error MethodInvalid = Error.Validation(
        "Payments.MethodInvalid",
        "Payment method is not valid.");

    /// <summary>BUSINESS_RULES.md §5: a subscription, and likewise a service charge, cannot be overpaid.</summary>
    public static readonly Error Overpayment = Error.BusinessRule(
        "Payments.Overpayment",
        "This payment would exceed what is owed on the item.");

    public static readonly Error RefundReasonRequired = Error.Validation(
        "Payments.RefundReasonRequired",
        "A reason is required to refund a payment.");

    public static readonly Error RefundReasonTooLong = Error.Validation(
        "Payments.RefundReasonTooLong",
        "The refund reason is too long.");

    /// <summary>BUSINESS_RULES.md §5: a refund cannot exceed the current net paid amount.</summary>
    public static readonly Error RefundExceedsNetPaid = Error.BusinessRule(
        "Payments.RefundExceedsNetPaid",
        "This refund would exceed the item's net paid amount.");

    /// <summary>
    /// BUSINESS_RULES.md §5: a subscription can only be refunded while nobody has used it, whatever
    /// its status. Sessions already taken are not bought back.
    /// </summary>
    public static readonly Error RefundAfterUse = Error.BusinessRule(
        "Payments.RefundAfterUse",
        "A subscription with a used session cannot be refunded.");
}
