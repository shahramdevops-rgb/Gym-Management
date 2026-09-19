using FluentValidation;

using Gym.Domain.Payments;

namespace Gym.Application.Payments;

/// <summary>
/// The field checks the register-payment (and, later, refund) commands share, with the entity's
/// error codes, so the form gets a message per field before anything touches the database.
/// </summary>
public static class PaymentRules
{
    /// <summary>One check per error, so each failure carries its own code for the form.</summary>
    public static IRuleBuilderOptions<T, decimal> ValidAmount<T>(this IRuleBuilderInitial<T, decimal> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(amount => Payment.CheckAmount(amount) != PaymentErrors.AmountNotPositive)
            .WithErrorCode(PaymentErrors.AmountNotPositive.Code).WithMessage(PaymentErrors.AmountNotPositive.Description)
            .Must(amount => Payment.CheckAmount(amount) != PaymentErrors.AmountTooLarge)
            .WithErrorCode(PaymentErrors.AmountTooLarge.Code).WithMessage(PaymentErrors.AmountTooLarge.Description)
            .Must(amount => Payment.CheckAmount(amount) != PaymentErrors.AmountTooManyDecimals)
            .WithErrorCode(PaymentErrors.AmountTooManyDecimals.Code).WithMessage(PaymentErrors.AmountTooManyDecimals.Description);

    public static IRuleBuilderOptions<T, PaymentMethod> ValidMethod<T>(this IRuleBuilderInitial<T, PaymentMethod> rule) =>
        rule.IsInEnum()
            .WithErrorCode(PaymentErrors.MethodInvalid.Code).WithMessage(PaymentErrors.MethodInvalid.Description);

    public static IRuleBuilderOptions<T, string?> ValidReferenceNumber<T>(this IRuleBuilderInitial<T, string?> rule) =>
        rule.MaximumLength(Payment.ReferenceNumberMaxLength)
            .WithErrorCode(PaymentErrors.ReferenceNumberTooLong.Code).WithMessage(PaymentErrors.ReferenceNumberTooLong.Description);
}
