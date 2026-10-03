using FluentValidation;

using Gym.Application.Payments;
using Gym.Domain.ServiceCharges;

namespace Gym.Application.ServiceCharges.RecordMiscellaneousSale;

public sealed class RecordMiscellaneousSaleValidator : AbstractValidator<RecordMiscellaneousSaleCommand>
{
    public RecordMiscellaneousSaleValidator()
    {
        RuleFor(command => command.Description)
            .Cascade(CascadeMode.Stop)
            .Must(description => !string.IsNullOrWhiteSpace(description))
            .WithErrorCode(ServiceChargeErrors.DescriptionRequired.Code).WithMessage(ServiceChargeErrors.DescriptionRequired.Description)
            .Must(description => description.Trim().Length <= ServiceCharge.DescriptionMaxLength)
            .WithErrorCode(ServiceChargeErrors.DescriptionTooLong.Code).WithMessage(ServiceChargeErrors.DescriptionTooLong.Description);

        RuleFor(command => command.Quantity)
            .InclusiveBetween(1, ServiceCharge.MaxQuantity)
            .WithErrorCode(ServiceChargeErrors.QuantityInvalid.Code).WithMessage(ServiceChargeErrors.QuantityInvalid.Description);

        RuleFor(command => command.UnitPrice).ValidAmount();

        RuleFor(command => command.Method!.Value)
            .ValidMethod()
            .OverridePropertyName(nameof(RecordMiscellaneousSaleCommand.Method))
            .When(command => command.Method is not null);
    }
}
