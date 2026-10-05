using FluentValidation;

namespace Gym.Application.Payables.RegisterPayable;

public sealed class RegisterPayableValidator : AbstractValidator<RegisterPayableCommand>
{
    public RegisterPayableValidator()
    {
        RuleFor(command => command.Kind).ValidKind();
        RuleFor(command => command.Amount).ValidAmount();
        RuleFor(command => command.Payee).ValidPayee();
        RuleFor(command => command.Description).ValidDescription();
        RuleFor(command => command.CategoryId).ValidCategory();
        this.AddInstallmentNumbersRule(
            command => command.Kind, command => command.InstallmentNumber, command => command.InstallmentCount);
    }
}
