using Gym.Domain.Common;

namespace Gym.Domain.Cheques;

public static class ChequeErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Cheques.NotFound",
        "No cheque has that id.");

    public static readonly Error AmountNotPositive = Error.Validation(
        "Cheques.AmountNotPositive",
        "Cheque amount must be greater than zero.");

    public static readonly Error AmountTooLarge = Error.Validation(
        "Cheques.AmountTooLarge",
        "Cheque amount is too large.");

    public static readonly Error AmountTooManyDecimals = Error.Validation(
        "Cheques.AmountTooManyDecimals",
        $"Cheque amount can have at most {Cheque.AmountDecimals} decimal places.");

    public static readonly Error PayeeRequired = Error.Validation(
        "Cheques.PayeeRequired",
        "The payee is required.");

    public static readonly Error PayeeTooLong = Error.Validation(
        "Cheques.PayeeTooLong",
        "The payee is too long.");

    public static readonly Error DescriptionRequired = Error.Validation(
        "Cheques.DescriptionRequired",
        "A description is required.");

    public static readonly Error DescriptionTooLong = Error.Validation(
        "Cheques.DescriptionTooLong",
        "The description is too long.");

    /// <summary>
    /// BUSINESS_RULES.md §9 <i>Cheques</i>: a bank does not pay a cheque before its date, so it
    /// cannot be marked passed before then.
    /// </summary>
    public static readonly Error NotDueYet = Error.BusinessRule(
        "Cheques.NotDueYet",
        "The cheque cannot be marked as passed before its date.");

    /// <summary>A passed cheque is final: not edited, not cancelled, not passed twice.</summary>
    public static readonly Error AlreadyPassed = Error.BusinessRule(
        "Cheques.AlreadyPassed",
        "The cheque is already passed and can no longer be changed.");

    /// <summary>A cancelled cheque is final: not edited, not passed, not cancelled twice.</summary>
    public static readonly Error AlreadyCancelled = Error.BusinessRule(
        "Cheques.AlreadyCancelled",
        "The cheque is cancelled and can no longer be changed.");

    public static readonly Error CancelReasonRequired = Error.Validation(
        "Cheques.CancelReasonRequired",
        "A reason is required to cancel a cheque.");

    public static readonly Error CancelReasonTooLong = Error.Validation(
        "Cheques.CancelReasonTooLong",
        "The cancel reason is too long.");

    /// <summary>Two people changed the same cheque at the same moment; the second save is refused.</summary>
    public static readonly Error ChangedConcurrently = Error.Conflict(
        "Cheques.ChangedConcurrently",
        "The cheque was changed by someone else at the same moment. Reload and try again.");
}
