using FluentValidation;

namespace Gym.Application.Payments.RegisterRefund;

public sealed class RegisterRefundValidator : AbstractValidator<RegisterRefundCommand>
{
    public RegisterRefundValidator()
    {
        RuleFor(command => command.Amount).ValidAmount();
        RuleFor(command => command.Method).ValidMethod();
        RuleFor(command => command.ReferenceNumber).ValidReferenceNumber();
        RuleFor(command => command.Reason).ValidRefundReason();
    }
}
