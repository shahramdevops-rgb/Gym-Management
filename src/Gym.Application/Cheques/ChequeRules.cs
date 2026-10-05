using FluentValidation;

using Gym.Domain.Cheques;

namespace Gym.Application.Cheques;

/// <summary>
/// The field checks the cheque commands share, with the entity's error codes, so the form gets a
/// message per field before anything touches the database. The limits come from
/// <see cref="Cheque"/>, so the validator and the entity cannot disagree.
/// </summary>
public static class ChequeRules
{
    /// <summary>One check per error, so each failure carries its own code for the form.</summary>
    public static IRuleBuilderOptions<T, decimal> ValidAmount<T>(this IRuleBuilderInitial<T, decimal> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(amount => Cheque.CheckAmount(amount) != ChequeErrors.AmountNotPositive)
            .WithErrorCode(ChequeErrors.AmountNotPositive.Code).WithMessage(ChequeErrors.AmountNotPositive.Description)
            .Must(amount => Cheque.CheckAmount(amount) != ChequeErrors.AmountTooLarge)
            .WithErrorCode(ChequeErrors.AmountTooLarge.Code).WithMessage(ChequeErrors.AmountTooLarge.Description)
            .Must(amount => Cheque.CheckAmount(amount) != ChequeErrors.AmountTooManyDecimals)
            .WithErrorCode(ChequeErrors.AmountTooManyDecimals.Code)
            .WithMessage(ChequeErrors.AmountTooManyDecimals.Description);

    public static IRuleBuilderOptions<T, string> ValidPayee<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(payee => !string.IsNullOrWhiteSpace(payee))
            .WithErrorCode(ChequeErrors.PayeeRequired.Code)
            .WithMessage(ChequeErrors.PayeeRequired.Description)
            .Must(payee => payee.Trim().Length <= Cheque.PayeeMaxLength)
            .WithErrorCode(ChequeErrors.PayeeTooLong.Code)
            .WithMessage(ChequeErrors.PayeeTooLong.Description);

    public static IRuleBuilderOptions<T, string> ValidDescription<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(description => !string.IsNullOrWhiteSpace(description))
            .WithErrorCode(ChequeErrors.DescriptionRequired.Code)
            .WithMessage(ChequeErrors.DescriptionRequired.Description)
            .Must(description => description.Trim().Length <= Cheque.DescriptionMaxLength)
            .WithErrorCode(ChequeErrors.DescriptionTooLong.Code)
            .WithMessage(ChequeErrors.DescriptionTooLong.Description);

    public static IRuleBuilderOptions<T, string> ValidCancelReason<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithErrorCode(ChequeErrors.CancelReasonRequired.Code)
            .WithMessage(ChequeErrors.CancelReasonRequired.Description)
            .Must(reason => reason.Trim().Length <= Cheque.CancelReasonMaxLength)
            .WithErrorCode(ChequeErrors.CancelReasonTooLong.Code)
            .WithMessage(ChequeErrors.CancelReasonTooLong.Description);
}
