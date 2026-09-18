using FluentValidation;

using Gym.Domain.Plans;

namespace Gym.Application.Plans;

/// <summary>
/// The field checks create and update share, with the entity's error codes, so the form gets a
/// message per field before anything touches the database. The limits come from
/// <see cref="Plan"/>, so the validator and the entity cannot disagree.
/// </summary>
public static class PlanRules
{
    public static IRuleBuilderOptions<T, string> ValidName<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithErrorCode(PlanErrors.NameRequired.Code).WithMessage(PlanErrors.NameRequired.Description)
            .Must(name => name.Trim().Length <= Plan.NameMaxLength)
            .WithErrorCode(PlanErrors.NameTooLong.Code).WithMessage(PlanErrors.NameTooLong.Description);

    public static IRuleBuilderOptions<T, int> ValidDurationDays<T>(this IRuleBuilderInitial<T, int> rule) =>
        rule.InclusiveBetween(1, Plan.MaxDurationDays)
            .WithErrorCode(PlanErrors.DurationInvalid.Code).WithMessage(PlanErrors.DurationInvalid.Description);

    public static IRuleBuilderOptions<T, int?> ValidSessionCount<T>(this IRuleBuilderInitial<T, int?> rule) =>
        rule.Must(count => count is null or (>= 1 and <= Plan.MaxSessionCount))
            .WithErrorCode(PlanErrors.SessionCountInvalid.Code).WithMessage(PlanErrors.SessionCountInvalid.Description);

    /// <summary>One check per error, so each failure carries its own code for the form.</summary>
    public static IRuleBuilderOptions<T, decimal> ValidPrice<T>(this IRuleBuilderInitial<T, decimal> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .Must(price => Plan.CheckPrice(price) != PlanErrors.PriceNegative)
            .WithErrorCode(PlanErrors.PriceNegative.Code).WithMessage(PlanErrors.PriceNegative.Description)
            .Must(price => Plan.CheckPrice(price) != PlanErrors.PriceTooLarge)
            .WithErrorCode(PlanErrors.PriceTooLarge.Code).WithMessage(PlanErrors.PriceTooLarge.Description)
            .Must(price => Plan.CheckPrice(price) != PlanErrors.PriceTooManyDecimals)
            .WithErrorCode(PlanErrors.PriceTooManyDecimals.Code).WithMessage(PlanErrors.PriceTooManyDecimals.Description);
}
