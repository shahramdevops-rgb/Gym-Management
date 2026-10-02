using FluentValidation;

using Gym.Application.Payments;

namespace Gym.Application.Attendances.SettleGuestCafe;

/// <summary>The same money checks as every other payment form (<see cref="PaymentRules"/>).</summary>
public sealed class SettleGuestCafeValidator : AbstractValidator<SettleGuestCafeCommand>
{
    public SettleGuestCafeValidator()
    {
        RuleFor(command => command.Amount).ValidAmount();
        RuleFor(command => command.Method).ValidMethod();
        RuleFor(command => command.ReferenceNumber).ValidReferenceNumber();
    }
}
