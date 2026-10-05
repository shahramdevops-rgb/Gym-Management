using FluentValidation;

using Gym.Domain.Payables;

namespace Gym.Application.Payables;

/// <summary>
/// The field checks the payable commands share, with the entity's error codes, so the form gets a
/// message per field before anything touches the database. The limits come from
/// <see cref="Payable"/>, so the validator and the entity cannot disagree.
/// </summary>
public static class PayableRules
{
    /// <summary>One check per error, so each failure carries its own code for the form.</summary>
    public static IRuleBuilderOptions<T, decimal> ValidAmount<T>(this IRuleBuilderInitial<T, decimal> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(amount => Payable.CheckAmount(amount) != PayableErrors.AmountNotPositive)
            .WithErrorCode(PayableErrors.AmountNotPositive.Code).WithMessage(PayableErrors.AmountNotPositive.Description)
            .Must(amount => Payable.CheckAmount(amount) != PayableErrors.AmountTooLarge)
            .WithErrorCode(PayableErrors.AmountTooLarge.Code).WithMessage(PayableErrors.AmountTooLarge.Description)
            .Must(amount => Payable.CheckAmount(amount) != PayableErrors.AmountTooManyDecimals)
            .WithErrorCode(PayableErrors.AmountTooManyDecimals.Code)
            .WithMessage(PayableErrors.AmountTooManyDecimals.Description);

    public static IRuleBuilderOptions<T, string> ValidPayee<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(payee => !string.IsNullOrWhiteSpace(payee))
            .WithErrorCode(PayableErrors.PayeeRequired.Code)
            .WithMessage(PayableErrors.PayeeRequired.Description)
            .Must(payee => payee.Trim().Length <= Payable.PayeeMaxLength)
            .WithErrorCode(PayableErrors.PayeeTooLong.Code)
            .WithMessage(PayableErrors.PayeeTooLong.Description);

    public static IRuleBuilderOptions<T, string> ValidDescription<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(description => !string.IsNullOrWhiteSpace(description))
            .WithErrorCode(PayableErrors.DescriptionRequired.Code)
            .WithMessage(PayableErrors.DescriptionRequired.Description)
            .Must(description => description.Trim().Length <= Payable.DescriptionMaxLength)
            .WithErrorCode(PayableErrors.DescriptionTooLong.Code)
            .WithMessage(PayableErrors.DescriptionTooLong.Description);

    public static IRuleBuilderOptions<T, Guid> ValidCategory<T>(this IRuleBuilderInitial<T, Guid> rule) =>
        rule.NotEmpty()
            .WithErrorCode(PayableErrors.CategoryRequired.Code)
            .WithMessage(PayableErrors.CategoryRequired.Description);

    public static IRuleBuilderOptions<T, PayableKind> ValidKind<T>(this IRuleBuilderInitial<T, PayableKind> rule) =>
        rule.IsInEnum();

    /// <summary>A reason to cancel, or to send a payment back to pending: the same limits.</summary>
    public static IRuleBuilderOptions<T, string> ValidReason<T>(
        this IRuleBuilderInitial<T, string> rule, Domain.Common.Error required, Domain.Common.Error tooLong)
    {
        ArgumentNullException.ThrowIfNull(required);
        ArgumentNullException.ThrowIfNull(tooLong);

        return rule.Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithErrorCode(required.Code)
            .WithMessage(required.Description)
            .Must(reason => reason.Trim().Length <= Payable.ReasonMaxLength)
            .WithErrorCode(tooLong.Code)
            .WithMessage(tooLong.Description);
    }
}
