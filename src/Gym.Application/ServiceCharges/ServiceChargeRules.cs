using FluentValidation;

using Gym.Domain.ServiceCharges;

namespace Gym.Application.ServiceCharges;

/// <summary>
/// The field checks the service charge commands share, with the entity's error codes, so the
/// form gets a message per field before anything touches the database — the same split
/// <see cref="Payments.PaymentRules"/> uses.
/// </summary>
public static class ServiceChargeRules
{
    /// <summary>One check per error, so each failure carries its own code for the form.</summary>
    public static IRuleBuilderOptions<T, decimal> ValidAmount<T>(this IRuleBuilderInitial<T, decimal> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(amount => ServiceCharge.CheckAmount(amount) != ServiceChargeErrors.AmountNotPositive)
            .WithErrorCode(ServiceChargeErrors.AmountNotPositive.Code).WithMessage(ServiceChargeErrors.AmountNotPositive.Description)
            .Must(amount => ServiceCharge.CheckAmount(amount) != ServiceChargeErrors.AmountTooLarge)
            .WithErrorCode(ServiceChargeErrors.AmountTooLarge.Code).WithMessage(ServiceChargeErrors.AmountTooLarge.Description)
            .Must(amount => ServiceCharge.CheckAmount(amount) != ServiceChargeErrors.AmountTooManyDecimals)
            .WithErrorCode(ServiceChargeErrors.AmountTooManyDecimals.Code).WithMessage(ServiceChargeErrors.AmountTooManyDecimals.Description);

    public static IRuleBuilderOptions<T, ServiceChargeKind> ValidKind<T>(this IRuleBuilderInitial<T, ServiceChargeKind> rule) =>
        rule.IsInEnum()
            .WithErrorCode(ServiceChargeErrors.KindInvalid.Code).WithMessage(ServiceChargeErrors.KindInvalid.Description);

    public static IRuleBuilderOptions<T, string> ValidVoidReason<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode(ServiceChargeErrors.VoidReasonRequired.Code).WithMessage(ServiceChargeErrors.VoidReasonRequired.Description)
            .MaximumLength(ServiceCharge.VoidReasonMaxLength)
            .WithErrorCode(ServiceChargeErrors.VoidReasonTooLong.Code).WithMessage(ServiceChargeErrors.VoidReasonTooLong.Description);
}
