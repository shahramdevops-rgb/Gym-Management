using Gym.Domain.Common;

namespace Gym.Domain.Payables;

public static class PayableErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Payables.NotFound",
        "No cheque or instalment has that id.");

    public static readonly Error AmountNotPositive = Error.Validation(
        "Payables.AmountNotPositive",
        "The amount must be greater than zero.");

    public static readonly Error AmountTooLarge = Error.Validation(
        "Payables.AmountTooLarge",
        "The amount is too large.");

    public static readonly Error AmountTooManyDecimals = Error.Validation(
        "Payables.AmountTooManyDecimals",
        $"The amount can have at most {Payable.AmountDecimals} decimal places.");

    public static readonly Error PayeeRequired = Error.Validation(
        "Payables.PayeeRequired",
        "The payee is required.");

    public static readonly Error PayeeTooLong = Error.Validation(
        "Payables.PayeeTooLong",
        "The payee is too long.");

    public static readonly Error DescriptionRequired = Error.Validation(
        "Payables.DescriptionRequired",
        "A description is required.");

    public static readonly Error DescriptionTooLong = Error.Validation(
        "Payables.DescriptionTooLong",
        "The description is too long.");

    public static readonly Error CategoryRequired = Error.Validation(
        "Payables.CategoryRequired",
        "An expense category is required.");

    /// <summary>The category id is well formed but names no category.</summary>
    public static readonly Error CategoryNotFound = Error.NotFound(
        "Payables.CategoryNotFound",
        "No expense category has that id.");

    /// <summary>An instalment says which one it is: «قسط n از N», both numbers required.</summary>
    public static readonly Error InstallmentNumbersRequired = Error.Validation(
        "Payables.InstallmentNumbersRequired",
        "An instalment needs its number and the number of instalments.");

    /// <summary>A cheque has no instalment numbers.</summary>
    public static readonly Error InstallmentNumbersOnlyForInstallments = Error.Validation(
        "Payables.InstallmentNumbersOnlyForInstallments",
        "Only an instalment has instalment numbers.");

    public static readonly Error InstallmentCountOutOfRange = Error.Validation(
        "Payables.InstallmentCountOutOfRange",
        $"The number of instalments must be between 1 and {Payable.MaxInstallmentCount}.");

    public static readonly Error InstallmentNumberOutOfRange = Error.Validation(
        "Payables.InstallmentNumberOutOfRange",
        "The instalment number must be between 1 and the number of instalments.");

    /// <summary>
    /// BUSINESS_RULES.md §9 <i>Cheques and instalments</i>: a bank does not pay a cheque before its
    /// date, so it cannot be marked paid before then. An instalment can.
    /// </summary>
    public static readonly Error ChequeNotDueYet = Error.BusinessRule(
        "Payables.ChequeNotDueYet",
        "The cheque cannot be marked as paid before its date.");

    /// <summary>A paid one is not edited, cancelled or paid twice; it can only go back to pending.</summary>
    public static readonly Error AlreadyPaid = Error.BusinessRule(
        "Payables.AlreadyPaid",
        "It is already paid. Send it back to pending first.");

    /// <summary>A cancelled one is final: not edited, not paid, not cancelled twice.</summary>
    public static readonly Error AlreadyCancelled = Error.BusinessRule(
        "Payables.AlreadyCancelled",
        "It is cancelled and can no longer be changed.");

    /// <summary>Only a paid one goes back to pending.</summary>
    public static readonly Error NotPaid = Error.BusinessRule(
        "Payables.NotPaid",
        "Only a paid cheque or instalment can be sent back to pending.");

    public static readonly Error CancelReasonRequired = Error.Validation(
        "Payables.CancelReasonRequired",
        "A reason is required to cancel.");

    public static readonly Error CancelReasonTooLong = Error.Validation(
        "Payables.CancelReasonTooLong",
        "The cancel reason is too long.");

    public static readonly Error RevertReasonRequired = Error.Validation(
        "Payables.RevertReasonRequired",
        "A reason is required to send it back to pending.");

    public static readonly Error RevertReasonTooLong = Error.Validation(
        "Payables.RevertReasonTooLong",
        "The reason is too long.");

    /// <summary>Two people changed the same record at the same moment; the second save is refused.</summary>
    public static readonly Error ChangedConcurrently = Error.Conflict(
        "Payables.ChangedConcurrently",
        "It was changed by someone else at the same moment. Reload and try again.");
}
