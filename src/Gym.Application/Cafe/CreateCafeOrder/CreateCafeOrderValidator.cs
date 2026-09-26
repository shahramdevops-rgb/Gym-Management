using FluentValidation;

using Gym.Application.Payments;
using Gym.Domain.Cafe;

namespace Gym.Application.Cafe.CreateCafeOrder;

public sealed class CreateCafeOrderValidator : AbstractValidator<CreateCafeOrderCommand>
{
    public CreateCafeOrderValidator()
    {
        RuleFor(command => command.Items)
            .Must(items => items is { Count: > 0 })
            .WithErrorCode(CafeOrderErrors.NoItems.Code).WithMessage(CafeOrderErrors.NoItems.Description)
            .Must(items => items is null || items.Count <= CafeOrder.MaxItems)
            .WithErrorCode(CafeOrderErrors.TooManyItems.Code).WithMessage(CafeOrderErrors.TooManyItems.Description);

        RuleForEach(command => command.Items).ChildRules(line =>
        {
            line.RuleFor(item => item.Quantity)
                .InclusiveBetween(1, CafeOrderItem.MaxQuantity)
                .WithErrorCode(CafeOrderErrors.QuantityInvalid.Code)
                .WithMessage(CafeOrderErrors.QuantityInvalid.Description);
        });

        // The payment block is optional; when it is there it follows every ordinary payment rule.
        When(command => command.Payment is not null, () =>
        {
            RuleFor(command => command.Payment!.Amount).ValidAmount();
            RuleFor(command => command.Payment!.Method).ValidMethod();
            RuleFor(command => command.Payment!.ReferenceNumber).ValidReferenceNumber();
        });
    }
}
