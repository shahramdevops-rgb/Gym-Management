using FluentValidation;

namespace Gym.Application.Payments.RegisterPayment;

public sealed class RegisterPaymentValidator : AbstractValidator<RegisterPaymentCommand>
{
    public RegisterPaymentValidator()
    {
        RuleFor(command => command.Amount).ValidAmount();
        RuleFor(command => command.Method).ValidMethod();
        RuleFor(command => command.ReferenceNumber).ValidReferenceNumber();
    }
}
